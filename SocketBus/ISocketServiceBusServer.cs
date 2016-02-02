using System;
using Messages;

namespace SocketBus
{
    public interface ISocketServiceBusServer
    {
        void RegisterMessageHandler<TMessage>(Action<TMessage> handler) where TMessage : class;
        void Publish<T>(Message<T> message);
        void Start();
        void Stop();
    }
}