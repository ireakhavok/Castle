
// Folder: SiegeEngine.Core
// File: AssetParsing/Model/Animation.cs
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing.Model
{
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
                bool lowerDecomposed = DecomposeRobust(lower.BoneTransforms[b], out Vector3 lScale, out Quaternion lRot, out Vector3 lTrans);
                bool upperDecomposed = DecomposeRobust(upper.BoneTransforms[b], out Vector3 uScale, out Quaternion uRot, out Vector3 uTrans);
                if (lowerDecomposed && upperDecomposed)
                {
                    Vector3 iTrans = Vector3.Lerp(lTrans, uTrans, factor);
                    float dot = Quaternion.Dot(lRot, uRot);
                    if (dot < 0) uRot = -uRot;
                    Quaternion iRot = Quaternion.Normalize(Quaternion.Slerp(Quaternion.Normalize(lRot), Quaternion.Normalize(uRot), factor));
                    Vector3 iScale = Vector3.Lerp(lScale, uScale, factor);
                    interpolated[b] = Matrix4x4.CreateScale(iScale) * Matrix4x4.CreateFromQuaternion(iRot) * Matrix4x4.CreateTranslation(iTrans);
                }
                else
                {
                    interpolated[b] = lower.BoneTransforms[b];
                }
            }
            return interpolated;
        }
        private static bool DecomposeRobust(Matrix4x4 matrix, out Vector3 scale, out Quaternion rotation, out Vector3 translation)
        {
            translation = new Vector3(matrix.M41, matrix.M42, matrix.M43);
            var c0 = new Vector3(matrix.M11, matrix.M12, matrix.M13);
            var c1 = new Vector3(matrix.M21, matrix.M22, matrix.M23);
            var c2 = new Vector3(matrix.M31, matrix.M32, matrix.M33);
            float det = MatrixDeterminant(new Matrix3x3(c0.X, c0.Y, c0.Z, c1.X, c1.Y, c1.Z, c2.X, c2.Y, c2.Z));
            if (det < 0)
            {
                c2 = -c2;
            }
            float sx = c0.Length();
            float sy = c1.Length();
            float sz = c2.Length();
            scale = new Vector3(sx, sy, sz);
            if (Math.Abs(sx) < 1e-6f || Math.Abs(sy) < 1e-6f || Math.Abs(sz) < 1e-6f)
            {
                rotation = Quaternion.Identity;
                return false; // Singular matrix
            }
            // Normalize columns to get rotation matrix
            c0 /= sx;
            c1 /= sy;
            c2 /= sz;
            // Create rotation matrix
            Matrix4x4 rotMatrix = new Matrix4x4(c0.X, c0.Y, c0.Z, 0,
                                                c1.X, c1.Y, c1.Z, 0,
                                                c2.X, c2.Y, c2.Z, 0,
                                                0, 0, 0, 1);
            // Convert to quaternion
            rotation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rotMatrix));
            return true;
        }
        private struct Matrix3x3
        {
            public float M11, M12, M13;
            public float M21, M22, M23;
            public float M31, M32, M33;
            public Matrix3x3(float m11, float m12, float m13, float m21, float m22, float m23, float m31, float m32, float m33)
            {
                M11 = m11; M12 = m12; M13 = m13;
                M21 = m21; M22 = m22; M23 = m23;
                M31 = m31; M32 = m32; M33 = m33;
            }
        }
        private static float MatrixDeterminant(Matrix3x3 m)
        {
            return m.M11 * (m.M22 * m.M33 - m.M23 * m.M32) -
                   m.M12 * (m.M21 * m.M33 - m.M23 * m.M31) +
                   m.M13 * (m.M21 * m.M32 - m.M22 * m.M31);
        }
    }
}
