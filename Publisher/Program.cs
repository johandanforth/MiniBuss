using System;
using System.Threading;
using System.Threading.Tasks;
using Messages;

using MiniBuss;

namespace Publisher
{
    class Program
    {
        static void Main()
        {
            var bus = new ServiceBus { LocalEndpoint = "minibuss_publisher1" };

            bus.HandleSubscriptionsFor<SomethingHappenedEvent>();

            bus.Start();    

            Task.Factory.StartNew(() => PublishingThread(bus), TaskCreationOptions.LongRunning);

            Console.WriteLine("Working, press ENTER to exit");
            Console.ReadLine();
        }

        private static void PublishingThread(IServiceBus bus)
        {
            while (true)
            {
                var guid = Guid.NewGuid();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Publishing event with guid " + guid);
                bus.Publish(new SomethingHappenedEvent { Guid = guid, Sent = DateTime.Now });
                Thread.Sleep(1000);
            }
        }
    }
}
