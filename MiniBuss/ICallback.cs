using System;

namespace MiniBuss
{
    public interface ICallback
    {
        IAsyncResult Register(AsyncCallback callback, object state);

        void Register<T>(Action<T> callback) where T : class;
    }
}