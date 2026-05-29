// Folder: SiegeEngine/Core/Networking
// File: ClientGameServerProxy.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
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
        private int _nextEntityId = 1;
        public ClientGameServerProxy(EventBus eventBus)
        {
            _eventBus = eventBus;
        }
        public void AddEntity(Entity entity)
        {
            if (entity == null) return;

            // FIXED: robust ID assignment + duplicate guard (matches GameServer exactly)
            // This prevents the incremental sync from ever adding the same entity twice
            bool isDuplicate = _entities.Any(e => e.Id == entity.Id && entity.Id > 0);
            if (entity.Id <= 0 || isDuplicate)
            {
                entity.Id = _nextEntityId++;
            }
            else
            {
                _nextEntityId = Math.Max(_nextEntityId, entity.Id + 1);
            }
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
        /// <summary>
        /// Editor-only helper (safe to call in editor context). Used during project reload
        /// to reset runtime entities before re-adding restored ones from Level.
        /// </summary>
        public void ClearEntities()
        {
            _entities.Clear();
            // NOTE: _nextEntityId is deliberately NOT reset here - it persists across syncs so new entities get high IDs
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