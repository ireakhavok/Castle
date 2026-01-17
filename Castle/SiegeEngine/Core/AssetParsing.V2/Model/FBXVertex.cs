// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/FBXVertex.cs
using System;
using System.Numerics;
namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public struct FBXVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord;
        public Vector3 Tangent;
        public Vector4 BoneIDs;
        public Vector4 Weights;
        public float MatIdx;
    }
}