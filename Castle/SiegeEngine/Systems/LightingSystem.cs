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
        private readonly List<LightComponent> _spotLights = new List<LightComponent>();
        private readonly LightComponent _defaultSun;

        public LightingSystem(IGameServer server) : base(server)
        {
            _defaultSun = new LightComponent(LightType.Directional, new Vector3(1f, 1f, 1f), 1.0f, new Vector3(-0.85f, 0.10f, -0.52f));
            AddLight(_defaultSun);
        }

        public LightComponent DefaultSun => _defaultSun;

        public void AddLight(LightComponent light)
        {
            if (light == null) return;
            if (light.Type == LightType.Directional)
                _directionalLights.Add(light);
            else if (light.Type == LightType.Point)
                _pointLights.Add(light);
            else if (light.Type == LightType.Spot)
                _spotLights.Add(light);
        }

        public void RemoveLight(LightComponent light)
        {
            if (light == null) return;
            if (light.Type == LightType.Directional)
                _directionalLights.Remove(light);
            else if (light.Type == LightType.Point)
                _pointLights.Remove(light);
            else if (light.Type == LightType.Spot)
                _spotLights.Remove(light);
        }

        public override void Update(float deltaTime)
        {
            SyncFromEntities();
        }

        public void SyncFromEntities()
        {
            _directionalLights.Clear();
            _pointLights.Clear();
            _spotLights.Clear();

            var entities = _server?.GetEntities();
            if (entities != null)
            {
                foreach (var entity in entities)
                {
                    var light = entity.GetComponent<LightComponent>();
                    if (light == null || !light.Enabled)
                        continue;

                    var physics = entity.GetComponent<PhysicsComponent>();
                    if (physics != null && light.Type != LightType.Directional)
                    {
                        light.Position = physics.Position;
                        if (light.Type == LightType.Spot && light.Direction.LengthSquared() < 1e-8f)
                        {
                            Vector3 forward = Vector3.Transform(Vector3.UnitY, physics.Rotation);
                            if (forward.LengthSquared() > 1e-8f)
                                light.Direction = Vector3.Normalize(forward);
                        }
                    }

                    AddLight(light);
                }
            }

            if (_directionalLights.Count == 0)
                _directionalLights.Add(_defaultSun);
        }

        public List<LightComponent> GetDirectionalLights() => _directionalLights;
        public List<LightComponent> GetPointLights() => _pointLights;
        public List<LightComponent> GetSpotLights() => _spotLights;

        public IEnumerable<LightComponent> GetAllLights()
        {
            foreach (var light in _directionalLights) yield return light;
            foreach (var light in _pointLights) yield return light;
            foreach (var light in _spotLights) yield return light;
        }

        public (Vector3 direction, Vector3 color, float intensity)? GetShaderUniforms()
        {
            if (_directionalLights.Count > 0)
            {
                var light = _directionalLights[0];
                return (light.ResolvedDirection(), light.Color, light.Intensity);
            }
            return null;
        }
    }
}
