// Folder: SiegeEngine/Core/AssetParsing/Model
// File: FBXMesh.cs
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class FBXMesh
    {
        public Vector3[] Vertices { get; set; }
        public Vector2[] UVs { get; set; }
        public int[] Indices { get; set; }
    }
}