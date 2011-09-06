using System;
using Messages;

namespace Subscriber2
{
    /// <summary>
    /// This subscriber is sharing the message assembly with the publisher. See subscriber1 on how
    /// to use local message classes.
    /// </summary>
    class Program
    {
        static void Main()
        {
            var bus = new MiniBuss.ServiceBus {LocalEndpoint = "minibuss_subscriber2"};

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
}
