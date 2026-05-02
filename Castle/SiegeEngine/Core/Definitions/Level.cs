// Folder: SiegeEngine/Core/Definitions
// File: Level.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using SiegeEngine.Core.Events;

namespace SiegeEngine.Core.Definitions
{
    public class Level : IDisposable
    {
        public string Name { get; set; } = "Untitled";
        public List<Entity> Entities { get; } = new List<Entity>();
        public TerrainData Terrain { get; set; } = new TerrainData();
        public EnvironmentSettings Environment { get; set; } = new EnvironmentSettings();
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

            if (entity.Id <= 0)
            {
                entity.Id = _nextEntityId++;
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
            var entity = new Entity { Id = 0, Type = type };
            var physics = new PhysicsComponent();
            physics.Position = position;
            physics.Rotation = rotation;
            if (scale != default) physics.Scale = scale;
            entity.AddComponent(physics);

            AddEntity(entity);
            return entity;
        }

        public byte[] Serialize()
        {
            var dto = new LevelDto { Name = Name, Terrain = Terrain, Environment = Environment, Entities = Entities.ConvertAll(e => e.ToData()), CustomData = CustomData };
            return JsonSerializer.SerializeToUtf8Bytes(dto, new JsonSerializerOptions { WriteIndented = true });
        }

        public static Level Deserialize(byte[] data, EventBus eventBus = null)
        {
            if (data == null || data.Length == 0) return new Level(eventBus);
            var dto = JsonSerializer.Deserialize<LevelDto>(data);
            var level = new Level(eventBus) { Name = dto?.Name ?? "Untitled", Terrain = dto?.Terrain ?? new TerrainData(), Environment = dto?.Environment ?? new EnvironmentSettings() };

            if (dto?.Entities != null)
            {
                foreach (var ed in dto.Entities)
                {
                    level.AddEntity(Entity.FromData(ed)); // Use AddEntity for consistent ID assignment and logging
                }
            }
            if (dto?.CustomData != null)
            {
                foreach (var kv in dto.CustomData) level.CustomData[kv.Key] = kv.Value;
            }
            return level;
        }

        public void Dispose() { }

        private class LevelDto
        {
            public string Name { get; set; }
            public TerrainData Terrain { get; set; }
            public EnvironmentSettings Environment { get; set; }
            public List<EntityData> Entities { get; set; }
            public Dictionary<string, object> CustomData { get; set; }
        }
    }
}