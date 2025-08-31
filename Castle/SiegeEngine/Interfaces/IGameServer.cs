using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Systems;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Interfaces
{
    public interface IGameServer
    {
        void AddEntity(Entity entity);
        void RemoveEntity(int id);
        IReadOnlyList<Entity> GetEntities();
        Entity GetEntityById(int id);
        void AddSystem(GameSystem system);
        void Update(float deltaTime);
        bool ValidateAndUpdateMovement(int entityId, Vector2 requestedPosition, Quaternion requestedRotation, ulong steamId);
        bool ValidateInventory(int entityId, string action, object data);
        void Publish<T>(T eventData, bool networkSync = false) where T : class;
        RayTraceResult RequestRayTrace(Vector3 start, Vector3 direction, float maxDistance);
    }
}