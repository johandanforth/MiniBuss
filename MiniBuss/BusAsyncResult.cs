using System;
using System.Threading;

namespace MiniBuss
{
    /// <summary>
    /// Implementation of IAsyncResult returned when registering a callback.
    /// </summary>
    public class BusAsyncResult : IAsyncResult
    {
        private readonly AsyncCallback callback;
        private readonly CompletionResult result;
        private volatile bool completed;
        private readonly ManualResetEvent sync;

        /// <summary>
        /// Creates a new object storing the given callback and state.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        public BusAsyncResult(AsyncCallback callback, object state)
        {
            this.callback = callback;
            this.result = new CompletionResult();
            this.result.State = state;
            this.sync = new ManualResetEvent(false);
        }

        /// <summary>
        /// Stores the given error code and messages, 
        /// releases any blocked threads,
        /// and invokes the previously given callback.
        /// </summary>
        /// <param name="messages"></param>
        public void Complete(params object[] messages)
        {
            this.result.Messages = messages;
            this.completed = true;

            if (this.callback != null)
                try
                {
                    this.callback(this);
                }
                catch (Exception e)
                {
                    // log.Error(this.callback, e);
                }

            this.sync.Set();
        }

      
        #region IAsyncResult Members

        /// <summary>
        /// Returns a completion result containing the error code, messages, and state.
        /// </summary>
        public object AsyncState
        {
            get { return this.result; }
        }

        /// <summary>
        /// Returns a handle suitable for blocking threads.
        /// </summary>
        public WaitHandle AsyncWaitHandle
        {
            get { return this.sync; }
        }

        /// <summary>
        /// Returns false.
        /// </summary>
        public bool CompletedSynchronously
        {
            get { return false; }
        }

        /// <summary>
        /// Returns if the operation has completed.
        /// </summary>
        public bool IsCompleted
        {
            get { return this.completed; }
        }

        #endregion
    }

    public class CompletionResult
    {
        public object State { get; set; }

        public object[] Messages { get; set; }
    }
}