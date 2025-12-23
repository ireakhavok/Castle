// Folder: SiegeEngine
// File: FBXModel.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace SiegeEngine.Core.AssetParsing
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
        private Matrix4x4[] _currentTransforms;
        public Matrix4x4[] GetTransforms()
        {
            if (_currentTransforms == null)
            {
                _currentTransforms = new Matrix4x4[Bones.Count];
                for (int i = 0; i < Bones.Count; i++)
                    _currentTransforms[i] = Matrix4x4.Identity;
            }
            return _currentTransforms;
        }
        public void UpdateTransforms(Matrix4x4[] transforms)
        {
            _currentTransforms = transforms;
        }
        public Matrix4x4[] ComputeGlobalTransforms(Matrix4x4[] localTransforms)
        {
            var globalTransforms = new Matrix4x4[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
            {
                var parentIndex = Bones[i].ParentIndex;
                globalTransforms[i] = (parentIndex >= 0 ? globalTransforms[parentIndex] : Matrix4x4.Identity) * localTransforms[i];
            }
            return globalTransforms;
        }
        public Matrix4x4[] ComputeFinalTransforms(Matrix4x4[] globalTransforms)
        {
            var finalTransforms = new Matrix4x4[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
            {
                finalTransforms[i] = globalTransforms[i] * Bones[i].BindPose;
            }
            return finalTransforms;
        }
    }
    public class Bone
    {
        public string Name { get; set; }
        public Matrix4x4 BindPose { get; set; }
        public int ParentIndex { get; set; }
        public Matrix4x4 LocalRest { get; set; } = Matrix4x4.Identity;
        public Vector3 LclTranslation { get; set; } = Vector3.Zero;
        public Vector3 LclRotation { get; set; } = Vector3.Zero;
        public Vector3 LclScaling { get; set; } = Vector3.One;
        public Vector3 PreRotation { get; set; } = Vector3.Zero;
        public Vector3 PostRotation { get; set; } = Vector3.Zero;
        public Vector3 RotationPivot { get; set; } = Vector3.Zero;
        public Vector3 RotationOffset { get; set; } = Vector3.Zero;
        public Vector3 ScalingPivot { get; set; } = Vector3.Zero;
        public Vector3 ScalingOffset { get; set; } = Vector3.Zero;
        public int RotationOrder { get; set; } = 0; // Default eEulerXYZ
        public string BoneType { get; set; }
        public float Size { get; set; } = 1f;
        public Vector3 GeometricTranslation { get; set; } = Vector3.Zero;
        public Vector3 GeometricRotation { get; set; } = Vector3.Zero;
        public Vector3 GeometricScaling { get; set; } = Vector3.One;
        public Matrix4x4 Geo => Matrix4x4.CreateScale(GeometricScaling) *
                                CreateFromEuler(GeometricRotation, 0) *
                                Matrix4x4.CreateTranslation(GeometricTranslation);
        public Matrix4x4 ComputeLocal(Vector3? t = null, Vector3? r = null, Vector3? s = null)
        {
            Vector3 useT = t ?? LclTranslation;
            Vector3 useR = r ?? LclRotation;
            Vector3 useS = s ?? LclScaling;
            if (BoneType == "LimbNode")
            {
                useT += new Vector3(0, 0, Size / 2); // along Z up
            }
            else if (BoneType == "Limb")
            {
                useT += new Vector3(0, Size / 2, 0); // along Y forward
            }
            Matrix4x4 T = Matrix4x4.CreateTranslation(useT);
            Matrix4x4 Roff = Matrix4x4.CreateTranslation(RotationOffset);
            Matrix4x4 Rp = Matrix4x4.CreateTranslation(RotationPivot);
            Matrix4x4 invRp = Matrix4x4.CreateTranslation(-RotationPivot);
            Matrix4x4 Soff = Matrix4x4.CreateTranslation(ScalingOffset);
            Matrix4x4 Sp = Matrix4x4.CreateTranslation(ScalingPivot);
            Matrix4x4 invSp = Matrix4x4.CreateTranslation(-ScalingPivot);
            Matrix4x4 S = Matrix4x4.CreateScale(useS);
            Matrix4x4 Pre = CreateFromEuler(PreRotation, 0); // Always XYZ
            Matrix4x4 R = CreateFromEuler(useR, RotationOrder);
            Matrix4x4 Post = CreateFromEuler(PostRotation, 0); // Always XYZ
            Matrix4x4 invPost;
            Matrix4x4.Invert(Post, out invPost);
            // FBX order: T * Roff * Rp * Pre * R * inv(Post) * inv(Rp) * Soff * Sp * S * inv(Sp)
            Matrix4x4 local = T * Roff * Rp * Pre * R * invPost * invRp * Soff * Sp * S * invSp;
            return local;
        }
        private Matrix4x4 CreateFromEuler(Vector3 degrees, int order)
        {
            float rx = degrees.X * MathF.PI / 180f;
            float ry = degrees.Y * MathF.PI / 180f;
            float rz = degrees.Z * MathF.PI / 180f;
            Matrix4x4 mx = Matrix4x4.CreateRotationX(rx);
            Matrix4x4 my = Matrix4x4.CreateRotationY(ry);
            Matrix4x4 mz = Matrix4x4.CreateRotationZ(rz);
            switch (order)
            {
                case 0: // eEulerXYZ
                    return mx * my * mz;
                case 1: // eEulerXZY
                    return mx * mz * my;
                case 2: // eEulerYZX
                    return my * mz * mx;
                case 3: // eEulerYXZ
                    return my * mx * mz;
                case 4: // eEulerZXY
                    return mz * mx * my;
                case 5: // eEulerZYX
                    return mz * my * mx;
                case 6: // eSphericXYZ, approximate as XYZ
                    return mx * my * mz;
                default:
                    return mx * my * mz;
            }
        }
    }
    public class Animation
    {
        public string Name { get; set; }
        public List<Keyframe> Keyframes { get; set; } = new List<Keyframe>();
        public Matrix4x4[] GetBoneTransforms(float time)
        {
            if (Keyframes.Count == 0)
            {
                return null;
            }
            float duration = Keyframes.Last().Time;
            if (duration <= 0)
            {
                return Keyframes[0].BoneTransforms.ToArray();
            }
            time = time % duration;
            int lowerIndex = Keyframes.FindLastIndex(kf => kf.Time <= time);
            if (lowerIndex == -1)
            {
                return Keyframes[0].BoneTransforms.ToArray();
            }
            if (lowerIndex == Keyframes.Count - 1 || Keyframes[lowerIndex].Time == time)
            {
                return Keyframes[lowerIndex].BoneTransforms.ToArray();
            }
            Keyframe lower = Keyframes[lowerIndex];
            Keyframe upper = Keyframes[lowerIndex + 1];
            float factor = (time - lower.Time) / (upper.Time - lower.Time);
            int numBones = lower.BoneTransforms.Count;
            Matrix4x4[] interpolated = new Matrix4x4[numBones];
            for (int b = 0; b < numBones; b++)
            {
                Matrix4x4.Decompose(lower.BoneTransforms[b], out Vector3 lScale, out Quaternion lRot, out Vector3 lTrans);
                Matrix4x4.Decompose(upper.BoneTransforms[b], out Vector3 uScale, out Quaternion uRot, out Vector3 uTrans);
                Vector3 iTrans = Vector3.Lerp(lTrans, uTrans, factor);
                Quaternion iRot = Quaternion.Slerp(lRot, uRot, factor);
                Vector3 iScale = Vector3.Lerp(lScale, uScale, factor);
                interpolated[b] = Matrix4x4.CreateScale(iScale) * Matrix4x4.CreateFromQuaternion(iRot) * Matrix4x4.CreateTranslation(iTrans);
            }
            return interpolated;
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