using System;
using System.Threading.Tasks;
using System.Timers;
using Messages;
using SocketBus;

namespace SocketBusServer
{
    internal class Program
    {
        private static Timer _aTimer;
        private static SocketServiceBusServer _bus;

        private static void Main(string[] args)
        {
            Task.Factory.StartNew(BusThread);
            Task.Factory.StartNew(PingThread);

            Console.WriteLine("Running server...");
            Console.ReadLine();
        }

        private static void PingThread()
        {
            _aTimer = new Timer();
            _aTimer.Elapsed += OnTimedEvent;
            _aTimer.Interval = 5000;
            _aTimer.Enabled = true;
            _aTimer.Start();
        }

        private static void BusThread()
        {
            _bus = new SocketServiceBusServer("localhost:11000");

            _bus.RegisterMessageHandler<HelloMessage>(message => 
                Console.WriteLine(message.Message + " Guid: " + message.Guid));
            _bus.Start();
        }

        private static void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            _bus.Publish(new PingMessage());
        }
    }

// ReSharper disable once ClassNeverInstantiated.Global
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