using System;

namespace SocketBus
{
    public interface ISocketServiceBusClient
    {
        void RegisterMessageHandler<TMessage>(Action<TMessage> handler) where TMessage : class;
        void Start();
        void Stop();
    }
}