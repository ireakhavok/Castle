// Folder: SiegeEngine/Core/Definitions
// File: Entity.cs
using SiegeEngine.Core.AssetParsing;
using System;
using System.Collections.Generic;

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
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }
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
    }
}