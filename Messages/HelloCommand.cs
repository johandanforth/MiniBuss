using System;

namespace Messages
{
    public class HelloCommand 
    {
        public Guid Guid { get; set; }
        public string Message { get; set; }
        public int Number { get; set; }
    }
}