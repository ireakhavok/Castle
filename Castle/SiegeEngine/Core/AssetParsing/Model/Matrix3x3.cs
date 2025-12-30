using System;
using System.Numerics;
namespace SiegeEngine.Core.AssetParsing.Model
{
    public struct Matrix3x3
    {
        public float M11, M12, M13;
        public float M21, M22, M23;
        public float M31, M32, M33;

        public Matrix3x3(
            float m11, float m12, float m13,
            float m21, float m22, float m23,
            float m31, float m32, float m33)
        {
            M11 = m11; M12 = m12; M13 = m13;
            M21 = m21; M22 = m22; M23 = m23;
            M31 = m31; M32 = m32; M33 = m33;
        }

        public Matrix3x3 Transpose()
        {
            return new Matrix3x3(
                M11, M21, M31,
                M12, M22, M32,
                M13, M23, M33);
        }

        public Matrix3x3 Inverse()
        {
            float det = M11 * (M22 * M33 - M23 * M32) -
                        M12 * (M21 * M33 - M23 * M31) +
                        M13 * (M21 * M32 - M22 * M31);

            if (Math.Abs(det) < 1e-6f)
            {
                return new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1); // Identity as fallback
            }

            float invDet = 1f / det;

            return new Matrix3x3(
                invDet * (M22 * M33 - M23 * M32), invDet * (M13 * M32 - M12 * M33), invDet * (M12 * M23 - M13 * M22),
                invDet * (M23 * M31 - M21 * M33), invDet * (M11 * M33 - M13 * M31), invDet * (M13 * M21 - M11 * M23),
                invDet * (M21 * M32 - M22 * M31), invDet * (M12 * M31 - M11 * M32), invDet * (M11 * M22 - M12 * M21));
        }
    }
}