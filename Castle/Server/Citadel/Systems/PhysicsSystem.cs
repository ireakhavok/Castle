using System;
using System.Collections.Generic;
using System.Numerics;
using Citadel.Server;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Systems;

namespace Citadel.Systems
{
    public class PhysicsSystem : GameSystem
    {
        private Dictionary<int, Vector3> _lastPositions = new Dictionary<int, Vector3>();

        public PhysicsSystem(GameServer server) : base(server) { }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _server.GetEntities())
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null)
                {
                    // Log only on significant position change
                    if (!_lastPositions.TryGetValue(entity.Id, out var lastPos) || Vector3.Distance(lastPos, physics.Position) > 0.01f)
                    {
                        Console.WriteLine($"PhysicsSystem: Updating entity {entity.Id} at {physics.Position}");
                        _lastPositions[entity.Id] = physics.Position;
                    }
                }
            }
        }
    }
}