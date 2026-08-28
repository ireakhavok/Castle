using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Physics;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace SiegeEngine.Core.Networking
{
    public class ClientGameServerProxy : IGameServer
    {
        private readonly EventBus _eventBus;
        private readonly List<Entity> _entities = new List<Entity>();
        private readonly List<GameSystem> _systems = new List<GameSystem>();
        private readonly PhysicsWorld _physicsWorld = new PhysicsWorld();
        private int _nextEntityId = 1;
        public ClientGameServerProxy(EventBus eventBus)
        {
            _eventBus = eventBus;
        }
        // Editor / debug surface
        public PhysicsWorld PhysicsWorld => _physicsWorld;
        public IReadOnlyList<ContactManifold> CurrentManifolds => _physicsWorld.CurrentManifolds;
        public void SetHeightProvider(IHeightProvider provider)
        {
            _physicsWorld.SetHeightProvider(provider);
        }
        public void SnapToGround(PhysicsComponent body)
        {
            _physicsWorld.SnapToGround(body);
        }
        public void AddEntity(Entity entity)
        {
            if (entity == null) return;
            var existing = _entities.Find(e => e.Id == entity.Id && entity.Id > 0);
            if (existing != null)
            {
                existing.Type = entity.Type;
                var existingPhysics = existing.GetComponent<PhysicsComponent>();
                var newPhysics = entity.GetComponent<PhysicsComponent>();
                if (existingPhysics != null && newPhysics != null)
                {
                    existingPhysics.Position = newPhysics.Position;
                    existingPhysics.Rotation = Entity.SanitizeRotation(newPhysics.Rotation);
                    existingPhysics.Scale = newPhysics.Scale;
                    existingPhysics.Size = newPhysics.Size;
                    existingPhysics.LocalBoundsMinCm = newPhysics.LocalBoundsMinCm;
                    existingPhysics.LocalBoundsMaxCm = newPhysics.LocalBoundsMaxCm;
                    existingPhysics.Velocity = newPhysics.Velocity;
                    existingPhysics.BodyType = newPhysics.BodyType;
                    existingPhysics.AngularVelocity = newPhysics.AngularVelocity;
                    existingPhysics.LinearDamping = newPhysics.LinearDamping;
                    existingPhysics.AngularDamping = newPhysics.AngularDamping;
                    existingPhysics.Friction = newPhysics.Friction;
                    existingPhysics.Restitution = newPhysics.Restitution;
                    existingPhysics.KineticFriction = newPhysics.KineticFriction;
                    existingPhysics.StaticFriction = newPhysics.StaticFriction;
                    existingPhysics.RollingResistance = newPhysics.RollingResistance;
                    existingPhysics.IsSleeping = newPhysics.IsSleeping;
                    existingPhysics.IslandId = newPhysics.IslandId;
                    existingPhysics.SleepThreshold = newPhysics.SleepThreshold;
                    existingPhysics.SleepTimer = newPhysics.SleepTimer;
                    existingPhysics.Mass = newPhysics.Mass;
                    existingPhysics.Health = newPhysics.Health;
                    existingPhysics.IsBreakable = newPhysics.IsBreakable;
                    existingPhysics.IsVisible = newPhysics.IsVisible;
                    existingPhysics.CollisionEnabled = newPhysics.CollisionEnabled;
                    existingPhysics.IsGrounded = newPhysics.IsGrounded;
                    existingPhysics.SlopeLimitDegrees = newPhysics.SlopeLimitDegrees;
                    existingPhysics.StepHeight = newPhysics.StepHeight;
                    existingPhysics.InvalidateShape();
                    var model = (existing.GetComponent<ModelComponent>() ?? entity.GetComponent<ModelComponent>())?.Model;
                    existingPhysics.RebuildShape(model);
                }
                var existingModel = existing.GetComponent<ModelComponent>();
                var newModel = entity.GetComponent<ModelComponent>();
                if (existingModel != null && newModel != null)
                {
                    existingModel.Key = newModel.Key;
                    existingModel.Model = newModel.Model;
                }
                var existingBlend = existing.GetComponent<BlendedAnimationComponent>();
                var newBlend = entity.GetComponent<BlendedAnimationComponent>();
                if (existingBlend == null && newBlend != null)
                {
                    existing.AddComponent(newBlend);
                }
                // Typed SoundComponent merge (same pattern as BlendedAnimation).
                // Must use the concrete type so the generic AddComponent keys correctly.
                var existingSound = existing.GetComponent<SoundComponent>();
                var newSound = entity.GetComponent<SoundComponent>();
                if (newSound != null)
                {
                    if (existingSound != null)
                    {
                        existingSound.AudioClip = newSound.AudioClip;
                        existingSound.Type = newSound.Type;
                        existingSound.IsSensitive = newSound.IsSensitive;
                        existingSound.Loop = newSound.Loop;
                        existingSound.Volume = newSound.Volume;
                    }
                    else
                    {
                        existing.AddComponent(newSound);
                    }
                }
                var existingLight = existing.GetComponent<LightComponent>();
                var newLight = entity.GetComponent<LightComponent>();
                if (newLight != null)
                {
                    if (existingLight != null)
                    {
                        existingLight.Type = newLight.Type;
                        existingLight.Color = newLight.Color;
                        existingLight.Intensity = newLight.Intensity;
                        existingLight.Position = newLight.Position;
                        existingLight.Direction = newLight.Direction;
                        existingLight.AttenuationLinear = newLight.AttenuationLinear;
                        existingLight.AttenuationQuadratic = newLight.AttenuationQuadratic;
                        existingLight.Enabled = newLight.Enabled;
                        existingLight.Range = newLight.Range;
                        existingLight.InnerConeDegrees = newLight.InnerConeDegrees;
                        existingLight.OuterConeDegrees = newLight.OuterConeDegrees;
                        existingLight.CastShadows = newLight.CastShadows;
                        existingLight.ShadowMode = newLight.ShadowMode;
                        existingLight.ShadowBias = newLight.ShadowBias;
                        existingLight.ShadowNormalBias = newLight.ShadowNormalBias;
                    }
                    else
                    {
                        existing.AddComponent(newLight);
                    }
                }
                Console.WriteLine($"[ClientGameServerProxy] Updated existing entity {entity.Id} (prevented duplicate from editor sync)");
                return;
            }
            bool isDuplicate = _entities.Any(e => e.Id == entity.Id && entity.Id > 0);
            if (entity.Id <= 0 || isDuplicate)
            {
                entity.Id = _nextEntityId++;
            }
            else
            {
                _nextEntityId = Math.Max(_nextEntityId, entity.Id + 1);
            }
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics != null) physics.Rotation = Entity.SanitizeRotation(physics.Rotation);
            _entities.Add(entity);
            _eventBus.Publish(new EntityAddedEvent(entity), true);
        }
        public void RemoveEntity(int id)
        {
            var entity = _entities.Find(e => e.Id == id);
            if (entity != null)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null) _physicsWorld.UnregisterBody(physics);
                _entities.Remove(entity);
                _eventBus.Publish(new EntityRemovedEvent(id), true);
            }
        }
        public void ClearEntities()
        {
            var idsToRemove = _entities.Select(e => e.Id).ToList();
            foreach (var id in idsToRemove)
            {
                _eventBus.Publish(new EntityRemovedEvent(id), true);
            }
            _physicsWorld.ClearBodies();
            _entities.Clear();
        }
        public IReadOnlyList<Entity> GetEntities()
        {
            return _entities.AsReadOnly();
        }
        public Entity GetEntityById(int id)
        {
            return _entities.Find(e => e.Id == id);
        }
        public void AddSystem(GameSystem system)
        {
            if (system != null && !_systems.Contains(system))
                _systems.Add(system);
        }
        public T GetSystem<T>() where T : GameSystem
        {
            return _systems.OfType<T>().FirstOrDefault();
        }
        public void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null) _physicsWorld.RegisterBody(physics);
            }
            _physicsWorld.Step(deltaTime);
            foreach (var system in _systems)
                system.Update(deltaTime);
        }
        public bool ValidateAndUpdateMovement(int entityId, Vector2 requestedPosition, Quaternion requestedRotation, ulong steamId)
        {
            _eventBus.Publish(new MovementRequestEvent(entityId, requestedPosition, requestedRotation, steamId), true);
            return true;
        }
        public bool ValidateInventory(int entityId, string action, object data)
        {
            return true;
        }
        public void Publish<T>(T eventData, bool networkSync = false) where T : class
        {
            _eventBus.Publish(eventData, networkSync);
        }
        /// <summary>
        /// Local client-side ray-trace against the same PhysicsComponent shapes the server uses.
        /// Enables continuous occlusion / wall muffling in pure-client Play mode.
        /// </summary>
        public RayTraceResult RequestRayTrace(Vector3 start, Vector3 direction, float maxDistance)
        {
            RayTraceResult result = new RayTraceResult { DidHit = false };
            float closestDistance = float.MaxValue;
            PhysicsComponent hitPhysics = null;
            if (direction.LengthSquared() < 1e-8f)
                return result;
            Vector3 dir = Vector3.Normalize(direction);
            for (int i = 0; i < _entities.Count; i++)
            {
                var entity = _entities[i];
                if (entity == null) continue;
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics == null || !physics.CollisionEnabled) continue;
                if (physics.RayIntersects(start, dir, out float distance, out Vector3 hitPoint) &&
                    distance < closestDistance && distance <= maxDistance && distance > 0.001f)
                {
                    closestDistance = distance;
                    hitPhysics = physics;
                    result.HitPoint = hitPoint;
                }
            }
            if (hitPhysics != null)
            {
                result.DidHit = true;
                result.Distance = closestDistance;
                result.HitNormal = ApproximateNormal(result.HitPoint, hitPhysics);
                float volume = hitPhysics.Size.X * hitPhysics.Size.Y * hitPhysics.Size.Z;
                result.Material = new MaterialProperties
                {
                    Density = volume > 1e-6f ? hitPhysics.Mass / volume : 1.0f
                };
            }
            return result;
        }
        private static Vector3 ApproximateNormal(Vector3 hitPoint, PhysicsComponent physics)
        {
            Vector3 center = physics.Position;
            Vector3 halfSize = physics.Size * 0.5f;
            Vector3 localHit = hitPoint - center;
            Vector3 absLocalHit = new Vector3(Math.Abs(localHit.X), Math.Abs(localHit.Y), Math.Abs(localHit.Z));
            Vector3 faceDistances = halfSize - absLocalHit;
            if (faceDistances.X < faceDistances.Y && faceDistances.X < faceDistances.Z)
                return new Vector3(localHit.X > 0 ? 1 : -1, 0, 0);
            else if (faceDistances.Y < faceDistances.Z)
                return new Vector3(0, localHit.Y > 0 ? 1 : -1, 0);
            else
                return new Vector3(0, 0, localHit.Z > 0 ? 1 : -1);
        }
        public void QueueNetworkEvent(IEvent e)
        {
        }
    }
}