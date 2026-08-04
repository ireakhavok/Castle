// Folder: Citadel/Systems
// File: PhysicsSystem.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using Citadel.Server;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Physics;
using SiegeEngine.Systems;

namespace Citadel.Systems
{
    public class PhysicsSystem : GameSystem
    {
        private readonly PhysicsWorld _world;
        private readonly Dictionary<int, Vector3> _lastPositions = new Dictionary<int, Vector3>();

        public PhysicsSystem(GameServer server) : base(server)
        {
            _world = new PhysicsWorld();
        }

        public PhysicsWorld World => _world;

        public override void Update(float deltaTime)
        {
            // Keep the body list in sync with the server entities that carry a PhysicsComponent
            foreach (var entity in _server.GetEntities())
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null)
                {
                    _world.RegisterBody(physics);

                    // Existing debug logging (preserved)
                    if (!_lastPositions.TryGetValue(entity.Id, out var lastPos) || Vector3.Distance(lastPos, physics.Position) > 0.01f)
                    {
                        Console.WriteLine($"PhysicsSystem: Updating entity {entity.Id} at {physics.Position}");
                        _lastPositions[entity.Id] = physics.Position;
                    }
                }
            }

            _world.Step(deltaTime);
        }
    }
}