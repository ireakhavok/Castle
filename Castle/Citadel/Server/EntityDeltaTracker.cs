using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.Definitions;

namespace Citadel.Server
{
    public class EntityDeltaTracker
    {
        private readonly Dictionary<int, Vector3> _lastPositions = new();

        public void Update(IReadOnlyList<Entity> entities)
        {
            foreach (var entity in entities)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null)
                {
                    _lastPositions[entity.Id] = physics.Position;
                }
            }
        }

        public Dictionary<int, Vector3> GetDeltas(IReadOnlyList<Entity> entities)
        {
            var deltas = new Dictionary<int, Vector3>();
            foreach (var entity in entities)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null && _lastPositions.TryGetValue(entity.Id, out var lastPos) && lastPos != physics.Position)
                {
                    deltas[entity.Id] = physics.Position;
                }
            }
            return deltas;
        }
    }
}