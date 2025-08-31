using System;
using System.Text.Json;

namespace SiegeEngine.Events
{
    public class SelectBrushEvent : IEvent
    {
        public string Type => "SelectBrush";
        public ulong PlayerId { get; set; }
        public string BrushType { get; set; }

        public SelectBrushEvent(ulong playerId, string brushType)
        {
            PlayerId = playerId;
            BrushType = brushType;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, PlayerId, BrushType });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<SelectBrushEvent>(json);
            PlayerId = obj.PlayerId;
            BrushType = obj.BrushType;
        }
    }
}