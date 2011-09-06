using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Messaging;
using System.Threading.Tasks;
using System.Transactions;

namespace MiniBuss
{
    public interface IServiceBus
    {
        void RegisterMessageHandler<TCommand>(Action<TCommand> handler) where TCommand : class;
        void RegisterMessageEndpoint<TCommand>(string targetEndpoint) where TCommand : class;
        void HandleSubscriptionsFor<TEvent>() where TEvent : class;

        void Send(object command);
        void Reply(object message, object response);
        void Publish(object @event);

        void Subscribe<TEvent>(string publisherEndpoint, Action<TEvent> handler) where TEvent : class;
        void UnSubscribe<TEvent>(string publisherEndpoint) where TEvent : class;

        void Start();
        void Stop();

        string LocalEndpoint { get; set; }
    }

    public class ServiceBus : IServiceBus
    {
        private static readonly ConcurrentDictionary<RuntimeTypeHandle, string> ReplyQueues = new ConcurrentDictionary<RuntimeTypeHandle, string>();
        private static readonly ConcurrentDictionary<RuntimeTypeHandle, string> TargetQueues = new ConcurrentDictionary<RuntimeTypeHandle, string>();
        private static readonly ConcurrentDictionary<Type, object> MessageHandlers = new ConcurrentDictionary<Type, object>();
        private static readonly List<Subscription> Subscriptions = new List<Subscription>();    //concurrency handled with lock()
        private static readonly ConcurrentDictionary<string, Type> HandledSubscriptions = new ConcurrentDictionary<string, Type>();

        private MessageQueue _queue;

        public ServiceBus() { }

        public ServiceBus(string localEndpoint)
        {
            LocalEndpoint = localEndpoint;
        }

        private string _localEndpoint;
        public string LocalEndpoint
        {
            get { return _localEndpoint; }
            set { _localEndpoint = GetEndpointName(value); }
        }

        private static string GetEndpointName(string value)
        {
            var machine = ".";
            var queue = value;
            if (value.Contains("@"))
            {
                machine = value.Split('@')[1];
                queue = value.Split('@')[0];
            }
            if (machine == "localhost") machine = ".";
            return machine + "\\private$\\" + queue;
        }

        public void RegisterMessageEndpoint<TCommand>(string targetEndpoint) where TCommand : class
        {
            TargetQueues[typeof(TCommand).TypeHandle] = GetEndpointName(targetEndpoint);
        }

        public void Reply(object message, object response)
        {
            var rq = ReplyQueues[message.GetType().TypeHandle];
            if (rq == null) throw new InvalidOperationException("Endpoint for replying not found for current message");

            SendMessage(response, rq);
        }

        public void Send(object command)
        {
            var targetQueue = TargetQueues[command.GetType().TypeHandle];
            SendMessage(command, targetQueue);
        }

        private void SendMessage(object msg, string targetQueue)
        {
            var type = msg.GetType();
            if (type.Name == null) throw new Exception("Should not be possible");

            var message = new Message { Body = msg, Recoverable = true, Label = type.Name };
            var msgQ = new MessageQueue(targetQueue);

            if (LocalEndpoint != null)  //we expect and handle replies, so add a response-queue
            {
                var responseQ = new MessageQueue(LocalEndpoint);
                message.ResponseQueue = responseQ;
            }

            if (msgQ.Transactional)
            {
                using (var tx = new TransactionScope())
                {
                    msgQ.Send(message, MessageQueueTransactionType.Automatic);
                    tx.Complete();
                }
            }
            else
            {
                msgQ.Send(message);
            }
        }

        private static void CreateTransactionalQueueIfNotExists(string queueName)
        {
            if (!MessageQueue.Exists(queueName))
                MessageQueue.Create(queueName, true);
        }

        public void RegisterMessageHandler<TCommand>(Action<TCommand> handler) where TCommand : class
        {
            var name = typeof(TCommand).Name;
            if (name == null) throw new InvalidOperationException("Should not happen");

            MessageHandlers[typeof(TCommand)] = CastArgument<object, TCommand>(x => handler(x));
        }

