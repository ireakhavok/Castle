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

        public PhysicsComponent Physics => GetComponent<PhysicsComponent>();

        public IReadOnlyDictionary<Type, IComponent> Components => _components;

        public Entity()
        {
        }

        public void AddComponent<T>(T component) where T : IComponent
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            _components[typeof(T)] = component;
        }

        public void AddComponent(IComponent component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            _components[component.GetType()] = component;
        }

        public T GetComponent<T>() where T : IComponent
        {
            return _components.TryGetValue(typeof(T), out var component) ? (T)component : default;
        }

        public bool RemoveComponent<T>() where T : IComponent
        {
            return _components.Remove(typeof(T));
        }

        public EntityData ToData()
        {
            var data = new EntityData
            {
                Type = Type,
                Id = Id
            };

            var physics = GetComponent<PhysicsComponent>();
            if (physics != null)
            {
                data.Position = physics.Position;
                data.Rotation = SanitizeRotation(physics.Rotation);
                data.Scale = physics.Scale;
            }
            else
            {
                data.Position = Vector3.Zero;
                data.Rotation = Quaternion.Identity;
                data.Scale = Vector3.One;
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

                if (modelComp.HiddenMeshIndices != null && modelComp.HiddenMeshIndices.Count > 0)
                    data.HiddenMeshIndices = new List<int>(modelComp.HiddenMeshIndices);
            }

            data.Components = new List<EntityData.ComponentEntry>();

            foreach (var kvp in _components)
            {
                var componentType = kvp.Key;
                var component = kvp.Value;

                if (componentType == typeof(IComponent) || componentType.IsInterface)
                    continue;

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

            var physics = new PhysicsComponent();
            physics.Position = data.Position;
            physics.Rotation = SanitizeRotation(data.Rotation);
            physics.Scale = data.Scale != default ? data.Scale : Vector3.One;

            entity.AddComponent(physics);

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

                if (data.HiddenMeshIndices != null && data.HiddenMeshIndices.Count > 0)
                    modelComp.HiddenMeshIndices = new List<int>(data.HiddenMeshIndices);

                entity.AddComponent(modelComp);

                if (modelComp.Model != null)
                {
                    physics.Size = modelComp.Model.GetBoundingSize();
                    physics.LocalBoundsMinCm = modelComp.Model.LocalBoundsMinCm;
                    physics.LocalBoundsMaxCm = modelComp.Model.LocalBoundsMaxCm;
                }
            }

            if (data.Components != null)
            {
                string physicsFullName = typeof(PhysicsComponent).FullName;
                string modelFullName = typeof(ModelComponent).FullName;

                foreach (var entry in data.Components)
                {
                    if (string.IsNullOrEmpty(entry.Type)) continue;

                    if (entry.Type == physicsFullName)
                    {
                        if (entry.Data != null)
                            physics.FromSerializableData(entry.Data);
                        physics.Rotation = SanitizeRotation(physics.Rotation);
                        continue;
                    }

                    // Do not replay ModelComponent payload — that overwrites live
                    // materials and was breaking walls on reload. Hide list lives
                    // on EntityData.HiddenMeshIndices only.
                    if (entry.Type == modelFullName)
                        continue;

                    try
                    {
                        System.Type componentType = ResolveComponentType(entry.Type);
                        if (componentType == null ||
                            !typeof(IComponent).IsAssignableFrom(componentType) ||
                            componentType.IsInterface)
                        {
                            Console.WriteLine($"[Entity.FromData] Could not recreate component type: {entry.Type}");
                            continue;
                        }

                        var component = (IComponent)Activator.CreateInstance(componentType);

                        if (component is IComponentData serializable && entry.Data != null)
                        {
                            serializable.FromSerializableData(entry.Data);
                        }

                        entity.AddComponent(component);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Entity.FromData] Could not recreate component type: {entry.Type} ({ex.Message})");
                    }
                }
            }

            Console.WriteLine($"[Entity.FromData] Rehydrated entity '{entity.Type}' ID={entity.Id} Position={physics.Position} Size={physics.Size} HiddenMeshes={(data.HiddenMeshIndices == null ? 0 : data.HiddenMeshIndices.Count)} Components={entity.Components.Count}");
            return entity;
        }

        private static System.Type ResolveComponentType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;

            System.Type t = System.Type.GetType(fullName);
            if (t != null) return t;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType(fullName);
                    if (t != null) return t;
                }
                catch
                {
                }
            }
            return null;
        }

        public static Quaternion SanitizeRotation(Quaternion q)
        {
            if (float.IsNaN(q.X) || float.IsNaN(q.Y) || float.IsNaN(q.Z) || float.IsNaN(q.W) ||
                q.LengthSquared() < 1e-6f)
            {
                return Quaternion.Identity;
            }
            return Quaternion.Normalize(q);
        }
    }

    public interface IComponentData
    {
        object ToSerializableData();
        void FromSerializableData(object data);
    }
}
