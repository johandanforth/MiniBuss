using System;
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
                        Console.WriteLine("Reply from receiver: " + reply.Message);
                    });

            bus.Start();    //start bus if expecting replies

            Task.Factory.StartNew(() => SendThread(bus), TaskCreationOptions.LongRunning);

            Console.WriteLine("Working, press ENTER to exit");
            Console.ReadLine();
        }

        private static void SendThread(IServiceBus bus)
        {
            var random = new Random();

            for (var i = 0; i < 1000; i++)
            {
                var guid = Guid.NewGuid();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Sending command with guid " + guid);
                bus.Send(new HelloCommand { Guid = Guid.NewGuid(), Message = "Hello", Number = random.Next() });
            }
            Console.WriteLine("Done!");
        }
    }
}
