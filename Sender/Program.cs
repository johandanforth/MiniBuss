using System;
using System.Threading;
using System.Threading.Tasks;
using Messages;
using MiniBuss;

namespace Sender
{
    class Program
    {
        static void Main()
        {
            var bus = new ServiceBus { LocalEndpoint = "minibuss_sender1" };

            bus.RegisterMessageEndpoint<HelloCommand>("minibuss_receiver1@localhost");

            bus.RegisterMessageHandler<HelloResponse>(reply =>
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Reply from receiver should not be called: " + reply.Message);
                    });

            bus.Start();    //start bus if expecting replies

            Task.Factory.StartNew(() => SendThread(bus), TaskCreationOptions.LongRunning);

            Console.WriteLine("Working, press ENTER to exit");
            Console.ReadLine();
        }
        private static ManualResetEvent wait = new ManualResetEvent(false);
        private static void SendThread(IServiceBus bus)
        {
            var random = new Random();

            for (var i = 0; i < 1000; i++)
            {
                wait.Reset();
                var guid = Guid.NewGuid();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Sending command with guid " + guid);
                bus.Send(new HelloCommand { Guid = Guid.NewGuid(), Message = "Hello", Number = random.Next() }).Register
                    <HelloResponse>(x =>
                                        {
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine("Reply from receiver: " + x.Message);
                                            wait.Set();
                                        });
                wait.WaitOne(TimeSpan.FromSeconds(10));
            }
            Console.WriteLine("Done!");
        }
    }
}
