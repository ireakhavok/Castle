using SiegeEngine.AssetParsing;
using System;
using System.Collections.Generic;

namespace SiegeEngine.Definitions
{
    public interface IComponent { }

    public class Entity
    {
        private readonly Dictionary<Type, IComponent> _components = new();

        public int Id { get; set; }
        public string Type { get; set; } = "Default";
        public IReadOnlyDictionary<Type, IComponent> Components => _components;

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
    }
}