// Folder: SiegeEngine
// File: Core/AssetParsing/Model/Bone.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
namespace SiegeEngine.Core.AssetParsing.Model
{
    public class Bone
    {
        public string Name { get; set; }
        public Matrix4x4 BindPose { get; set; }
        public int ParentIndex { get; set; }
        public Matrix4x4 LocalRest { get; set; } = Matrix4x4.Identity;
        public Vector3 LclTranslation { get; set; } = Vector3.Zero;
        private Vector3 _lclRotationDegrees = Vector3.Zero;
        public Vector3 LclRotationDegrees
        {
            get => _lclRotationDegrees;
            set
            {
                _lclRotationDegrees = value;
                _lclRotationRadians = value * (MathF.PI / 180f);
            }
        }
        public Vector3 LclRotationRadians
        {
            get => _lclRotationRadians;
            set
            {
                _lclRotationRadians = value;
                _lclRotationDegrees = value * (180f / MathF.PI);
            }
        }
        private Vector3 _lclRotationRadians = Vector3.Zero;
        public Vector3 LclScaling { get; set; } = Vector3.One;
        private Vector3 _preRotationDegrees = Vector3.Zero;
        public Vector3 PreRotationDegrees
        {
            get => _preRotationDegrees;
            set
            {
                _preRotationDegrees = value;
                _preRotationRadians = value * (MathF.PI / 180f);
            }
        }
        public Vector3 PreRotationRadians
        {
            get => _preRotationRadians;
            set
            {
                _preRotationRadians = value;
                _preRotationDegrees = value * (180f / MathF.PI);
            }
        }
        private Vector3 _preRotationRadians = Vector3.Zero;
        private Vector3 _postRotationDegrees = Vector3.Zero;
        public Vector3 PostRotationDegrees
        {
            get => _postRotationDegrees;
            set
            {
                _postRotationDegrees = value;
                _postRotationRadians = value * (MathF.PI / 180f);
            }
        }
        public Vector3 PostRotationRadians
        {
            get => _postRotationRadians;
            set
            {
                _postRotationRadians = value;
                _postRotationDegrees = value * (180f / MathF.PI);
            }
        }
        private Vector3 _postRotationRadians = Vector3.Zero;
        public Vector3 RotationPivot { get; set; } = Vector3.Zero;
        public Vector3 RotationOffset { get; set; } = Vector3.Zero;
        public Vector3 ScalingPivot { get; set; } = Vector3.Zero;
        public Vector3 ScalingOffset { get; set; } = Vector3.Zero;
        public int RotationOrder { get; set; } = 0; // Default eEulerXYZ
        public string BoneType { get; set; }
        public float Size { get; set; } = 1f;
        private Vector3 _geometricRotationDegrees = Vector3.Zero;
        public Vector3 GeometricRotationDegrees
        {
            get => _geometricRotationDegrees;
            set
            {
                _geometricRotationDegrees = value;
                _geometricRotationRadians = value * (MathF.PI / 180f);
            }
        }
        public Vector3 GeometricRotationRadians
        {
            get => _geometricRotationRadians;
            set
            {
                _geometricRotationRadians = value;
                _geometricRotationDegrees = value * (180f / MathF.PI);
            }
        }
        private Vector3 _geometricRotationRadians = Vector3.Zero;
        public Vector3 GeometricTranslation { get; set; } = Vector3.Zero;
        public Vector3 GeometricScaling { get; set; } = Vector3.One;
        public Matrix4x4 Geo => Matrix4x4.CreateScale(GeometricScaling) *
            CreateFromEuler(GeometricRotationDegrees, 0) *
            Matrix4x4.CreateTranslation(GeometricTranslation);
        public Matrix4x4 ComputeLocal(Vector3? t = null, Vector3? r = null, Vector3? s = null)
        {
            Vector3 useT = t ?? LclTranslation;
            Vector3 useR = r ?? LclRotationDegrees; // Use degrees internally
            Vector3 useS = s ?? LclScaling;
            Matrix4x4 T = Matrix4x4.CreateTranslation(useT);
            Matrix4x4 Roff = Matrix4x4.CreateTranslation(RotationOffset);
            Matrix4x4 Rp = Matrix4x4.CreateTranslation(RotationPivot);
            Matrix4x4.Invert(Rp, out Matrix4x4 invRp); // More robust than negation
            Matrix4x4 Soff = Matrix4x4.CreateTranslation(ScalingOffset);
            Matrix4x4.Invert(Soff, out Matrix4x4 invSoff); // More robust than negation
            Matrix4x4 Sp = Matrix4x4.CreateTranslation(ScalingPivot);
            Matrix4x4.Invert(Sp, out Matrix4x4 invSp); // More robust than negation
            Matrix4x4 S = Matrix4x4.CreateScale(useS);
            Matrix4x4 Pre = CreateFromEuler(PreRotationDegrees, 0); // Always XYZ
            Matrix4x4 R = CreateFromEuler(useR, RotationOrder);
            Matrix4x4 Post = CreateFromEuler(PostRotationDegrees, 0); // Always XYZ
            Matrix4x4.Invert(Post, out Matrix4x4 invPost); // More robust than negation
            // Standard FBX order: T * Roff * Rp * Pre * R * Post * inv(Rp) * Soff * Sp * S * inv(Sp) 
            // originally was the values listed above. after looking at autodesk FBX below is accurate
            Matrix4x4 local = T * Roff * Rp * Pre * R * invPost * invRp * Soff * Sp * S * invSp;
            return local;
        }
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
                    return mz * my * mx;
            }
        }
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