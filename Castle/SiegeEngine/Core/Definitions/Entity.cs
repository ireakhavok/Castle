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
                Id = Id   // FIXED: persist unique ID across save/load so spawn never duplicates
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

                // NEW: serialize Material (world-aligned textures + all slots)
                if (modelComp.Material != null)
                {
                    data.MaterialData = new MaterialData
                    {
                        Name = modelComp.Material.Name,
                        TextureSlots = modelComp.Material.TextureSlots
                    };
                }
            }

            return data;
        }

        public static Entity FromData(EntityData data)
        {
            if (data == null) return new Entity();

            var entity = new Entity
            {
                Id = data.Id,   // FIXED: respect saved ID (prevents reset/duplicates on load)
                Type = data.Type ?? "Default"
            };

            // === PHYSICS COMPONENT IS THE SINGLE SOURCE OF TRUTH ON LOAD ===
            var physics = new PhysicsComponent();
            physics.Position = data.Position;
            physics.Rotation = data.Rotation;
            physics.Scale = data.Scale != default ? data.Scale : Vector3.One;

            entity.AddComponent(physics);

            // Defensive sync so Entity.Transform always matches (prevents any legacy code paths from seeing origin)
            entity.Transform.Position = physics.Position;
            entity.Transform.Rotation = physics.Rotation;
            entity.Transform.Scale = physics.Scale;

            if (!string.IsNullOrEmpty(data.AssetPackKey))
            {
                var modelComp = new ModelComponent { Key = data.AssetPackKey };

                // NEW: restore Material from saved data
                if (data.MaterialData != null)
                {
                    modelComp.Material = new Material
                    {
                        Name = data.MaterialData.Name ?? "DefaultMaterial",
                        TextureSlots = data.MaterialData.TextureSlots ?? new List<TextureSlot>()
                    };
                }

                entity.AddComponent(modelComp);

                // RIGHT-WAY FIX: set real model bounding size + exact local AABB (cm) from FBXModel
                // This guarantees OBB exactly matches visual geometry for raycast selection on rotated/non-centered models.
                if (modelComp.Model != null)
                {
                    physics.Size = modelComp.Model.GetBoundingSize();
                    physics.LocalBoundsMinCm = modelComp.Model.LocalBoundsMinCm;
                    physics.LocalBoundsMaxCm = modelComp.Model.LocalBoundsMaxCm;
                }
            }

            Console.WriteLine($"[Entity.FromData] Rehydrated entity '{entity.Type}' ID={entity.Id} Position={physics.Position} Size={physics.Size} LocalAABB=({physics.LocalBoundsMinCm}..{physics.LocalBoundsMaxCm}) (PhysicsComponent authoritative) MaterialSlots={entity.GetComponent<ModelComponent>()?.Material?.TextureSlots?.Count ?? 0}");
            return entity;
        }
    }
}