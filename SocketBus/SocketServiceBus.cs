using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Messages;

namespace SocketBus
{
    public class SocketServiceBus
    {
        private static readonly ConcurrentDictionary<Type, object> MessageHandlers =
            new ConcurrentDictionary<Type, object>();

        private static readonly ManualResetEvent SendDone = new ManualResetEvent(false);

         protected static void ConsoleInfo(string text)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        protected static void ConsoleError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(text);
            Console.ResetColor();
        }


        public void RegisterMessageHandler<TMessage>(Action<TMessage> handler) where TMessage : class
        {
            var name = typeof (TMessage).Name;
            if (name == null) throw new InvalidOperationException("Should not happen");

            MessageHandlers[typeof (TMessage)] = CastArgument<object, TMessage>(x => handler(x));
        }

        private static Action<TBase> CastArgument<TBase, TDerived>(Expression<Action<TDerived>> source)
            where TDerived : TBase
        {
            if (typeof (TDerived) == typeof (TBase))
                return (Action<TBase>) ((Delegate) source.Compile());
            var sourceParameter = Expression.Parameter(typeof (TBase), "source");
            var result =
                Expression.Lambda<Action<TBase>>(
                    Expression.Invoke(source, Expression.Convert(sourceParameter, typeof (TDerived))), sourceParameter);
            return result.Compile();
        }

        protected virtual void HandleLostConnection(Socket socket, SocketException se)
        {
            //overridden i child impl.
        }

        protected void ReceiveCallback(IAsyncResult ar)
        {
            // var content = String.Empty;

            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            var state = (StateObject) ar.AsyncState;
            var handler = state.WorkSocket;

            // Read data from the client socket. 
            var bytesRead = 0;

            try
            {
                bytesRead = handler.EndReceive(ar);
            }
            catch (SocketException se)
            {
                HandleLostConnection(handler, se);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception while receiving data from " + handler.RemoteEndPoint + ": " + e.Message);
            }

            if (bytesRead <= 0) return;
            
            // There  might be more data, so store the data received so far.
            state.Sb.Append(Encoding.UTF8.GetString(state.Buffer, 0, bytesRead));

            // a complete message received?
            while (state.Sb.ToString().IndexOf("<EOF>", StringComparison.Ordinal) > -1)
            {
                var message = state.Sb.ToString().Substring(0, state.Sb.ToString().IndexOf("<EOF>", StringComparison.Ordinal));

                // remove the received message and keep appending bytes to the rest for the next while-turn
                state.Sb.Remove(0, state.Sb.ToString().IndexOf("<EOF>", StringComparison.Ordinal) + 5);
                
                HandleMessage(message);
            }

            handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                ReceiveCallback, state);
        }

        protected static void Send<T>(Socket client, Message<T> message)
        {
            var data = message.ToXml();
            var typeName = message.GetType().Name;
            var byteData = Encoding.UTF8.GetBytes(typeName + "#" + data + "<EOF>");

            // Begin sending the data to the remote device.
            client.BeginSend(byteData, 0, byteData.Length, 0,
                SendCallback, client);
        }

        private static object FromXml(string xml, Type t)
        {
            using (var s = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                var ser = new XmlSerializer(t);
                return ser.Deserialize(s);
            }
        }

        private static void HandleMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            var types = MessageHandlers.Select(h => h.Key).ToArray();
            var index = message.IndexOf("#", StringComparison.Ordinal);
            var messageClassName = message.Substring(0, index);
            var xml = message.Substring(index + 1);
            var messageType = types.Single(t => t.Name == messageClassName);

            var handler = MessageHandlers[messageType] as Action<object>; //will throw if no handler is found

            var obj = FromXml(xml, messageType);

            //execute the delegate for this message
            if (handler != null) handler(obj);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                var client = (Socket) ar.AsyncState;

                // Complete sending the data to the remote device.
                var bytesSent = client.EndSend(ar);
                ConsoleInfo("Sent " + bytesSent +" bytes.");

                // Signal that all bytes have been sent.
                SendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}