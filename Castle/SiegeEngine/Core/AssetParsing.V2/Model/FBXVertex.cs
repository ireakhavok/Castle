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
        public Vector2 UV;
        public Vector2 TexCoord;
        public Vector3 Tangent;
        public int BoneID0;
        public int BoneID1;
        public int BoneID2;
        public int BoneID3;
        public Vector4 Weights;
        public float MatIdx;
        public FBXVertex(float x, float y, float z, float nx, float ny, float nz, float u, float v, float matIdx, float tx = 0, float ty = 0, float tz = 0,
        int boneID0 = -1, int boneID1 = -1, int boneID2 = -1, int boneID3 = -1,
        float weight0 = 0, float weight1 = 0, float weight2 = 0, float weight3 = 0)
        {
            Position = new Vector3(x, y, z);
            Normal = new Vector3(nx, ny, nz);
            UV = new Vector2(u, v);
            TexCoord = new Vector2(u, v);
            MatIdx = matIdx;
            Tangent = new Vector3(tx, ty, tz);
            BoneID0 = boneID0;
            BoneID1 = boneID1;
            BoneID2 = boneID2;
            BoneID3 = boneID3;
            Weights = new Vector4(weight0, weight1, weight2, weight3);
        }
    }
}