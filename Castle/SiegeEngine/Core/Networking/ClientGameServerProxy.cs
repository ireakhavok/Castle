// Folder: SiegeEngine/Core/Networking
// File: ClientGameServerProxy.cs
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
                    existingPhysics.IsSleeping = newPhysics.IsSleeping;
                    existingPhysics.IslandId = newPhysics.IslandId;
                    existingPhysics.SleepThreshold = newPhysics.SleepThreshold;
                    existingPhysics.SleepTimer = newPhysics.SleepTimer;
                    existingPhysics.Mass = newPhysics.Mass;
                    existingPhysics.Health = newPhysics.Health;
                    existingPhysics.IsBreakable = newPhysics.IsBreakable;
                    existingPhysics.IsVisible = newPhysics.IsVisible;

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
            if (physics != null)
                physics.Rotation = Entity.SanitizeRotation(physics.Rotation);

            _entities.Add(entity);
            _eventBus.Publish(new EntityAddedEvent(entity), true);
        }

        public void RemoveEntity(int id)
        {
            var entity = _entities.Find(e => e.Id == id);
            if (entity != null)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null)
                    _physicsWorld.UnregisterBody(physics);
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

        public void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null)
                    _physicsWorld.RegisterBody(physics);
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

        public RayTraceResult RequestRayTrace(Vector3 start, Vector3 direction, float maxDistance)
        {
            return new RayTraceResult { DidHit = false };
        }

        public void QueueNetworkEvent(IEvent e) { }
    }
}