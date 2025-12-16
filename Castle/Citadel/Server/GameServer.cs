using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Citadel.Network;
using Citadel.Systems;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Systems;
using SiegeEngine.ContextManagement;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Definitions;

namespace Citadel.Server
{
    public class GameServer : IGameServer
    {
        private readonly List<Entity> _entities = new List<Entity>();
        private readonly List<GameSystem> _systems = new List<GameSystem>();
        private readonly EventBus _eventBus;
        private readonly float _maxSpeed = 20.0f;
        private readonly float _maxDistance = 20.0f;
        private readonly ServerValidationSystem _validationSystem;
        private readonly NetworkManager _networkManager;
        private readonly EntityDeltaTracker _deltaTracker = new();
        private readonly Dictionary<(int, int), List<Entity>> _spatialGrid = new();
        private const float GridCellSize = 10f;
        private readonly Queue<IEvent> _networkEventQueue = new Queue<IEvent>();
        public GameServer(EventBus eventBus, NetworkManager networkManager = null)
        {
            _eventBus = eventBus;
            _networkManager = networkManager;
            _validationSystem = new ServerValidationSystem(this);
            AddSystem(new PhysicsSystem(this));
            AddSystem(_validationSystem);
            AddSystem(new AudioSystem(this, _eventBus, true, _validationSystem));
            _eventBus.Subscribe<ExitEditorEvent>(OnExitEditor);
            _eventBus.Subscribe<EntityPlacedEvent>(OnEntityPlaced);
            _eventBus.Subscribe<ItemPickedUpEvent>(OnItemPickedUp);
            _eventBus.Subscribe<PhysicsCollisionEvent>(OnPhysicsCollision);
            _eventBus.Subscribe<PlayerExitedEditorEvent>(OnPlayerExitedEditor);
            _eventBus.Subscribe<MouseInputEvent>(OnMouseInput);
            _eventBus.Subscribe<KeyInputEvent>(OnKeyInput);
        }
        public void AddEntity(Entity entity)
        {
            _entities.Add(entity);
            UpdateSpatialGrid(entity);
            Console.WriteLine($"GameServer: Added entity {entity.Id} of type {entity.Type}");
        }
        public void RemoveEntity(int id)
        {
            var entity = _entities.Find(e => e.Id == id);
            if (entity != null)
            {
                _entities.Remove(entity);
                RemoveFromSpatialGrid(entity);
                Console.WriteLine($"GameServer: Removed entity {id}");
            }
        }
        public IReadOnlyList<Entity> GetEntities() => _entities.AsReadOnly();
        public Entity GetEntityById(int id) => _entities.Find(e => e.Id == id);
        public void AddSystem(GameSystem system)
        {
            _systems.Add(system);
            Console.WriteLine($"GameServer: Added system {system.GetType().Name}");
        }
        public void Update(float deltaTime)
        {
            while (_networkEventQueue.Count > 0)
            {
                var e = _networkEventQueue.Dequeue();
                _eventBus.Publish(e, false);
            }
            foreach (var system in _systems)
            {
                system.Update(deltaTime);
            }
            if (_validationSystem.IsAuthoritativeMode())
            {
                foreach (var source in _entities)
                {
                    var sourcePhysics = source.GetComponent<PhysicsComponent>();
                    var sourcePlayer = source.GetComponent<Player>();
                    if (sourcePhysics != null && sourcePlayer != null && sourcePlayer.Camera != null)
                    {
                        Vector3 rayStart = sourcePhysics.Position;
                        float yawRad = sourcePlayer.Camera.Yaw * (float)(Math.PI / 180);
                        Vector3 viewDir = Vector3.Normalize(new Vector3((float)Math.Sin(yawRad), (float)Math.Cos(yawRad), 0));
                        var nearbyEntities = GetNearbyEntities(rayStart);
                        foreach (var target in nearbyEntities)
                        {
                            if (target.Id == source.Id) continue;
                            var targetPhysics = target.GetComponent<PhysicsComponent>();
                            if (targetPhysics != null)
                            {
                                Vector3 toTarget = targetPhysics.Position - rayStart;
                                float distance = toTarget.Length();
                                if (distance < 0.1f) continue;
                                Vector3 toTargetNorm = toTarget / distance;
                                float dotProduct = Math.Clamp(Vector3.Dot(toTargetNorm, viewDir), -1.0f, 1.0f);
                                float hAngle = (float)Math.Acos(dotProduct) * (180f / (float)Math.PI);
                                float zComponent = Math.Clamp(toTargetNorm.Z, -1.0f, 1.0f);
                                float vAngle = (float)Math.Asin(zComponent) * (180f / (float)Math.PI);
                                bool inFrustum = hAngle <= 60f && Math.Abs(vAngle) <= 45f;
                                if (inFrustum)
                                {
                                    bool occluded = CheckOcclusion(rayStart, targetPhysics.Position);
                                    targetPhysics.IsVisible = !occluded;
                                    if (!occluded)
                                    {
                                        Console.WriteLine($"GameServer: Entity {target.Id} visible at {targetPhysics.Position}, Distance: {distance:F2} units");
                                    }
                                }
                                else
                                {
                                    targetPhysics.IsVisible = false;
                                }
                            }
                        }
                    }
                }
            }
            _deltaTracker.Update(GetEntities());
        }
        public bool ValidateAndUpdateMovement(int entityId, Vector2 requestedPosition, Quaternion requestedRotation, ulong steamId)
        {
            bool validated = _validationSystem.ValidateMovement(entityId, requestedPosition, requestedRotation, steamId);
            Console.WriteLine($"GameServer: Movement validation for entity {entityId} (SteamID: {steamId}) to {requestedPosition}, Rotation={requestedRotation} - {(validated ? "Success" : "Failed")}");
            return validated;
        }
        public bool ValidateInventory(int entityId, string action, object data)
        {
            return _validationSystem.ValidateInventory(entityId, action, data);
        }
        public void Publish<T>(T eventData, bool networkSync = false) where T : class
        {
            _eventBus.Publish(eventData, networkSync);
            if (networkSync && _networkManager != null && eventData is IEvent ievent)
            {
                _networkManager.SendToAll(ievent.Serialize(), eventData is EntityMovedEvent ? 1 : 0);
            }
        }
        public byte[] Serialize()
        {
            var deltas = _deltaTracker.GetDeltas(GetEntities());
            var visibleDeltas = new Dictionary<int, Vector3>();
            foreach (var entity in GetEntities())
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null && physics.IsVisible)
                {
                    visibleDeltas[entity.Id] = physics.Position;
                }
            }
            return JsonSerializer.SerializeToUtf8Bytes(new { Deltas = visibleDeltas });
        }
        public void Deserialize(byte[] data)
        {
            var state = JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, Vector3>>>(data);
            if (state != null && state.TryGetValue("Deltas", out var deltas))
            {
                foreach (var kvp in deltas)
                {
                    var entity = GetEntityById(kvp.Key);
                    if (entity != null)
                    {
                        var physics = entity.GetComponent<PhysicsComponent>();
                        if (physics != null) physics.Position = kvp.Value;
                    }
                }
            }
        }
        public RayTraceResult RequestRayTrace(Vector3 start, Vector3 direction, float maxDistance)
        {
            RayTraceResult result = new RayTraceResult { DidHit = false };
            float closestDistance = float.MaxValue;
            Entity hitEntity = null;
            foreach (var entity in GetNearbyEntities(start))
            {
                if (entity.GetComponent<PhysicsComponent>() != null)
                {
                    var physics = entity.GetComponent<PhysicsComponent>();
                    Vector3 min = physics.Position - physics.Size / 2;
                    Vector3 max = physics.Position + physics.Size / 2;
                    if (RayAABBIntersect(start, direction, min, max, out float distance))
                    {
                        if (distance < closestDistance && distance <= maxDistance)
                        {
                            closestDistance = distance;
                            hitEntity = entity;
                        }
                    }
                }
            }
            if (hitEntity != null)
            {
                result.DidHit = true;
                result.Distance = closestDistance;
                result.HitPoint = start + closestDistance * direction;
                result.HitNormal = ApproximateNormal(result.HitPoint, hitEntity.GetComponent<PhysicsComponent>());
                var physics = hitEntity.GetComponent<PhysicsComponent>();
                float volume = physics.Size.X * physics.Size.Y * physics.Size.Z;
                result.Material = new MaterialProperties { Density = volume > 0 ? physics.Mass / volume : 0 };
                Console.WriteLine($"GameServer: Raycast hit entity {hitEntity.Id} at {result.HitPoint}, Distance: {result.Distance}, Density: {result.Material.Density}");
            }
            return result;
        }
        private bool RayAABBIntersect(Vector3 rayOrigin, Vector3 rayDirection, Vector3 boxMin, Vector3 boxMax, out float distance)
        {
            distance = 0f;
            float tmin = float.MinValue;
            float tmax = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                if (Math.Abs(rayDirection[i]) < 1e-6)
                {
                    if (rayOrigin[i] < boxMin[i] || rayOrigin[i] > boxMax[i])
                        return false;
                }
                else
                {
                    float ood = 1.0f / rayDirection[i];
                    float t1 = (boxMin[i] - rayOrigin[i]) * ood;
                    float t2 = (boxMax[i] - rayOrigin[i]) * ood;
                    if (t1 > t2) (t1, t2) = (t2, t1);
                    tmin = Math.Max(tmin, t1);
                    tmax = Math.Min(tmax, t2);
                    if (tmin > tmax) return false;
                }
            }
            distance = tmin >= 0 ? tmin : 0;
            return true;
        }
        private Vector3 ApproximateNormal(Vector3 hitPoint, PhysicsComponent physics)
        {
            Vector3 center = physics.Position;
            Vector3 halfSize = physics.Size / 2;
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
        private void OnExitEditor(ExitEditorEvent e)
        {
            Console.WriteLine($"GameServer: Player {e.PlayerId} exited editor mode");
            Publish(new PlayerExitedEditorEvent(e.PlayerId), true);
        }
        private void OnEntityPlaced(EntityPlacedEvent e)
        {
            var entity = new Entity { Id = e.EntityId, Type = e.EntityType };
            entity.AddComponent(new PhysicsComponent { Position = e.Position });
            if (e.EntityType == "Player")
            {
                entity.AddComponent(new Player(e.EntityId, e.Position, e.PlayerId ?? 0));
            }
            AddEntity(entity);
            Publish(e, true);
        }
        private void OnItemPickedUp(ItemPickedUpEvent e)
        {
            var entity = GetEntityById(e.EntityId);
            if (entity != null && _validationSystem.ValidateInventory(e.EntityId, "AddItem", e.ItemId))
            {
                Console.WriteLine($"GameServer: Entity {e.EntityId} picked up item {e.ItemId}");
                Publish(e, true);
            }
            else
            {
                Console.WriteLine($"GameServer: Item pickup failed for entity {e.EntityId}");
            }
        }
        private void OnPhysicsCollision(PhysicsCollisionEvent e)
        {
            var source = GetEntityById(e.SourceId);
            var target = GetEntityById(e.TargetId);
            if (source != null && target != null && _validationSystem.ValidateCombat(e.SourceId, e.TargetId, e.Force.Length()))
            {
                Console.WriteLine($"GameServer: Collision from {e.SourceId} to {e.TargetId} with force {e.Force}");
                Publish(e, true);
            }
            else
            {
                Console.WriteLine($"GameServer: Collision failed for {e.SourceId} to {e.TargetId}");
            }
        }
        private void OnPlayerExitedEditor(PlayerExitedEditorEvent e)
        {
            Console.WriteLine($"GameServer: Player {e.PlayerId} confirmed editor exit");
        }
        private void OnMouseInput(MouseInputEvent e)
        {
            Console.WriteLine($"GameServer: Received MouseInputEvent from SteamID: {e.SteamId}, Pos: {e.Position}, Button: {e.Button}, Action: {e.Action}");
            if (_validationSystem.ValidateMouseInput(e.SteamId, e.Position, e.Button, e.Action))
            {
                if (e.Button != (MouseButton)(-1) && e.Action != (InputAction)(-1))
                {
                    var entity = _entities.Find(en => en.GetComponent<Player>()?.SteamId == e.SteamId);
                    if (entity != null)
                    {
                        Publish(e, true);
                    }
                }
            }
            else
            {
                Console.WriteLine($"GameServer: Invalid mouse input from SteamID: {e.SteamId}");
            }
        }
        private void OnKeyInput(KeyInputEvent e)
        {
            Console.WriteLine($"GameServer: Received KeyInputEvent from SteamID: {e.SteamId}, Key: {e.Key}, Action: {e.Action}");
            if (_validationSystem.ValidateKeyInput(e.SteamId, e.Key, e.Action))
            {
                var entity = _entities.Find(en => en.GetComponent<Player>()?.SteamId == e.SteamId);
                if (entity != null)
                {
                    Publish(e, true);
                }
            }
            else
            {
                Console.WriteLine($"GameServer: Invalid key input from SteamID: {e.SteamId}");
            }
        }
        private bool CheckOcclusion(Vector3 start, Vector3 end)
        {
            var nearbyEntities = GetNearbyEntities(start);
            foreach (var entity in nearbyEntities)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null && physics.Position != start && physics.Position != end)
                {
                    Vector3 toEntity = physics.Position - start;
                    float distToEntity = toEntity.Length();
                    if (distToEntity < 0.1f) continue;
                    Vector3 rayDir = Vector3.Normalize(end - start);
                    float projDist = Vector3.Dot(toEntity, rayDir);
                    if (projDist > 0 && projDist < (end - start).Length())
                    {
                        Vector3 projPoint = start + rayDir * projDist;
                        if ((projPoint - physics.Position).Length() < physics.Size.X / 2)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private void UpdateSpatialGrid(Entity entity)
        {
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics != null)
            {
                var cell = GetGridCell(physics.Position);
                if (!_spatialGrid.ContainsKey(cell))
                {
                    _spatialGrid[cell] = new List<Entity>();
                }
                _spatialGrid[cell].Add(entity);
            }
        }
        private void RemoveFromSpatialGrid(Entity entity)
        {
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics != null)
            {
                var cell = GetGridCell(physics.Position);
                if (_spatialGrid.ContainsKey(cell))
                {
                    _spatialGrid[cell].Remove(entity);
                    if (_spatialGrid[cell].Count == 0)
                    {
                        _spatialGrid.Remove(cell);
                    }
                }
            }
        }
        private (int, int) GetGridCell(Vector3 position)
        {
            return ((int)(position.X / GridCellSize), (int)(position.Y / GridCellSize));
        }
        private IEnumerable<Entity> GetNearbyEntities(Vector3 position)
        {
            var (cx, cy) = GetGridCell(position);
            var nearby = new List<Entity>();
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                for (int y = cy - 1; y <= cy + 1; y++)
                {
                    if (_spatialGrid.TryGetValue((x, y), out var entities))
                    {
                        nearby.AddRange(entities);
                    }
                }
            }
            return nearby;
        }
        public void QueueNetworkEvent(IEvent e)
        {
            _networkEventQueue.Enqueue(e);
        }
    }
}