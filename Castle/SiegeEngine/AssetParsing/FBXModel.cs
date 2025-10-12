// Engine.Core.AssetParsing/FBXModel.cs
using SiegeEngine.Rendering;
using SiegeEngine.Definitions;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.AssetParsing
{
    public class FBXModel
    {
        public List<MeshData> Meshes { get; set; } = new List<MeshData>();
        public Skeleton Skeleton { get; set; }
        public List<Animation> Animations { get; set; }
        public Entity Entity { get; set; }
        public List<Material> Materials { get; set; }
        public bool HasSkin { get; set; }
        public FBXModel()
        {
            Skeleton = new Skeleton { Bones = new List<Bone>() };
            Animations = new List<Animation>();
            Materials = new List<Material>();
        }
    }

    public class MeshData
    {
        public List<FBXVertex> Vertices { get; set; } = new List<FBXVertex>();
        public List<uint> Indices { get; set; } = new List<uint>();
        public Vector3 Bounds { get; set; }
        public int[] PolygonMaterialIndices { get; set; }
        public List<Material> Materials { get; set; } = new List<Material>();
    }

    public class FBXVertex
    {
        public float X, Y, Z;
        public float Nx, Ny, Nz;
        public float U, V;
        public float MatIdx;
        public float Tx, Ty, Tz;
        public int BoneID0, BoneID1, BoneID2, BoneID3;
        public float Weight0, Weight1, Weight2, Weight3;

        public FBXVertex(float x, float y, float z, float nx, float ny, float nz, float u, float v, float matIdx, float tx = 0, float ty = 0, float tz = 0,
                         int boneID0 = 0, int boneID1 = 0, int boneID2 = 0, int boneID3 = 0,
                         float weight0 = 0, float weight1 = 0, float weight2 = 0, float weight3 = 0)
        {
            X = x; Y = y; Z = z;
            Nx = nx; Ny = ny; Nz = nz;
            U = u; V = v;
            MatIdx = matIdx;
            Tx = tx; Ty = ty; Tz = tz;
            BoneID0 = boneID0; BoneID1 = boneID1; BoneID2 = boneID2; BoneID3 = boneID3;
            Weight0 = weight0; Weight1 = weight1; Weight2 = weight2; Weight3 = weight3;
        }
    }

    public class Skeleton
    {
        public List<Bone> Bones { get; set; }
        public Matrix4x4[] GetTransforms()
        {
            var transforms = new Matrix4x4[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
                transforms[i] = Matrix4x4.Identity;
            return transforms;
        }
        public void UpdateTransforms(Matrix4x4[] transforms)
        {
            Console.WriteLine($"Skeleton: Updated {transforms.Length} bone transforms");
        }
    }

    public class Bone
    {
        public string Name { get; set; }
        public Matrix4x4 BindPose { get; set; }
        public int ParentIndex { get; set; }
    }

    public class Animation
    {
        public string Name { get; set; }
        public List<Keyframe> Keyframes { get; set; }
        public Matrix4x4[] GetBoneTransforms(float time)
        {
            return new Matrix4x4[1] { Matrix4x4.Identity };
        }
    }

    public class Keyframe
    {
        public float Time { get; set; }
        public List<Matrix4x4> BoneTransforms { get; set; }
    }

    public class TextureInfo
    {
        public string Path { get; set; }
        public int WrapU { get; set; }
        public int WrapV { get; set; }
    }

    public class Material
    {
        public string Name { get; set; }
        public string ShadingModel { get; set; }
        public string CullingMode { get; set; }
        public bool MultiLayer { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public Dictionary<string, TextureInfo> Textures { get; set; } // e.g., "albedo" -> TextureInfo
        public Material()
        {
            Properties = new Dictionary<string, object>();
            Textures = new Dictionary<string, TextureInfo>();
        }
    }
}