using System;
using MiniBuss;

namespace Receiver
{
    /// <summary>
    /// This receiver is using local message classes which looks the same as the ones sent by the publisher.
    /// Namespace doesn't matter, just use the same class and property names.
    /// 
    /// It may also share message classes with the sender for convenience.
    /// </summary>
    class Program
    {
        static void Main()
        {
            var bus = new ServiceBus { LocalEndpoint = "minibuss_receiver1" };

            bus.RegisterMessageHandler<HelloCommand>(command =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(command.Message + " Guid: " + command.Guid + ", Number: " + command.Number);

                bus.Reply(command, new HelloResponse { Guid = Guid.NewGuid(), Message = "Hello back!" });
            });

            bus.Start();

            Console.WriteLine("Waiting for commands, press ENTER to exit");
            Console.ReadLine();
        }
    }

    public class HelloResponse
    {
        public Guid Guid { get; set; }
        public string Message { get; set; }
    }

    public class HelloCommand
    {
        public Guid Guid { get; set; }
        public string Message { get; set; }
        public int Number { get; set; }
    }

}
