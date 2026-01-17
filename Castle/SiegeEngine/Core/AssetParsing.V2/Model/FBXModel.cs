// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/FBXModel.cs
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class FBXModel
    {
        public List<MeshData> Meshes { get; set; } = new List<MeshData>();
        public Skeleton Skeleton { get; set; } = new Skeleton();
        public List<Animation> Animations { get; set; } = new List<Animation>();
        public int[] SourceToTarget { get; set; }
        public int[] Signs { get; set; }
        public float ModelScale { get; set; }
        public Matrix4x4 P4 { get; set; }
        public Matrix4x4 InvP4 { get; set; }
        public bool HasSkin { get; set; } = false;
        public bool HasRestPose { get; set; }
        // Methods like HasUnweightedVertices will be added iteratively
    }
}