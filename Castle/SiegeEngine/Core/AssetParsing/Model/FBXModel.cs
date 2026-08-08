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

        /// <summary>
        /// Conversion factor from the numbers stored in the vertex buffers to metres.
        /// Default 0.01 = classic Unity/Unreal centimetre convention.
        /// Set to 1.0 for Blender exports that already write metres (UnitScaleFactor == 1).
        /// </summary>
        public float UnitToMeters { get; set; } = 0.01f;

        // Local-space AABB in METRES (already multiplied by UnitToMeters).
        // Name retained for compatibility.
        public Vector3 LocalBoundsMinCm { get; set; } = new Vector3(float.MaxValue);
        public Vector3 LocalBoundsMaxCm { get; set; } = new Vector3(float.MinValue);

        /// <summary>
        /// Returns the axis-aligned bounding size in METRES.
        /// Applies UnitToMeters once so every consumer receives consistent units.
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
                return new Vector3(1f);
            }
            // Convert to metres once
            LocalBoundsMinCm = min * UnitToMeters;
            LocalBoundsMaxCm = max * UnitToMeters;
            return (max - min) * UnitToMeters;
        }
    }
}