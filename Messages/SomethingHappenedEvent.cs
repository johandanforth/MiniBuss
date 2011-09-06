using System;

namespace Messages
{
    public class SomethingHappenedEvent 
    {
        public Guid Guid { get; set; }
        public DateTime Sent { get; set; }
    }
}