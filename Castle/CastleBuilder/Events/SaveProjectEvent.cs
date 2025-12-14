// Folder: CastleBuilder/Events
// File: SaveProjectEvent.cs
using System.Text.Json;
using System.Text;

namespace CastleBuilder.Events
{
    public class SaveProjectEvent : SiegeEngine.Events.IEvent
    {
        public string Type => "SaveProject";
        public string Path { get; set; }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(this);
            return Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<SaveProjectEvent>(json);
            Path = obj.Path;
        }
    }
}