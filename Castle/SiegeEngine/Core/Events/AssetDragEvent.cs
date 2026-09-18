// Folder: SiegeEngine/Core/Events
// File: AssetDragEvent.cs
using System.Text;
using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    public enum AssetDragPhase
    {
        Begin,
        Drop,
        Cancel
    }

    public class AssetDragEvent : IEvent
    {
        public string Type => "AssetDrag";
        public string Path { get; set; }
        public AssetDragPhase Phase { get; set; }

        public AssetDragEvent() { }

        public AssetDragEvent(string path, AssetDragPhase phase)
        {
            Path = path;
            Phase = phase;
        }

        public byte[] Serialize()
        {
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Type, Path, Phase }));
        }

        public void Deserialize(byte[] data)
        {
            var obj = JsonSerializer.Deserialize<AssetDragEvent>(Encoding.UTF8.GetString(data));
            Path = obj.Path;
            Phase = obj.Phase;
        }
    }
}
