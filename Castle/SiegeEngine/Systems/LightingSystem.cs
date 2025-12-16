using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Interfaces;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Systems
{
    public class LightingSystem : GameSystem
    {
        private readonly List<LightComponent> _directionalLights = new List<LightComponent>();
        private readonly List<LightComponent> _pointLights = new List<LightComponent>();

        public LightingSystem(IGameServer server) : base(server)
        {
            var sun = new LightComponent(LightType.Directional, new Vector3(1f, 1f, 1f), 1.0f, new Vector3(-0.5f, -1.0f, -0.5f));
            AddLight(sun);
        }

        public void AddLight(LightComponent light)
        {
            if (light.Type == LightType.Directional)
                _directionalLights.Add(light);
            else if (light.Type == LightType.Point)
                _pointLights.Add(light);
        }

        public void RemoveLight(LightComponent light)
        {
            if (light.Type == LightType.Directional)
                _directionalLights.Remove(light);
            else if (light.Type == LightType.Point)
                _pointLights.Remove(light);
        }

        public override void Update(float deltaTime)
        {
            // Update dynamic lights if needed
        }

        public List<LightComponent> GetDirectionalLights() => _directionalLights;
        public List<LightComponent> GetPointLights() => _pointLights;

        public (Vector3 direction, Vector3 color, float intensity)? GetShaderUniforms()
        {
            if (_directionalLights.Count > 0)
            {
                var light = _directionalLights[0];
                return (light.Direction, light.Color, light.Intensity);
            }
            return null;
        }
    }
}