        private static Action<TBase> CastArgument<TBase, TDerived>(Expression<Action<TDerived>> source) where TDerived : TBase
        {
            if (typeof(TDerived) == typeof(TBase))
                return (Action<TBase>)((Delegate)source.Compile());
            var sourceParameter = Expression.Parameter(typeof(TBase), "source");
            var result = Expression.Lambda<Action<TBase>>(Expression.Invoke(source, Expression.Convert(sourceParameter, typeof(TDerived))), sourceParameter);
            return result.Compile();
        }

        public void Start()
        {
            ConsoleInfo("Starting local endpoint queue: " + LocalEndpoint);

            CreateTransactionalQueueIfNotExists(LocalEndpoint);
            CreateTransactionalQueueIfNotExists(LocalEndpoint + "_errors");

            _queue = new MessageQueue(LocalEndpoint)
                        {
                            MessageReadPropertyFilter = { AppSpecific = true }
                        };
            _queue.ReceiveCompleted += QueueReceiveCompleted;   //NOTE: this whole bit could be removed if we decided to not use non-transactional queues
            _queue.PeekCompleted += QueuePeekCompleted;
            if (_queue.Transactional)
                _queue.BeginPeek();
            else
                _queue.BeginReceive();
        }

        public void Stop()
        {
            _queue.ReceiveCompleted -= QueueReceiveCompleted;
            _queue.PeekCompleted -= QueuePeekCompleted;
        }

        private static void QueuePeekCompleted(object sender, PeekCompletedEventArgs e)
        {
            var cmq = (MessageQueue)sender;
            cmq.EndPeek(e.AsyncResult);

            Message msg = null; //keep outside scope to move this to the error log 
            try
            {
                msg = cmq.Receive();

                if (msg == null) throw new InvalidOperationException("Null message should not be possible");

                if (msg.AppSpecific == 0)
                    HandleMessage(msg);
                else
                    HandleSubscribeAndUnsubscribeMessage(msg);
            }
            catch (Exception ex)
            {
                ConsoleError("Exception in Peek: " + ex.Message);

                if (msg != null)
                    using (var scope = new TransactionScope())
                    {
                        using (var myQueue = new MessageQueue(cmq.MachineName + "\\" + cmq.QueueName + "_errors"))
                        {
                            myQueue.Send(msg, MessageQueueTransactionType.Automatic);
                        }
                        scope.Complete();
                    }
            }
            cmq.Refresh();
            cmq.BeginPeek();
        }

        private static void QueueReceiveCompleted(object sender, ReceiveCompletedEventArgs e)
        {
            var cmq = (MessageQueue)sender;

            Message msg = null; //keep outside scope to move this to the error log 
            try
            {
                msg = cmq.EndReceive(e.AsyncResult);
                if (msg.AppSpecific == 0)
                    HandleMessage(msg);
                else
                    HandleSubscribeAndUnsubscribeMessage(msg);

            }
            catch (Exception ex)
            {
                ConsoleError("Exception while receiving message: " + ex.Message);
                if (msg != null)
                    using (var scope = new TransactionScope())
                    {
                        using (var myQueue = new MessageQueue(cmq.MachineName + "\\" + cmq.QueueName + "_errors"))
                        {
                            myQueue.Send(msg, MessageQueueTransactionType.Automatic);
                        }
                        scope.Complete();
                    }

            }
            cmq.Refresh();
            cmq.BeginReceive();
        }

        private static void HandleMessage(Message msg)
        {
            var types = MessageHandlers.Select(h => h.Key).ToArray();

            msg.Formatter = new XmlMessageFormatter(types);
            var message = msg.Body;
            if (message == null) throw new Exception("Could not extract message from msg body - unknown message to us?");

            var messageType = message.GetType();
            var handler = MessageHandlers[messageType] as Action<object>; //will throw if no handler is found

            if (msg.ResponseQueue != null)
                ReplyQueues.TryAdd(message.GetType().TypeHandle,
                                    msg.ResponseQueue.MachineName + "\\" + msg.ResponseQueue.QueueName);

            //execute the delegate for this message
            if (handler != null) handler(message);

            if (msg.ResponseQueue != null)
            {
                string rq;
                var res = ReplyQueues.TryRemove(message.GetType().TypeHandle, out rq);
                if (res == false) throw new Exception("Could not remove reply-queue, should not happen");
            }
        }

