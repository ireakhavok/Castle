// Folder: SiegeEngine/Core/Definitions
// File: SoundComponent.cs
using System;
using System.Text.Json;

namespace SiegeEngine.Core.Definitions
{
    public class SoundComponent : IComponent, IComponentData
    {
        public string AudioClip { get; set; } = "";
        public string Type { get; set; } = "SoundSource";
        public bool IsSensitive { get; set; } = false;
        public bool Loop { get; set; } = false;
        public float Volume { get; set; } = 1f;

        public object ToSerializableData()
        {
            return new SoundComponentData
            {
                AudioClip = AudioClip,
                Type = Type,
                IsSensitive = IsSensitive,
                Loop = Loop,
                Volume = Volume
            };
        }

        public void FromSerializableData(object data)
        {
            if (data == null) return;

            if (data is SoundComponentData s)
            {
                AudioClip = s.AudioClip ?? "";
                Type = s.Type ?? "SoundSource";
                IsSensitive = s.IsSensitive;
                Loop = s.Loop;
                Volume = s.Volume;
                return;
            }

            // After JSON round-trip the payload arrives as JsonElement (same pattern as PhysicsComponent)
            if (data is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                ApplyFromJsonElement(je);
                return;
            }

            if (data is string jsonStr)
            {
                using var doc = JsonDocument.Parse(jsonStr);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    ApplyFromJsonElement(doc.RootElement);
            }
        }

        private void ApplyFromJsonElement(JsonElement je)
        {
            if (je.TryGetProperty("AudioClip", out var ac) && ac.ValueKind == JsonValueKind.String)
                AudioClip = ac.GetString() ?? "";
            if (je.TryGetProperty("Type", out var t) && t.ValueKind == JsonValueKind.String)
                Type = t.GetString() ?? "SoundSource";
            if (je.TryGetProperty("IsSensitive", out var sens))
                IsSensitive = sens.ValueKind == JsonValueKind.True;
            if (je.TryGetProperty("Loop", out var loop))
                Loop = loop.ValueKind == JsonValueKind.True;
            if (je.TryGetProperty("Volume", out var vol) && vol.TryGetSingle(out float v))
                Volume = v;
        }

        private class SoundComponentData
        {
            public string AudioClip { get; set; }
            public string Type { get; set; }
            public bool IsSensitive { get; set; }
            public bool Loop { get; set; }
            public float Volume { get; set; }
        }
    }
}