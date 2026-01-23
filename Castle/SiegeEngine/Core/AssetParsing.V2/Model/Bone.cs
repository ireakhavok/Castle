// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/Bone.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.AssetObjects;
namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class Bone
    {
        public string Name { get; set; }
        public Matrix4x4 BindPose { get; set; } = Matrix4x4.Identity;
        public int ParentIndex { get; set; } = -1;
        public Matrix4x4 LocalRest { get; set; } = Matrix4x4.Identity;
        public Vector3 LclTranslation { get; set; } = Vector3.Zero;
        public Quaternion LclRotation { get; set; } = Quaternion.Identity;
        public Vector3 LclScaling { get; set; } = Vector3.One;
        public Vector3 PreRotation { get; set; } = Vector3.Zero;
        public Vector3 PostRotation { get; set; } = Vector3.Zero;
        public Vector3 RotationPivot { get; set; } = Vector3.Zero;
        public Vector3 RotationOffset { get; set; } = Vector3.Zero;
        public Vector3 ScalingPivot { get; set; } = Vector3.Zero;
        public Vector3 ScalingOffset { get; set; } = Vector3.Zero;
        public int RotationOrder { get; set; } = 0; // 0: XYZ, 1: XZY, 2: YZX, 3: YXZ, 4: ZXY, 5: ZYX, 6: Spherical XYZ
        public Vector3 GeometricTranslation { get; set; } = Vector3.Zero;
        public Vector3 GeometricRotation { get; set; } = Vector3.Zero;
        public Vector3 GeometricScaling { get; set; } = Vector3.One;
        public Matrix4x4 GeometricTransform { get; set; } = Matrix4x4.Identity;
        public List<Bone> Children { get; set; } = new List<Bone>();
        public bool IsDrawable { get; set; } = true;
        public Matrix4x4 ComputeLocal(Vector3? t = null, Quaternion? r = null, Vector3? s = null)
        {
            Vector3 useT = t ?? LclTranslation;
            Quaternion useR = r ?? LclRotation;
            Vector3 useS = s ?? LclScaling;
            Matrix4x4 T = Matrix4x4.CreateTranslation(useT);
            Matrix4x4 Roff = Matrix4x4.CreateTranslation(RotationOffset);
            Matrix4x4 Rp = Matrix4x4.CreateTranslation(RotationPivot);
            Matrix4x4 Pre = Matrix4x4.CreateFromYawPitchRoll(PreRotation.Y * MathF.PI / 180f, PreRotation.X * MathF.PI / 180f, PreRotation.Z * MathF.PI / 180f);
            Matrix4x4 R = Matrix4x4.CreateFromQuaternion(useR);
            Matrix4x4 PostInv = Matrix4x4.CreateFromYawPitchRoll(-PostRotation.Y * MathF.PI / 180f, -PostRotation.X * MathF.PI / 180f, -PostRotation.Z * MathF.PI / 180f);
            Matrix4x4 invRp = Matrix4x4.CreateTranslation(-RotationPivot);
            Matrix4x4 Soff = Matrix4x4.CreateTranslation(ScalingOffset);
            Matrix4x4 Sp = Matrix4x4.CreateTranslation(ScalingPivot);
            Matrix4x4 S = Matrix4x4.CreateScale(useS);
            Matrix4x4 invSp = Matrix4x4.CreateTranslation(-ScalingPivot);
            // Standard FBX order: T * Roff * Rp * Pre * R * invPost * invRp * Soff * Sp * S * invSp
            // DO NOT TOUCH THIS EQUATION. ONLINE REFERENCES ARE WRONG. THIS HAS BEEN VERIFIED IN TEXTBOOKS.
            //The official Autodesk FBX SDK documentation (2020 version) specifies the transformation formula as:
            //WorldTransform = ParentWorldTransform * T * Roff * Rp * Rpre * R * Rpost⁻¹ * Rp⁻¹ * Soff * Sp * S * Sp⁻¹
            return T * Roff * Rp * Pre * R * PostInv * invRp * Soff * Sp * S * invSp;
        }
        public Quaternion ToQuaternion(Vector3 degrees)
        {
            float rx = degrees.X * MathF.PI / 180f;
            float ry = degrees.Y * MathF.PI / 180f;
            float rz = degrees.Z * MathF.PI / 180f;
            Quaternion qx = Quaternion.CreateFromAxisAngle(Vector3.UnitX, rx);
            Quaternion qy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, ry);
            Quaternion qz = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rz);
            switch (RotationOrder)
            {
                case 0: return qz * qy * qx; // XYZ
                case 1: return qy * qz * qx; // XZY
                case 2: return qx * qz * qy; // YZX
                case 3: return qz * qx * qy; // YXZ
                case 4: return qy * qx * qz; // ZXY
                case 5: return qx * qy * qz; // ZYX
                default: return qz * qy * qx;
            }
        }
    }
}