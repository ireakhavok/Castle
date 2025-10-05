using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
namespace SiegeEngine.Interfaces
{
    public class ClientGameServerProxy : IGameServer
    {
        private readonly EventBus _eventBus;
        private readonly List<Entity> _localEntities = new List<Entity>(); // Client-side cache
        public ClientGameServerProxy(EventBus eventBus)
        {
            _eventBus = eventBus;
            // Subscribe to networked events for state sync
            _eventBus.Subscribe<EntityPlacedEvent>(OnEntityPlaced);
            _eventBus.Subscribe<EntityMovedEvent>(OnEntityMoved);
            // Add more subscriptions as needed (e.g., RemoveEntityEvent)
        }
        public void AddEntity(Entity entity)
        {
            _localEntities.Add(entity);
            _eventBus.Publish(new EntityPlacedEvent(entity.Id, entity.Type, entity.GetComponent<PhysicsComponent>().Position), true);
            Console.WriteLine($"ClientProxy: Requested add entity {entity.Id} (networked)");
        }
        public void RemoveEntity(int id)
        {
            var entity = _localEntities.Find(e => e.Id == id);
            if (entity != null)
            {
                _localEntities.Remove(entity);
                // Publish remove event if defined
                Console.WriteLine($"ClientProxy: Requested remove entity {id} (networked)");
            }
        }
        public IReadOnlyList<Entity> GetEntities() => _localEntities.AsReadOnly();
        public Entity GetEntityById(int id) => _localEntities.Find(e => e.Id == id);
        public void AddSystem(GameSystem system)
        {
            // Client-side systems only; server systems not added here
            Console.WriteLine($"ClientProxy: Added client-side system {system.GetType().Name}");
        }
        public void Update(float deltaTime)
        {
            // Client-side update; server ticks independently
        }
        public bool ValidateAndUpdateMovement(int entityId, Vector2 requestedPosition, Quaternion requestedRotation, ulong steamId)
        {
            // Client prediction; actual validation on server
            _eventBus.Publish(new EntityMovedEvent(entityId, requestedPosition, requestedRotation), true);
            return true; // Assume success; server will correct if invalid
        }
        public bool ValidateInventory(int entityId, string action, object data)
        {
            // Send to server; assume temp success
            return true;
        }
        public void Publish<T>(T eventData, bool networkSync = false) where T : class
        {
            _eventBus.Publish(eventData, networkSync);
        }
        public RayTraceResult RequestRayTrace(Vector3 start, Vector3 direction, float maxDistance)
        {
            // Placeholder; send request event if needed
            return new RayTraceResult { DidHit = false };
        }
        public void QueueNetworkEvent(IEvent e)
        {
            // Client doesn't queue; publishes directly
            if (e is IEvent eventData)
                _eventBus.Publish(eventData, true);
        }
        private void OnEntityPlaced(EntityPlacedEvent e)
        {
            var entity = new Entity { Id = e.EntityId, Type = e.EntityType };
            entity.AddComponent(new PhysicsComponent { Position = e.Position });
            _localEntities.Add(entity);
        }
        private void OnEntityMoved(EntityMovedEvent e)
        {
            var entity = GetEntityById(e.EntityId);
            if (entity != null)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null)
                {
                    physics.Position = new Vector3(e.Position.X, e.Position.Y, physics.Position.Z);
                    physics.Rotation = e.Rotation;
                }
            }
        }
    }
}