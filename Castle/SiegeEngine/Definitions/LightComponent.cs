using System.Numerics;

namespace SiegeEngine.Definitions
{
    public class LightComponent : IComponent
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
    }
}