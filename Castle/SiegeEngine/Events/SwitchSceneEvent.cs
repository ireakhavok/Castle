using System;
using SiegeEngine.Events;

namespace SiegeEngine.Events
{
    public class SwitchSceneEvent : IEvent
    {
        public string Hook { get; set; }

        public byte[] Serialize()
        {
            return Array.Empty<byte>();
        }
    }
}