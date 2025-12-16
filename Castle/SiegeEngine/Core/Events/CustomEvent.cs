using System;
using System.Collections.Generic;

namespace SiegeEngine.Core.Events
{
    public class CustomEvent : IEvent
    {
        public string Type { get; set; }
        public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();

        public byte[] Serialize()
        {
            // Implement JSON serialization if needed for networking
            return Array.Empty<byte>();
        }

        public void Deserialize(byte[] data)
        {
            // Implement if needed
        }
    }
}