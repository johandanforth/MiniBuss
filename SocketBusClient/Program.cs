using System;
using System.Threading;
using Messages;
using SocketBus;

namespace SocketBusClient
{
    internal class Program
    {
        private static SocketServiceBusClient _bus;
        private static readonly ManualResetEvent BusStarted = new ManualResetEvent(false);

        private static void Main(string[] args)
        {
            ThreadPool.QueueUserWorkItem(BusThread);

            BusStarted.WaitOne();

            SendHelloMessages();

            Console.WriteLine("Running client...");
            Console.ReadLine();
        }

        private static void BusThread(object state)
        {
            _bus = new SocketServiceBusClient("localhost:11000");
            _bus.RegisterMessageHandler<PingMessage>(ping => Console.WriteLine("Got ping"));
            _bus.Start();

            //notify that we're ready
            BusStarted.Set();
        }

        private static void SendHelloMessages()
        {
            for (var i = 0; i < 10000; i++)
            {
                _bus.Send(new HelloMessage
                {
                    Guid = Guid.NewGuid(),
                    Message = "Hello " + i + " from client",
                    Number = i
                });
            }
        }
    }

    public class HelloMessage : Message<HelloMessage>
    {
        public Guid Guid { get; set; }
        public string Message { get; set; }
        public int Number { get; set; }
    }


    public class PingMessage : Message<PingMessage>
    {
    }
}