        public void Publish(object @event)
        {
            List<Subscription> subscriptions;
            lock (Subscriptions)
            {
                subscriptions = Subscriptions.Where(s => s.Type == @event.GetType()).ToList();
            }

            Parallel.ForEach(subscriptions, subscription =>
                    {
                        var type = subscription.Type;
                        if (type.Name == null) throw new Exception("Should not be possible");

                        var message = new Message { Body = @event, Recoverable = true, Label = type.Name };

                        //NOTE: Should published messages be removed if not handled? Easy to do with a TimeToBeReceived setting
                        //message.TimeToBeReceived = new TimeSpan(0,0,0,10);    //remove from queue after 10 secs
                        var msgQ = new MessageQueue(subscription.SubscriberQueue);
                        if (msgQ.Transactional)
                        {
                            using (var mqt = new MessageQueueTransaction())
                            {
                                mqt.Begin();
                                msgQ.Send(message, mqt);
                                mqt.Commit();
                            }
                        }
                        else
                        {
                            msgQ.Send(message);
                        }
                    });
        }

        public void HandleSubscriptionsFor<TEvent>() where TEvent : class
        {
            var type = typeof(TEvent);
            if (type.Name == null) throw new Exception("Should not be possible");

            if (!HandledSubscriptions.ContainsKey(type.Name))
                HandledSubscriptions[type.Name] = type;
        }

        private static void HandleSubscribeAndUnsubscribeMessage(Message subscriptionMsg)
        {
            var typestring = subscriptionMsg.Label; //label contains the message type name (no namespace, just class name)

            var type = HandledSubscriptions[typestring];

            var subscriptionCommand = (SubscriptionCommand)subscriptionMsg.AppSpecific;

            var queue = subscriptionMsg.ResponseQueue.MachineName + "\\" + subscriptionMsg.ResponseQueue.QueueName;

            switch (subscriptionCommand)
            {
                case SubscriptionCommand.Start:
                    ConsoleInfo("Start sending events of type " + typestring + " to " + queue);
                    lock (Subscriptions)
                    {
                        if (!Subscriptions.Any(s => s.Type == type && s.SubscriberQueue == queue))
                            Subscriptions.Add(new Subscription { Type = type, SubscriberQueue = queue });
                    }
                    break;
                case SubscriptionCommand.Stop:
                    ConsoleInfo("Stop sending events of type " + typestring + " to " + queue);
                    lock (Subscriptions)
                    {
                        if (Subscriptions.Any(s => s.Type == type && s.SubscriberQueue == queue))
                            Subscriptions.Remove(Subscriptions.Where(s => s.Type == type && s.SubscriberQueue == (queue)).FirstOrDefault());
                    }
                    break;
            }
        }

        public void Subscribe<TEvent>(string publisherEndpoint, Action<TEvent> handler) where TEvent : class
        {
            var type = typeof(TEvent);
            if (type.Name == null) throw new Exception("Should not be possible");

            MessageHandlers[typeof(TEvent)] = CastArgument<object, TEvent>(x => handler(x));

            var message = new Message { AppSpecific = (int)SubscriptionCommand.Start, Recoverable = true, Label = type.Name };
            SendSubscribeMessage(GetEndpointName(publisherEndpoint), message);
        }

        public void UnSubscribe<TEvent>(string publisherEndpoint) where TEvent : class
        {
            var type = typeof(TEvent);
            if (type.Name == null) throw new Exception("Should not be possible");

            var message = new Message { AppSpecific = (int)SubscriptionCommand.Stop, Recoverable = true, Label = type.Name };

            SendSubscribeMessage(GetEndpointName(publisherEndpoint), message);
        }

        private void SendSubscribeMessage(string publisherQueue, Message message)
        {
            var msgQ = new MessageQueue(publisherQueue);

            var responseQ = new MessageQueue(LocalEndpoint);
            message.ResponseQueue = responseQ;

            if (msgQ.Transactional)
            {
                using (var tx = new TransactionScope())
                {
                    msgQ.Send(message, MessageQueueTransactionType.Automatic);
                    tx.Complete();
                }
            }
            else
            {
                msgQ.Send(message);
            }
        }

        private static void ConsoleInfo(string text)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        private static void ConsoleError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(text);
            Console.ResetColor(); 
        }

        private enum SubscriptionCommand
        {
            Start = 1,
            Stop = 2
        }

        private class Subscription
        {
            public Type Type { get; set; }
            public string SubscriberQueue { get; set; }
        }
    }
   
}