using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Messages;
using SocketBus.Extensions;

namespace SocketBus
{
    public class SocketServiceBusServer : SocketServiceBus, ISocketServiceBusServer
    {
        private static readonly List<Socket> Connections = new List<Socket>(); //concurrency handled with lock()
        private static readonly ManualResetEvent AllDone = new ManualResetEvent(false);
        private Socket _listener;
        private readonly string _localEndpoint;

        public SocketServiceBusServer(string localEndpoint)
        {
            _localEndpoint = localEndpoint;
        }


        public void Publish<T>(Message<T> message)
        {
            //loop through all connected clients and send
            List<Socket> connectedSockets;
            lock (Connections)
            {
                connectedSockets = Connections.Where(s => s.IsConnected()).ToList();
            }
            Console.WriteLine("Sending to " + connectedSockets.Count + " clients...");
            Parallel.ForEach(connectedSockets, socket => Send(socket, message));
        }

       public void Start()
        {
            ConsoleInfo("Starting socket server on " + _localEndpoint);

            var index = _localEndpoint.IndexOf(":", StringComparison.Ordinal);
            var hostname = _localEndpoint.Substring(0, index);
            var port = Convert.ToInt32(_localEndpoint.Substring(index + 1));

            var ipHostInfo = Dns.GetHostEntry(hostname);

            var ipAddress = ipHostInfo.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ipAddress == null)
            {
                throw new Exception("Could not get ip4 address!");
            }
            var localEndPoint = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP socket.
            _listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                _listener.Bind(localEndPoint);
                _listener.Listen(100);

                while (true)
                {
                    // Set the event to nonsignaled state.
                    AllDone.Reset();

                    // Start an asynchronous socket to listen for connections.
                    Console.WriteLine("Waiting for a connection...");
                    _listener.BeginAccept(
                        AcceptCallback,
                        _listener);

                    // Wait until a connection is made before continuing.
                    AllDone.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void Stop()
        {
            ConsoleInfo("Stopping server..");

            _listener.Shutdown(SocketShutdown.Both);
            _listener.Close();
        }

        protected override void HandleLostConnection(Socket socket, SocketException se)
        {
            
            ConsoleError("Server lost connection with client! " + se.Message);
            Connections.Remove(socket);
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.
            AllDone.Set();

            // Get the socket that handles the client request.
            var listener = (Socket) ar.AsyncState;
            var handler = listener.EndAccept(ar);

            Connections.Add(handler);

            // Create the state object.
            var state = new StateObject {WorkSocket = handler};
            handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                ReceiveCallback, state);
        }
    }
}