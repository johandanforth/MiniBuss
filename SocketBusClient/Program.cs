using System;
using System.Threading;
using System.Threading.Tasks;
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
            _bus.RegisterMessageHandler<PingMessage>(ping => Console.WriteLine("Got a ping!"));
            _bus.Start();

            //notify that we're ready
            BusStarted.Set();
        }

        private static void SendHelloMessages()
        {
            _bus.Send(new HelloMessage() {Guid = Guid.NewGuid(), Message = "Hello 1 from client", Number = 1});
            _bus.Send(new HelloMessage() {Guid = Guid.NewGuid(), Message = "Hello 2 from client", Number = 2});
            _bus.Send(new HelloMessage() {Guid = Guid.NewGuid(), Message = "Hello 3 from client", Number = 3});
            _bus.Send(new HelloMessage() {Guid = Guid.NewGuid(), Message = "Hello 4 from client", Number = 4});
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