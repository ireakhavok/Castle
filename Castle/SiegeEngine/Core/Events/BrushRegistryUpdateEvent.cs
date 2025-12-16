using System;
using System.Numerics;
using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    public class BrushRegistryUpdateEvent : IEvent
    {
        public string Type => "BrushRegistryUpdate";
        public string BrushType { get; set; }
        public Vector3 Size { get; set; }
        public int TextureId { get; set; }

        public BrushRegistryUpdateEvent(string brushType, Vector3 size, int textureId)
        {
            BrushType = brushType;
            Size = size;
            TextureId = textureId;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, BrushType, Size, TextureId });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<BrushRegistryUpdateEvent>(json);
            BrushType = obj.BrushType;
            Size = obj.Size;
            TextureId = obj.TextureId;
        }
    }
}