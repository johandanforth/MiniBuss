# MiniBuss
{project:description}

# Source Repository at GitHub
The lastest source code for MiniBuss is managed at GitHub - [url:https://github.com/johandanforth/MiniBuss] but this will the place for tracking issues, discussions, documentation and so on for now. *If you want to fork the project, do it from GitHub*

# Important
*NOTE that MiniBuss is still a work in progress and even though it is being used in production, all features has not yet been fully tested under stress and in multi-threaded scenarios. Until then it's not recomended to use this code in production.*

## News / changes
*sept 6 - 2011* Moved source repository over to GitHub [url:https://github.com/johandanforth/MiniBuss]
*sept 1 - 2011* Removed dependency on IMessage for commands and events

## Installing
MiniBuss is best downloaded from *NuGet*, and currently there is only one package - MiniBuss. From the Package Manager Console type:

```
PM> Install-Package MiniBuss
```
This will create a new folder in your project called "MiniBuss" with a single file in it called ServiceBus.cs which contains all the source. The package will also add references to 2 additional .NET libraries; _System.Messaging_ and _System.Transactions_.

You may also download the single source file [url:ServiceBus.cs|http://minibuss.codeplex.com/SourceControl/changeset/view/69523#1515663] from the source code, but remember to add references to System.Messaging and System.Transactions.

## Getting Started
The best way to get started may be to download the solution from the source code which contains a number of very simple projects for send/receive and publish/subscribe# The samples below are taken from the sample code.

Setting up a sender may look something like this:

```
var bus = new ServiceBus();
bus.RegisterMessageEndpoint<HelloCommand>("minibuss_receiver1@johan-dell-ssd");
bus.Send(new HelloCommand { Guid = Guid.NewGuid(), Message = "Hello" });
```
Create the bus, register a message and tell it where messages of this type should go and send the message. *Note* that you don't have to create a local endpoint or start the bus when just sending a message. The remote endpoint must exist for the bus to be able to send a message though, or there will be an exception. There is no local queue where messages are written to and then sent - messages are delivered directly. Nothing prevents you from creating a store and forwad solution with MiniBuss.

Setting up a receiver may look something like this:

```
var bus = new ServiceBus { LocalEndpoint = "minibuss_receiver1" };
bus.RegisterMessageHandler<HelloCommand>(command => Console.WriteLine(command.Message + " Guid: " + command.Guid));
bus.Start();
```
Create the bus and tell it which endpoint to listen to (which creates a local MSMQ queue if necessary) and tell it which message type to listen 
for and which delegate to kick off when such a message is received.

Similarly, when doing a receive/reply, you would have to create the bus on the sender side with a local endpoint and register a message-handler for replies, like this:

```
var bus = new ServiceBus { LocalEndpoint = "minibuss_sender1" };
bus.RegisterMessageEndpoint<HelloCommand>("minibuss_receiver1@johan-dell-ssd");
bus.RegisterMessageHandler<HelloResponse>(reply => Console.WriteLine("Reply from receiver: " + reply.Message));
bus.Start();

Console.WriteLine("Sending command...");
bus.Send(new HelloCommand { Guid = Guid.NewGuid(), Message = "Hello" });
```
The receiver would do a bus.reply() like this:

```
var bus = new ServiceBus { LocalEndpoint = "minibuss_receiver1" };
bus.RegisterMessageHandler<HelloCommand>(command =>
 {
     Console.WriteLine(command.Message + " Guid: " + command.Guid);
     bus.Reply(command, new HelloResponse { Guid = Guid.NewGuid(), Message = "Hello back!" });
 });
 
bus.Start();
```
The MiniBus also supports publish to multiple subscribers. A simple publisher would create a bus with a local endpoint (to receive subscribe/unsubscribe commands), tell it to handle subscriptions for a certain event, then start publishing something every 5 seconds (as an example):

```
var bus = new ServiceBus { LocalEndpoint = "minibuss_publisher1" };
bus.HandleSubscriptionsFor<SomethingHappenedEvent>();
bus.Start();    

Task.Factory.StartNew(() => PublishingThread(bus), TaskCreationOptions.LongRunning);
 
Console.WriteLine("Done, press ENTER to exit");
 Console.ReadLine();
 
private static void PublishingThread(IServiceBus bus)
 {
     while (true)
     {
         Thread.Sleep(5000);
         var guid = Guid.NewGuid();
         Console.WriteLine("Publishing event with guid " + guid);
         bus.Publish(new SomethingHappenedEvent() { Guid = guid, Sent = DateTime.Now });
     }
 }
```
Any clients interesting in subscribing to events from the publisher would create a bus with a local endpoint, start the bus and then send a subscribe command to the publisher, telling it you’re interested in subscribing to a certain type of event and which delegate to handle it:

```
var bus = new ServiceBus {LocalEndpoint = "minibuss_subscriber1"};
bus.Start();

bus.Subscribe<SomethingHappenedEvent>("minibuss_publisher1@localhost", @event =>
{
       Console.WriteLine("something happened at {0}, event id {1}",
           @event.Sent, @event.Guid);
});
 
Console.WriteLine("Waiting for events, press ENTER to exit");
Console.ReadLine();
 
bus.UnSubscribe<SomethingHappenedEvent>("minibuss_publisher1");
```
## Exceptions in a Message Handler
If there's an exception in a message handler, the message will be moved to an xxxxxx_error queue created by MiniBuss. There is no retry (yet).
## Support for Transactions
MiniBuss supports use of TransactionScope() if the underlying MSMQ queue is transactional. When the service bus is started, MiniBuss will always create a transactional MSMQ queue if the queue doesn't already exist.
## Endpoints
When creating an endpoint, you're always working on your local computer localhost using a syntax like this:
```
var bus = new ServiceBus { LocalEndpoint = "MyEndpoint" };
```
This will create a local private queue called your-computer-name\private$\MyEndoint

When telling a message/command to go to an endpoint on the same/local machine:
```
bus.RegisterMessageEndpoint<HelloCommand>("MyEndpoint");
//or
bus.RegisterMessageEndpoint<HelloCommand>("MyEndpoint@localhost");
```
or if you want to send a message/command to a remote endpoint:
```
bus.RegisterMessageEndpoint<HelloCommand>("MyEndpoint@some-other-computer");
```
