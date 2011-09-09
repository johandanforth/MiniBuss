using System;

namespace MiniBuss
{
    public class BusAsyncResultEventArgs : EventArgs
    {
        public BusAsyncResult Result { get; set; }

        public string MessageId { get; set; }
    }
}