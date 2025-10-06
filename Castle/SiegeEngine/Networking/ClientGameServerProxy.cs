// Folder: SiegeEngine.Networking
// File: ClientGameServerProxy.cs
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.Systems;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Networking
{
    public class ClientGameServerProxy : IGameServer
    {
        private readonly EventBus _eventBus;
        private readonly List<Entity> _entities = new List<Entity>();

        public ClientGameServerProxy(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void AddEntity(Entity entity)
        {
            _entities.Add(entity);
            _eventBus.Publish(new EntityAddedEvent(entity), true);
        }

        public void RemoveEntity(int id)
        {
            var entity = _entities.Find(e => e.Id == id);
            if (entity != null)
            {
                _entities.Remove(entity);
                _eventBus.Publish(new EntityRemovedEvent(id), true);
            }
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
            // For client, systems are local, but not implemented here
        }

        public void Update(float deltaTime)
        {
            // Client update logic if needed
        }

        public bool ValidateAndUpdateMovement(int entityId, Vector2 requestedPosition, Quaternion requestedRotation, ulong steamId)
        {
            // Client sends request
            _eventBus.Publish(new MovementRequestEvent(entityId, requestedPosition, requestedRotation, steamId), true);
            return true; // Assume accepted locally
        }

        public bool ValidateInventory(int entityId, string action, object data)
        {
            // Similar
            return true;
        }

        public void Publish<T>(T eventData, bool networkSync = false) where T : class
        {
            _eventBus.Publish(eventData, networkSync);
        }

        public RayTraceResult RequestRayTrace(Vector3 start, Vector3 direction, float maxDistance)
        {
            // Simulate or send request
            return new RayTraceResult { DidHit = false };
        }

        public void QueueNetworkEvent(IEvent e)
        {
            // Queue for send
        }
    }
}