// Folder: CastleBuilder/Events
// File: LoadProjectEvent.cs
using System.Text.Json;
using System.Text;

namespace CastleBuilder.Events
{
    public class LoadProjectEvent : SiegeEngine.Events.IEvent
    {
        public string Type => "LoadProject";
        public string Path { get; set; }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(this);
            return Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<LoadProjectEvent>(json);
            Path = obj.Path;
        }
    }
}