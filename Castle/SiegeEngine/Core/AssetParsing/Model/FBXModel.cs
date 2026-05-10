// Folder: SiegeEngine/Core/AssetParsing/Model
// File: FBXModel.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class FBXModel
    {
        public List<MeshData> Meshes { get; set; } = new List<MeshData>();
        public Skeleton Skeleton { get; set; } = new Skeleton();
        public List<Animation> Animations { get; set; } = new List<Animation>();
        public bool HasSkin { get; set; } = false;
        public bool HasRestPose { get; set; }
        public bool AutoCorrected { get; set; } = false;

        // NEW: exact local-space AABB in FBX cm units (computed once from vertices)
        // Used by PhysicsComponent.RayIntersects to eliminate centering assumption for walls/prefabs
        public Vector3 LocalBoundsMinCm { get; set; } = new Vector3(float.MaxValue);
        public Vector3 LocalBoundsMaxCm { get; set; } = new Vector3(float.MinValue);

        /// <summary>
        /// Computes the world-space bounding size in METERS from all vertex positions.
        /// FBX files are exported in CENTIMETERS (standard convention). The 0.01f multiplier
        /// exactly matches the render scale used in SceneEditorPanel.RenderInnerContent
        /// (cm → m conversion). This guarantees the PhysicsComponent.Size AABB matches
        /// the visual geometry for raycast selection.
        /// </summary>
        public Vector3 GetBoundingSize()
        {
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);
            bool hasVertices = false;

            foreach (var mesh in Meshes)
            {
                foreach (var vertex in mesh.Vertices)
                {
                    hasVertices = true;
                    min = Vector3.Min(min, vertex.Position);
                    max = Vector3.Max(max, vertex.Position);
                }
            }

            if (!hasVertices)
            {
                LocalBoundsMinCm = Vector3.Zero;
                LocalBoundsMaxCm = Vector3.Zero;
                return new Vector3(1f); // safe fallback if model has no geometry
            }

            LocalBoundsMinCm = min;
            LocalBoundsMaxCm = max;

            Vector3 localSizeCm = max - min;
            return localSizeCm * 0.01f; // convert cm → meters
        }
    }
}