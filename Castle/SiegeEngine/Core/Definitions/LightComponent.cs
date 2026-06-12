// Folder: SiegeEngine/Core/Definitions
// File: LightComponent.cs
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    public class LightComponent : IComponent, IComponentData
    {
        public LightType Type { get; set; }
        public Vector3 Color { get; set; }
        public float Intensity { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Direction { get; set; }
        public float AttenuationLinear { get; set; }
        public float AttenuationQuadratic { get; set; }

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

        // NEW: IComponentData support for round-tripping
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
                AttenuationQuadratic = AttenuationQuadratic
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
        }
    }
}