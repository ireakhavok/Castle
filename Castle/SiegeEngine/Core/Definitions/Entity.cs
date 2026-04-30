using SiegeEngine.Core.AssetParsing;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

namespace SiegeEngine.Core.Definitions
{
    public interface IComponent { }

    public class Entity
    {
        private readonly Dictionary<Type, IComponent> _components = new();
        public int Id { get; set; }
        public string Type { get; set; } = "Default";
        public TransformComponent Transform { get; } = new TransformComponent();
        public IReadOnlyDictionary<Type, IComponent> Components => _components;

        public Entity()
        {
            AddComponent(Transform);
        }

        public void AddComponent<T>(T component) where T : IComponent
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            _components[typeof(T)] = component;
        }

        public T GetComponent<T>() where T : IComponent
        {
            return _components.TryGetValue(typeof(T), out var component) ? (T)component : default;
        }

        public bool RemoveComponent<T>() where T : IComponent
        {
            return _components.Remove(typeof(T));
        }

        public void SetParent(Entity parent)
        {
            Transform.SetParent(parent?.Transform);
        }

        public void AddChild(Entity child)
        {
            Transform.AddChild(child?.Transform);
        }

        public EntityData ToData()
        {
            var data = new EntityData
            {
                Type = Type,
                Position = Transform.Position,
                Rotation = Transform.Rotation,
                Scale = Transform.Scale
            };

            var modelComp = GetComponent<ModelComponent>();
            if (modelComp != null)
            {
                data.AssetPackKey = modelComp.Key;
            }

            return data;
        }

        public static Entity FromData(EntityData data)
        {
            if (data == null) return new Entity();
            var entity = new Entity { Id = 0, Type = data.Type ?? "Default" };
            entity.Transform.Position = data.Position;
            entity.Transform.Rotation = data.Rotation;
            entity.Transform.Scale = data.Scale != default ? data.Scale : Vector3.One;

            if (!string.IsNullOrEmpty(data.AssetPackKey))
            {
                var modelComp = new ModelComponent { Key = data.AssetPackKey };
                entity.AddComponent(modelComp);
            }

            return entity;
        }
    }
}