// Folder: SiegeEngine.Core.Definitions
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

        public Level(EventBus eventBus = null)
        {
            _eventBus = eventBus;
        }

        public void AddEntity(Entity entity)
        {
            if (entity == null) return;
            Entities.Add(entity);
            _eventBus?.Publish(new EntityAddedEvent(entity));
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
            var entity = new Entity { Id = Entities.Count + 1, Type = type };
            entity.Transform.Position = position;
            entity.Transform.Rotation = rotation;
            if (scale != default) entity.Transform.Scale = scale;
            AddEntity(entity);
            return entity;
        }

        // Called automatically when anything places an entity in the editor
        public void OnEntityPlaced(EntityPlacedEvent e)
        {
            var entity = new Entity { Id = e.EntityId, Type = e.Type ?? "Default" };
            entity.Transform.Position = e.Position;
            if (e.Rotation != default) entity.Transform.Rotation = e.Rotation;
            AddEntity(entity);
        }

        public byte[] Serialize()
        {
            var dto = new LevelDto
            {
                Name = Name,
                Terrain = Terrain,
                Environment = Environment,
                Entities = Entities.ConvertAll(e => e.ToData()),
                CustomData = CustomData
            };
            return JsonSerializer.SerializeToUtf8Bytes(dto, new JsonSerializerOptions { WriteIndented = true });
        }

        public static Level Deserialize(byte[] data, EventBus eventBus = null)
        {
            if (data == null || data.Length == 0) return new Level(eventBus);

            var dto = JsonSerializer.Deserialize<LevelDto>(data);
            var level = new Level(eventBus)
            {
                Name = dto?.Name ?? "Untitled",
                Terrain = dto?.Terrain ?? new TerrainData(),
                Environment = dto?.Environment ?? new EnvironmentSettings()
            };

            if (dto?.Entities != null)
            {
                foreach (var ed in dto.Entities)
                    level.Entities.Add(Entity.FromData(ed));
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
            public List<EntityData> Entities { get; set; }
            public Dictionary<string, object> CustomData { get; set; }
        }
    }
}