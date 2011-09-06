using System;

namespace Subscriber1
{
    /// <summary>
    /// This subscriber is using local message classes which looks the same as the ones sent by the publisher.
    /// Namespace doesn't matter, just use the same class and property names.
    /// </summary>
    class Program
    {
        static void Main()
        {
            var bus = new MiniBuss.ServiceBus {LocalEndpoint = "minibuss_subscriber1"};

            bus.Start();

            bus.Subscribe<SomethingHappenedEvent>("minibuss_publisher1@localhost", @event =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("something happened at {0}, event id {1}",
                    @event.Sent, @event.Guid);
            });

            Console.WriteLine("Waiting for events, press ENTER to exit");
            Console.ReadLine();

            bus.UnSubscribe<SomethingHappenedEvent>("minibuss_publisher1");
        }   
    }

    public class SomethingHappenedEvent
    {
        public Guid Guid { get; set; }
        public DateTime Sent { get; set; }
    }
}