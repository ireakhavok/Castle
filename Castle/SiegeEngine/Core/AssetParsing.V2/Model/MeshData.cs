// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/MeshData.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.AssetParsing.Model;

namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class MeshData
    {
        public List<FBXVertex> Vertices { get; set; } = new List<FBXVertex>();
        public List<uint> Indices { get; set; } = new List<uint>();
        public List<Material> Materials { get; set; } = new List<Material>();
        public Vector3 Bounds { get; set; }
    }
}