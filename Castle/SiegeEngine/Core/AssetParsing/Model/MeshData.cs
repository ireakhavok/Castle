using SiegeEngine.Core.AssetParsing.Model;
using System.Collections.Generic;
using System.Numerics;
namespace SiegeEngine.Core.AssetParsing.Model
{
    public class MeshData
    {
        public List<FBXVertex> Vertices { get; set; } = new List<FBXVertex>();
        public List<uint> Indices { get; set; } = new List<uint>();
        public Vector3 Bounds { get; set; }
        public int[] PolygonMaterialIndices { get; set; }
        public List<Material> Materials { get; set; } = new List<Material>();
    }
}