// Folder: SiegeEngine/Core/Events
// File: SelectSpriteEvent.cs
using System;
using System.Text.Json;
namespace SiegeEngine.Core.Events
{
    public class SelectSpriteEvent : IEvent
    {
        public string Type => "SelectSprite";
        public ulong PlayerId { get; set; }
        public string TexturePath { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public SelectSpriteEvent(ulong playerId, string texturePath, float width, float height)
        {
            PlayerId = playerId;
            TexturePath = texturePath;
            Width = width;
            Height = height;
        }
        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, PlayerId, TexturePath, Width, Height });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }
        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<SelectSpriteEvent>(json);
            PlayerId = obj.PlayerId;
            TexturePath = obj.TexturePath;
            Width = obj.Width;
            Height = obj.Height;
        }
    }
}