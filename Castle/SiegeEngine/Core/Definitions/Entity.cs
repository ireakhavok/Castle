// Folder: SiegeEngine/Core/Definitions
// File: Entity.cs
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.AssetParsing.Model;
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
                Id = Id
            };

            // PhysicsComponent is the definitive runtime/editor source of truth
            var physics = GetComponent<PhysicsComponent>();
            if (physics != null)
            {
                data.Position = physics.Position;
                data.Rotation = physics.Rotation;
                data.Scale = physics.Scale;
            }
            else
            {
                data.Position = Transform.Position;
                data.Rotation = Transform.Rotation;
                data.Scale = Transform.Scale;
            }

            var modelComp = GetComponent<ModelComponent>();
            if (modelComp != null)
            {
                if (!string.IsNullOrEmpty(modelComp.Key))
                {
                    data.AssetPackKey = modelComp.Key;
                }

                if (modelComp.Material != null)
                {
                    data.MaterialData = new MaterialData
                    {
                        Name = modelComp.Material.Name,
                        TextureSlots = modelComp.Material.TextureSlots
                    };
                }
            }

            // NEW: component round-tripping support (extensible, mod-friendly, backward-compatible)
            data.Components = new List<EntityData.ComponentEntry>();

            foreach (var kvp in _components)
            {
                var componentType = kvp.Key;
                var component = kvp.Value;

                // Only components that implement IComponentData will be saved
                if (component is IComponentData serializable)
                {
                    var entry = new EntityData.ComponentEntry
                    {
                        Type = componentType.FullName,
                        Data = serializable.ToSerializableData()
                    };
                    data.Components.Add(entry);
                }
            }

            return data;
        }

        public static Entity FromData(EntityData data)
        {
            if (data == null) return new Entity();

            var entity = new Entity
            {
                Id = data.Id,
                Type = data.Type ?? "Default"
            };

            // === PHYSICS COMPONENT IS THE SINGLE SOURCE OF TRUTH ON LOAD ===
            var physics = new PhysicsComponent();
            physics.Position = data.Position;
            physics.Rotation = data.Rotation;
            physics.Scale = data.Scale != default ? data.Scale : Vector3.One;

            entity.AddComponent(physics);

            // Defensive sync so Entity.Transform always matches
            entity.Transform.Position = physics.Position;
            entity.Transform.Rotation = physics.Rotation;
            entity.Transform.Scale = physics.Scale;

            if (!string.IsNullOrEmpty(data.AssetPackKey))
            {
                var modelComp = new ModelComponent { Key = data.AssetPackKey };

                if (data.MaterialData != null)
                {
                    modelComp.Material = new Material
                    {
                        Name = data.MaterialData.Name ?? "DefaultMaterial",
                        TextureSlots = data.MaterialData.TextureSlots ?? new List<TextureSlot>()
                    };
                }

                entity.AddComponent(modelComp);

                if (modelComp.Model != null)
                {
                    physics.Size = modelComp.Model.GetBoundingSize();
                    physics.LocalBoundsMinCm = modelComp.Model.LocalBoundsMinCm;
                    physics.LocalBoundsMaxCm = modelComp.Model.LocalBoundsMaxCm;
                }
            }

            // NEW: component round-tripping (extensible, mod-friendly)
            if (data.Components != null)
            {
                foreach (var entry in data.Components)
                {
                    // Central factory (registered components only — unknown ones are skipped gracefully)
                    if (ComponentFactory.TryCreate(entry.Type, entry.Data, out var component))
                    {
                        entity.AddComponent(component);
                    }
                }
            }

            Console.WriteLine($"[Entity.FromData] Rehydrated entity '{entity.Type}' ID={entity.Id} Position={physics.Position} Size={physics.Size} LocalAABB=({physics.LocalBoundsMinCm}..{physics.LocalBoundsMaxCm}) Components={entity.Components.Count}");
            return entity;
        }

        // Central registration point for components that want to be serialized
        private static class ComponentFactory
        {
            private static readonly Dictionary<string, Func<object, IComponent>> _creators = new();

            public static void Register<T>(Func<object, T> creator) where T : IComponent
            {
                _creators[typeof(T).FullName] = data => creator(data);
            }

            public static bool TryCreate(string typeName, object data, out IComponent component)
            {
                component = null;
                if (string.IsNullOrEmpty(typeName) || !_creators.TryGetValue(typeName, out var creator))
                    return false;

                try
                {
                    component = creator(data);
                    return component != null;
                }
                catch
                {
                    return false;
                }
            }

            static ComponentFactory()
            {
                // Register known core components here (moddable later via static registration)
                Register(data => new TransformComponent());
                Register(data => new PhysicsComponent());
                // Add more as needed — no hard-coded list of "all" types
            }
        }
    }

    // Optional lightweight interface for any component that wants to persist
    public interface IComponentData
    {
        object ToSerializableData();
        void FromSerializableData(object data);
    }
}