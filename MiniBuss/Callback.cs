using System;
using System.Linq;

namespace MiniBuss
{
    public class Callback : ICallback
    {
        private readonly string messageId;

        /// <summary>
        /// Creates a new instance of the callback object storing the given message id.
        /// </summary>
        /// <param name="messageId"></param>
        public Callback(string messageId)
        {
            this.messageId = messageId;
        }

        /// <summary>
        /// Event raised when the Register method is called.
        /// </summary>
        public event EventHandler<BusAsyncResultEventArgs> Registered;

        /// <summary>
        /// Returns the message id this object was constructed with.
        /// </summary>
        public string MessageId
        {
            get { return messageId; }
        }

        #region Implementation of ICallback

        IAsyncResult ICallback.Register(AsyncCallback callback, object state)
        {
            var result = new BusAsyncResult(callback, state);

            if (Registered != null)
                Registered(this, new BusAsyncResultEventArgs { Result = result, MessageId = messageId });

            return result;
        }

        void ICallback.Register<T>(Action<T> callback)
        {
            (this as ICallback).Register(
                asyncResult =>
                {
                    var cr = asyncResult.AsyncState as CompletionResult;
                    if (cr == null) return;
                    if ((cr.Messages.OfType<T>().Count() != 1))
                        return;
                    callback.Invoke(cr.Messages.First() as T);
                },
                null
                );
        }

        #endregion
    }
}