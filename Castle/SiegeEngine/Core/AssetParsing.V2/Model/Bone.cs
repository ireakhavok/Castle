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
        public Matrix4x4 BindLocal { get; set; } = Matrix4x4.Identity;
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
        public int InheritType { get; set; } = 0;

        //public string BoneType { get; set; } = "None";
        public Matrix4x4 ComputeLocal()
        {
            Vector3 useT = LclTranslation;
            Quaternion useR = LclRotation;
            Vector3 useS = LclScaling;
            Matrix4x4 T = Matrix4x4.CreateTranslation(useT);
            Matrix4x4 Roff = Matrix4x4.CreateTranslation(RotationOffset);
            Matrix4x4 Rp = Matrix4x4.CreateTranslation(RotationPivot);
            // PreRotation as quaternion in fixed XYZ order
            float prx = PreRotation.X * MathF.PI / 180f;
            float pry = PreRotation.Y * MathF.PI / 180f;
            float prz = PreRotation.Z * MathF.PI / 180f;
            Quaternion qxPre = Quaternion.CreateFromAxisAngle(Vector3.UnitX, prx);
            Quaternion qyPre = Quaternion.CreateFromAxisAngle(Vector3.UnitY, pry);
            Quaternion qzPre = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, prz);
            Matrix4x4 Pre = Matrix4x4.CreateFromQuaternion(qzPre * qyPre * qxPre);
            Matrix4x4 R = Matrix4x4.CreateFromQuaternion(useR);
            // PostRotation inverse as quaternion in fixed XYZ order
            float pox = PostRotation.X * MathF.PI / 180f;
            float poy = PostRotation.Y * MathF.PI / 180f;
            float poz = PostRotation.Z * MathF.PI / 180f;
            Quaternion qxPost = Quaternion.CreateFromAxisAngle(Vector3.UnitX, pox);
            Quaternion qyPost = Quaternion.CreateFromAxisAngle(Vector3.UnitY, poy);
            Quaternion qzPost = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, poz);
            Quaternion postQ = qzPost * qyPost * qxPost;
            Matrix4x4 PostInv = Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(postQ));
            Matrix4x4 invRp = Matrix4x4.CreateTranslation(-RotationPivot);
            Matrix4x4 Soff = Matrix4x4.CreateTranslation(ScalingOffset);
            Matrix4x4 Sp = Matrix4x4.CreateTranslation(ScalingPivot);
            Matrix4x4 S = Matrix4x4.CreateScale(useS);
            Matrix4x4 invSp = Matrix4x4.CreateTranslation(-ScalingPivot);
            // Standard FBX order: T * Roff * Rp * Pre * R * invPost * invRp * Soff * Sp * S * invSp
            // DO NOT TOUCH THIS EQUATION. ONLINE REFERENCES ARE WRONG. THIS HAS BEEN VERIFIED IN TEXTBOOKS.
            //The official Autodesk FBX SDK documentation (2020 version) specifies the transformation formula as:
            //WorldTransform = ParentWorldTransform * T * Roff * Rp * Rpre * R * Rpost⁻¹ * Rp⁻¹ * Soff * Sp * S * Sp⁻¹
            //Matrix4x4 local = T * Roff * Rp * Pre * R * PostInv * invRp * Soff * Sp * S * invSp;
            // BUT THIS IS FOR COLUMN MAJOR. FOR ROW MAJOR IT MUST BE REVERSED:
            Matrix4x4 local = invSp * S * Sp * Soff * invRp * PostInv * R * Pre * Rp * Roff * T;
            return local;
        }
        public Matrix4x4 ComputeLocal(Vector3 translation, Quaternion rotation, Vector3 scaling)
        {
            Vector3 useT = translation;
            Quaternion useR = rotation;
            Vector3 useS = scaling;
            Matrix4x4 T = Matrix4x4.CreateTranslation(useT);
            Matrix4x4 Roff = Matrix4x4.CreateTranslation(RotationOffset);
            Matrix4x4 Rp = Matrix4x4.CreateTranslation(RotationPivot);
            // PreRotation as quaternion in fixed XYZ order
            float prx = PreRotation.X * MathF.PI / 180f;
            float pry = PreRotation.Y * MathF.PI / 180f;
            float prz = PreRotation.Z * MathF.PI / 180f;
            Quaternion qxPre = Quaternion.CreateFromAxisAngle(Vector3.UnitX, prx);
            Quaternion qyPre = Quaternion.CreateFromAxisAngle(Vector3.UnitY, pry);
            Quaternion qzPre = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, prz);
            Matrix4x4 Pre = Matrix4x4.CreateFromQuaternion(qzPre * qyPre * qxPre);
            Matrix4x4 R = Matrix4x4.CreateFromQuaternion(useR);
            // PostRotation inverse as quaternion in fixed XYZ order
            float pox = PostRotation.X * MathF.PI / 180f;
            float poy = PostRotation.Y * MathF.PI / 180f;
            float poz = PostRotation.Z * MathF.PI / 180f;
            Quaternion qxPost = Quaternion.CreateFromAxisAngle(Vector3.UnitX, pox);
            Quaternion qyPost = Quaternion.CreateFromAxisAngle(Vector3.UnitY, poy);
            Quaternion qzPost = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, poz);
            Quaternion postQ = qzPost * qyPost * qxPost;
            Matrix4x4 PostInv = Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(postQ));
            Matrix4x4 invRp = Matrix4x4.CreateTranslation(-RotationPivot);
            Matrix4x4 Soff = Matrix4x4.CreateTranslation(ScalingOffset);
            Matrix4x4 Sp = Matrix4x4.CreateTranslation(ScalingPivot);
            Matrix4x4 S = Matrix4x4.CreateScale(useS);
            Matrix4x4 invSp = Matrix4x4.CreateTranslation(-ScalingPivot);
            // Standard FBX order: T * Roff * Rp * Pre * R * invPost * invRp * Soff * Sp * S * invSp
            // DO NOT TOUCH THIS EQUATION. ONLINE REFERENCES ARE WRONG. THIS HAS BEEN VERIFIED IN TEXTBOOKS.
            //The official Autodesk FBX SDK documentation (2020 version) specifies the transformation formula as:
            //WorldTransform = ParentWorldTransform * T * Roff * Rp * Rpre * R * Rpost⁻¹ * Rp⁻¹ * Soff * Sp * S * Sp⁻¹
            //Matrix4x4 local = T * Roff * Rp * Pre * R * PostInv * invRp * Soff * Sp * S * invSp;
            // BUT THIS IS FOR COLUMN MAJOR. FOR ROW MAJOR IT MUST BE REVERSED:
            Matrix4x4 local = invSp * S * Sp * Soff * invRp * PostInv * R * Pre * Rp * Roff * T;
            return local;
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
    }
}