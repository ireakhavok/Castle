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
        public string BrushShape { get; set; }
        public string BrushFalloff { get; set; }

        // NEW for Step 2: texture/material painting
        public int PaintLayer { get; set; }

        // NEW: carries the selected material albedo path for Paint mode stickers (event-driven)
        public string MaterialPath { get; set; } = string.Empty;

        public SelectBrushEvent(ulong playerId, string brushMode, float size, float intensity, string brushShape, string brushFalloff, int paintLayer = 0, string materialPath = "")
        {
            PlayerId = playerId;
            BrushMode = brushMode;
            Size = size;
            Intensity = intensity;
            BrushShape = brushShape;
            BrushFalloff = brushFalloff;
            PaintLayer = paintLayer;
            MaterialPath = materialPath ?? string.Empty;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new
            {
                Type,
                PlayerId,
                BrushMode,
                Size,
                Intensity,
                BrushShape,
                BrushFalloff,
                PaintLayer,
                MaterialPath
            });
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
            BrushShape = obj.BrushShape;
            BrushFalloff = obj.BrushFalloff;
            PaintLayer = obj.PaintLayer;
            MaterialPath = obj.MaterialPath ?? string.Empty;
        }
    }
}