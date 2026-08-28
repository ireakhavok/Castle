// Folder: SiegeEngine/Core/Definitions
// File: LightComponent.cs
using System.Numerics;

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
            if (data is LightComponentData l)
            {
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
