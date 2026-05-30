// Folder: CastleBuilder/Events
// File: ContextChangedEvent.cs
using System.Text;
using System.Text.Json;
using SiegeEngine.Core.Events;

namespace CastleBuilder.Events
{
    public class ContextChangedEvent : IEvent
    {
        public string Type => "ContextChanged";

        public string Context { get; set; }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(this);
            return Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<ContextChangedEvent>(json);
            Context = obj.Context;
        }
    }
}