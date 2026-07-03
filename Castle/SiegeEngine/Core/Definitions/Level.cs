// Folder: SiegeEngine/Core/Definitions
// File: Level.cs
using SiegeEngine.Core.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace SiegeEngine.Core.Definitions
{
    public class Level : IDisposable
    {
        public string Name { get; set; } = "Untitled";
        public List<Entity> Entities { get; } = new List<Entity>();
        public TerrainData Terrain { get; set; } = new TerrainData();
        public EnvironmentSettings Environment { get; set; } = new EnvironmentSettings();
        public SkyboxData Skybox { get; set; } = new SkyboxData();
        public Dictionary<string, object> CustomData { get; } = new Dictionary<string, object>();

        private readonly EventBus _eventBus;
        private int _nextEntityId = 1;

        public Level(EventBus eventBus = null)
        {
            _eventBus = eventBus;
        }

        public void AddEntity(Entity entity)
        {
            if (entity == null) return;

            // FIXED: always ensure _nextEntityId is one past the highest existing ID
            // This guarantees new placements after load get truly unique IDs
            if (Entities.Count > 0)
            {
                int maxId = Entities.Max(e => e.Id);
                _nextEntityId = Math.Max(_nextEntityId, maxId + 1);
            }

            bool isDuplicate = Entities.Any(e => e.Id == entity.Id && entity.Id > 0);
            if (entity.Id <= 0 || isDuplicate)
            {
                entity.Id = _nextEntityId++;
            }
            else
            {
                _nextEntityId = Math.Max(_nextEntityId, entity.Id + 1);
            }

            Entities.Add(entity);
            _eventBus?.Publish(new EntityAddedEvent(entity));

            Console.WriteLine($"[Level.AddEntity] Added entity ID={entity.Id} Type='{entity.Type}' Position={entity.GetComponent<PhysicsComponent>()?.Position}");
        }

        public void RemoveEntity(int id)
        {
            var entity = Entities.Find(e => e.Id == id);
            if (entity != null)
            {
                Entities.Remove(entity);
                _eventBus?.Publish(new EntityRemovedEvent(id));
            }
        }

        public Entity PlaceEntity(Vector3 position, string type = "Default", Quaternion rotation = default, Vector3 scale = default)
        {
            var entity = new Entity { Id = 0, Type = type };   // force ID=0 so AddEntity always assigns fresh ID
            var physics = new PhysicsComponent();
            physics.Position = position;
            physics.Rotation = rotation;
            if (scale != default) physics.Scale = scale;
            entity.AddComponent(physics);

            var modelComp = entity.GetComponent<ModelComponent>();
            if (modelComp?.Model != null)
            {
                physics.Size = modelComp.Model.GetBoundingSize();
                physics.LocalBoundsMinCm = modelComp.Model.LocalBoundsMinCm;
                physics.LocalBoundsMaxCm = modelComp.Model.LocalBoundsMaxCm;
            }

            AddEntity(entity);
            return entity;
        }

        public byte[] Serialize()
        {
            var dto = new LevelDto
            {
                Name = Name,
                Terrain = Terrain,
                Environment = Environment,
                Skybox = Skybox,
                Entities = Entities.ConvertAll(e => e.ToData()),
                CustomData = CustomData
            };
            return JsonSerializer.SerializeToUtf8Bytes(dto, EntityData.SerializerOptions);
        }

        public static Level Deserialize(byte[] data, EventBus eventBus = null)
        {
            if (data == null || data.Length == 0) return new Level(eventBus);

            var dto = JsonSerializer.Deserialize<LevelDto>(data, EntityData.SerializerOptions);
            var level = new Level(eventBus)
            {
                Name = dto?.Name ?? "Untitled",
                Terrain = dto?.Terrain ?? new TerrainData(),
                Environment = dto?.Environment ?? new EnvironmentSettings(),
                Skybox = dto?.Skybox ?? new SkyboxData()
            };

            if (dto?.Entities != null)
            {
                foreach (var ed in dto.Entities)
                {
                    level.AddEntity(Entity.FromData(ed));
                }
            }

            if (dto?.CustomData != null)
            {
                foreach (var kv in dto.CustomData)
                    level.CustomData[kv.Key] = kv.Value;
            }

            return level;
        }

        public void Dispose() { }

        private class LevelDto
        {
            public string Name { get; set; }
            public TerrainData Terrain { get; set; }
            public EnvironmentSettings Environment { get; set; }
            public SkyboxData Skybox { get; set; }
            public List<EntityData> Entities { get; set; }
            public Dictionary<string, object> CustomData { get; set; }
        }
    }
}