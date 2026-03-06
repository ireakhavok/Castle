// Folder: SiegeEngine.Core.Events
// File: SelectBrushEvent.cs
using System;
using System.Text.Json;
namespace SiegeEngine.Core.Events
{
    public class SelectBrushEvent : IEvent
    {
        public string Type => "SelectBrush";
        public ulong PlayerId { get; set; }
        public string BrushMode { get; set; }
        public float Size { get; set; }
        public float Intensity { get; set; }
        public SelectBrushEvent(ulong playerId, string brushMode, float size, float intensity)
        {
            PlayerId = playerId;
            BrushMode = brushMode;
            Size = size;
            Intensity = intensity;
        }
        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, PlayerId, BrushMode, Size, Intensity });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }
        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<SelectBrushEvent>(json);
            PlayerId = obj.PlayerId;
            BrushMode = obj.BrushMode;
            Size = obj.Size;
            Intensity = obj.Intensity;
        }
    }
}