// Folder: SiegeEngine/Core/Definitions
// File: LightComponent.cs
using System;
using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace SiegeEngine.Core.Definitions
{
    /// <summary>
    /// Unified light used by gameplay, the editor, and the renderer.
    /// Rasterized shadow maps are the current implementation. ShadowMode.RayTraced
    /// and Auto are reserved so a hardware RT path can be added without changing
    /// level content or gameplay code.
    /// </summary>
    public class LightComponent : IComponent, IComponentData
    {
        public LightType Type { get; set; }
        public Vector3 Color { get; set; }
        public float Intensity { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Direction { get; set; }
        public float AttenuationLinear { get; set; }
        public float AttenuationQuadratic { get; set; }

        public bool Enabled { get; set; } = true;
        public float Range { get; set; } = 25f;
        public float InnerConeDegrees { get; set; } = 20f;
        public float OuterConeDegrees { get; set; } = 30f;
        public bool CastShadows { get; set; } = true;
        public ShadowMode ShadowMode { get; set; } = ShadowMode.Auto;
        public float ShadowBias { get; set; } = 0.002f;
        public float ShadowNormalBias { get; set; } = 0.02f;

        public LightComponent()
        {
            Type = LightType.Point;
            Color = Vector3.One;
            Intensity = 1f;
            Direction = Vector3.Normalize(new Vector3(-0.85f, 0.10f, -0.52f));
        }

        public LightComponent(LightType type, Vector3 color, float intensity, Vector3 positionOrDirection, float attLinear = 0f, float attQuadratic = 0f)
        {
            Type = type;
            Color = color;
            Intensity = intensity;
            if (type == LightType.Directional)
            {
                Direction = Vector3.Normalize(positionOrDirection);
            }
            else
            {
                Position = positionOrDirection;
                AttenuationLinear = attLinear;
                AttenuationQuadratic = attQuadratic;
            }
        }

        public Vector3 ResolvedDirection()
        {
            if (Direction.LengthSquared() < 1e-8f)
                return Vector3.Normalize(new Vector3(-0.85f, 0.10f, -0.52f));
            return Vector3.Normalize(Direction);
        }

        public object ToSerializableData()
        {
            return new LightComponentData
            {
                Type = Type,
                Color = Color,
                Intensity = Intensity,
                Position = Position,
                Direction = Direction,
                AttenuationLinear = AttenuationLinear,
                AttenuationQuadratic = AttenuationQuadratic,
                Enabled = Enabled,
                Range = Range,
                InnerConeDegrees = InnerConeDegrees,
                OuterConeDegrees = OuterConeDegrees,
                CastShadows = CastShadows,
                ShadowMode = ShadowModeParser.ToPayloadString(ShadowMode),
                ShadowBias = ShadowBias,
                ShadowNormalBias = ShadowNormalBias
            };
        }

        public void FromSerializableData(object data)
        {
            if (data == null)
                return;

            if (data is LightComponentData typed)
            {
                ApplyData(typed);
                return;
            }

            if (data is JsonElement element)
            {
                ApplyData(ReadFromJson(element));
                return;
            }

            if (data is string raw && !string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(raw);
                    ApplyData(ReadFromJson(doc.RootElement));
                }
                catch
                {
                }
            }
        }

        private void ApplyData(LightComponentData l)
        {
            if (l == null)
                return;
            Type = l.Type;
            Color = l.Color;
            Intensity = l.Intensity;
            Position = l.Position;
            Direction = l.Direction;
            AttenuationLinear = l.AttenuationLinear;
            AttenuationQuadratic = l.AttenuationQuadratic;
            Enabled = l.Enabled;
            Range = l.Range > 0f ? l.Range : 25f;
            InnerConeDegrees = l.InnerConeDegrees;
            OuterConeDegrees = l.OuterConeDegrees;
            CastShadows = l.CastShadows;
            if (!ShadowModeParser.TryParse(l.ShadowMode, out ShadowMode parsed))
                parsed = ShadowMode.Auto;
            ShadowMode = parsed;
            ShadowBias = l.ShadowBias;
            ShadowNormalBias = l.ShadowNormalBias;
        }

        private static LightComponentData ReadFromJson(JsonElement el)
        {
            var data = new LightComponentData();
            if (el.ValueKind != JsonValueKind.Object)
                return data;

            if (TryGetProperty(el, "Type", out JsonElement typeEl))
            {
                if (typeEl.ValueKind == JsonValueKind.Number && typeEl.TryGetInt32(out int typeInt))
                    data.Type = (LightType)typeInt;
                else if (typeEl.ValueKind == JsonValueKind.String && Enum.TryParse(typeEl.GetString(), true, out LightType parsedType))
                    data.Type = parsedType;
            }

            data.Color = ReadVec3(el, "Color", Vector3.One);
            data.Intensity = ReadFloat(el, "Intensity", 1f);
            data.Position = ReadVec3(el, "Position", Vector3.Zero);
            data.Direction = ReadVec3(el, "Direction", new Vector3(-0.85f, 0.10f, -0.52f));
            data.AttenuationLinear = ReadFloat(el, "AttenuationLinear", 0f);
            data.AttenuationQuadratic = ReadFloat(el, "AttenuationQuadratic", 0f);
            data.Enabled = ReadBool(el, "Enabled", true);
            data.Range = ReadFloat(el, "Range", 25f);
            data.InnerConeDegrees = ReadFloat(el, "InnerConeDegrees", 20f);
            data.OuterConeDegrees = ReadFloat(el, "OuterConeDegrees", 30f);
            data.CastShadows = ReadBool(el, "CastShadows", true);
            data.ShadowMode = ReadString(el, "ShadowMode", "Auto");
            data.ShadowBias = ReadFloat(el, "ShadowBias", 0.002f);
            data.ShadowNormalBias = ReadFloat(el, "ShadowNormalBias", 0.02f);
            return data;
        }

        private static bool TryGetProperty(JsonElement el, string name, out JsonElement value)
        {
            if (el.TryGetProperty(name, out value))
                return true;
            foreach (JsonProperty prop in el.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        private static Vector3 ReadVec3(JsonElement el, string name, Vector3 fallback)
        {
            if (!TryGetProperty(el, name, out JsonElement value))
                return fallback;
            if (value.ValueKind == JsonValueKind.Object)
            {
                float x = ReadNamedFloat(value, "X", fallback.X);
                float y = ReadNamedFloat(value, "Y", fallback.Y);
                float z = ReadNamedFloat(value, "Z", fallback.Z);
                return new Vector3(x, y, z);
            }
            if (value.ValueKind == JsonValueKind.Array)
            {
                float x = value.GetArrayLength() > 0 ? ReadNumber(value[0], fallback.X) : fallback.X;
                float y = value.GetArrayLength() > 1 ? ReadNumber(value[1], fallback.Y) : fallback.Y;
                float z = value.GetArrayLength() > 2 ? ReadNumber(value[2], fallback.Z) : fallback.Z;
                return new Vector3(x, y, z);
            }
            if (value.ValueKind == JsonValueKind.String)
            {
                var parts = value.GetString()?.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts != null && parts.Length >= 3
                    && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                    && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    return new Vector3(x, y, z);
            }
            return fallback;
        }

        private static float ReadNamedFloat(JsonElement el, string name, float fallback)
        {
            return TryGetProperty(el, name, out JsonElement value) ? ReadNumber(value, fallback) : fallback;
        }

        private static float ReadFloat(JsonElement el, string name, float fallback)
        {
            return TryGetProperty(el, name, out JsonElement value) ? ReadNumber(value, fallback) : fallback;
        }

        private static float ReadNumber(JsonElement value, float fallback)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float n))
                return n;
            if (value.ValueKind == JsonValueKind.String
                && float.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return parsed;
            return fallback;
        }

        private static bool ReadBool(JsonElement el, string name, bool fallback)
        {
            if (!TryGetProperty(el, name, out JsonElement value))
                return fallback;
            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int n))
                return n != 0;
            if (value.ValueKind == JsonValueKind.String)
            {
                string raw = value.GetString();
                if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) || raw == "1" || raw == "on")
                    return true;
                if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase) || raw == "0")
                    return false;
            }
            return fallback;
        }

        private static string ReadString(JsonElement el, string name, string fallback)
        {
            if (!TryGetProperty(el, name, out JsonElement value))
                return fallback;
            if (value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? fallback;
            return fallback;
        }

        private class LightComponentData
        {
            public LightType Type { get; set; }
            public Vector3 Color { get; set; }
            public float Intensity { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 Direction { get; set; }
            public float AttenuationLinear { get; set; }
            public float AttenuationQuadratic { get; set; }
            public bool Enabled { get; set; } = true;
            public float Range { get; set; } = 25f;
            public float InnerConeDegrees { get; set; } = 20f;
            public float OuterConeDegrees { get; set; } = 30f;
            public bool CastShadows { get; set; } = true;
            public string ShadowMode { get; set; } = "Auto";
            public float ShadowBias { get; set; } = 0.002f;
            public float ShadowNormalBias { get; set; } = 0.02f;
        }
    }
}
