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

            // Emit PhysicsCollisionEvent for every live manifold so the already-subscribed
            // GameServer.OnPhysicsCollision path becomes active.
            var manifolds = _world.CurrentManifolds;
            if (manifolds != null && manifolds.Count > 0)
            {
                var entities = _server.GetEntities();
                for (int m = 0; m < manifolds.Count; m++)
                {
                    var man = manifolds[m];
                    if (man == null || man.PointCount <= 0) continue;

                    int sourceId = 0;
                    int targetId = 0;
                    for (int i = 0; i < entities.Count; i++)
                    {
                        var e = entities[i];
                        if (e == null) continue;
                        var p = e.GetComponent<PhysicsComponent>();
                        if (p == null) continue;
                        if (p == man.BodyA) sourceId = e.Id;
                        if (p == man.BodyB) targetId = e.Id;
                    }

                    Vector3 force = Vector3.Zero;
                    if (man.PointCount > 0)
                    {
                        var cp = man.Points[0];
                        float mag = MathF.Max(cp.Penetration, 0.01f) * (man.BodyA?.Mass ?? 1f);
                        force = cp.Normal * mag;
                    }

                    _server.Publish(new SiegeEngine.Core.Events.PhysicsCollisionEvent(sourceId, targetId, force), true);
                }
            }
        }
    }
}