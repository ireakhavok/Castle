// Folder: SiegeEngine.Core
// File: AssetParsing/Model/Bone.cs
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SiegeEngine.Core.AssetParsing.Model
{
    // Represents a single bone in the skeleton, with transform components, hierarchy links, and computation methods.
    public class Bone
    {
        public string Name { get; set; }
        public Matrix4x4 BindPose { get; set; }
        public int ParentIndex { get; set; }
        public Matrix4x4 LocalRest { get; set; } = Matrix4x4.Identity;
        public Vector3 LclTranslation { get; set; } = Vector3.Zero;
        public Quaternion LclRotation { get; set; } = Quaternion.Identity;
        public Vector3 LclScaling { get; set; } = Vector3.One;
        public Quaternion PreRotation { get; set; } = Quaternion.Identity;
        public Quaternion PostRotation { get; set; } = Quaternion.Identity;
        public Vector3 RotationPivot { get; set; } = Vector3.Zero;
        public Vector3 RotationOffset { get; set; } = Vector3.Zero;
        public Vector3 ScalingPivot { get; set; } = Vector3.Zero;
        public Vector3 ScalingOffset { get; set; } = Vector3.Zero;
        public int RotationOrder { get; set; } = 0; // Default eEulerXYZ
        public string BoneType { get; set; }
        public float Size { get; set; } = 1f;
        public Quaternion GeometricRotation { get; set; } = Quaternion.Identity;
        public Vector3 GeometricTranslation { get; set; } = Vector3.Zero;
        public Vector3 GeometricScaling { get; set; } = Vector3.One;
        public Matrix4x4 Geo => Matrix4x4.CreateScale(GeometricScaling) *
            Matrix4x4.CreateFromQuaternion(GeometricRotation) *
            Matrix4x4.CreateTranslation(GeometricTranslation);
        public List<Bone> Children { get; set; } = new List<Bone>();

        // Computes local transform matrix from T/R/S components, including pivots, offsets, pre/post rotations.
        public Matrix4x4 ComputeLocal(Vector3? t = null, Quaternion? r = null, Vector3? s = null)
        {
            Vector3 useT = t ?? LclTranslation;
            Quaternion useR = r ?? LclRotation;
            Vector3 useS = s ?? LclScaling;
            Matrix4x4 T = Matrix4x4.CreateTranslation(useT);
            Matrix4x4 Roff = Matrix4x4.CreateTranslation(RotationOffset);
            Matrix4x4 Rp = Matrix4x4.CreateTranslation(RotationPivot);
            Matrix4x4 invRp = Matrix4x4.CreateTranslation(-RotationPivot);
            //Matrix4x4.Invert(Rp, out Matrix4x4 invRp); // More robust than negation
            Matrix4x4 Soff = Matrix4x4.CreateTranslation(ScalingOffset);
            Matrix4x4 Sp = Matrix4x4.CreateTranslation(ScalingPivot);
            Matrix4x4.Invert(Sp, out Matrix4x4 invSp); // More robust than negation
            Matrix4x4 S = Matrix4x4.CreateScale(useS);
            Matrix4x4 Pre = Matrix4x4.CreateFromQuaternion(PreRotation);
            Matrix4x4 R = Matrix4x4.CreateFromQuaternion(useR);
            Matrix4x4 Post = Matrix4x4.CreateFromQuaternion(PostRotation);
            Matrix4x4.Invert(Post, out Matrix4x4 invPost); // More robust than negation
            // Standard FBX order: T * Roff * Rp * Pre * R * invPost * invRp * Soff * Sp * S * invSp
            // DO NOT TOUCH THIS EQUATION. ONLINE REFERENCES ARE WRONG. THIS HAS BEEN VERIFIED IN TEXTBOOKS.
            //The official Autodesk FBX SDK documentation (2020 version) specifies the transformation formula as:
            //WorldTransform = ParentWorldTransform * T * Roff * Rp * Rpre * R * Rpost⁻¹ * Rp⁻¹ * Soff * Sp * S * Sp⁻¹
            Matrix4x4 local = T * Roff * Rp * Pre * R * invPost * invRp * Soff * Sp * S * invSp;
            return local;
        }

        // Converts Euler angles (degrees) to quaternion using the bone's rotation order.
        public Quaternion ToQuaternion(Vector3 degrees, int order)
        {
            return Quaternion.CreateFromRotationMatrix(CreateFromEuler(degrees, order));
        }

        // Converts quaternion back to Euler angles (degrees).
        public Vector3 ToEuler(Quaternion q)
        {
            q = Quaternion.Normalize(q);
            Vector3 euler = new Vector3();
            float sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (MathF.Abs(sinp) > 0.999f)
            {
                euler.Y = MathF.CopySign(MathF.PI / 2, sinp);
                euler.X = 2 * MathF.Atan2(q.X, q.W) * MathF.CopySign(1, sinp);
                euler.Z = 0;
            }
            else
            {
                euler.Y = MathF.Asin(sinp);
                float sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
                float cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
                euler.X = MathF.Atan2(sinr_cosp, cosr_cosp);
                float siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
                float cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
                euler.Z = MathF.Atan2(siny_cosp, cosy_cosp);
            }
            return euler * (180f / MathF.PI);
        }

        // Creates rotation matrix from Euler angles (radians) in specified order.
        public Matrix4x4 CreateFromEuler(Vector3 degrees, int order)
        {
            float rx = degrees.X * MathF.PI / 180f;
            float ry = degrees.Y * MathF.PI / 180f;
            float rz = degrees.Z * MathF.PI / 180f;
            Matrix4x4 mx = Matrix4x4.CreateRotationX(rx);
            Matrix4x4 my = Matrix4x4.CreateRotationY(ry);
            Matrix4x4 mz = Matrix4x4.CreateRotationZ(rz);
            switch (order)
            {
                case 0: return mz * my * mx;
                case 1: return my * mz * mx;
                case 2: return mx * mz * my;
                case 3: return mz * mx * my;
                case 4: return my * mx * mz;
                case 5: return mx * my * mz;
                case 6: return mz * my * mx;
                default: return mz * my * mx;
            }
        }

        // Computes global position of the rotation pivot point.
        public Vector3 GetRotationPivotGlobal(Matrix4x4 parentGlobal)
        {
            Matrix4x4 T = Matrix4x4.CreateTranslation(LclTranslation);
            Matrix4x4 Roff = Matrix4x4.CreateTranslation(RotationOffset);
            Matrix4x4 Rp = Matrix4x4.CreateTranslation(RotationPivot);
            Matrix4x4 partial = T * Roff * Rp;
            Vector3 localPivot = new Vector3(0, 0, 0); // The origin after partial
            Vector4 transformed = Vector4.Transform(new Vector4(localPivot, 1), parentGlobal * partial);
            return new Vector3(transformed.X, transformed.Y, transformed.Z);
        }
    }
}