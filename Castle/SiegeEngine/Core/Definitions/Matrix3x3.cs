// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/Matrix3x3.cs
using System;
using System.Numerics;
namespace SiegeEngine.Core.Definitions;

public class Matrix3x3
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

    public Matrix3x3(Matrix4x4 m)
    {
        M11 = m.M11; M12 = m.M12; M13 = m.M13;
        M21 = m.M21; M22 = m.M22; M23 = m.M23;
        M31 = m.M31; M32 = m.M32; M33 = m.M33;
    }
    public static Matrix3x3 Identity { get; } = new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);
    public static Matrix3x3 Transpose(Matrix3x3 m)
    {
        return new Matrix3x3(
            m.M11, m.M21, m.M31,
            m.M12, m.M22, m.M32,
            m.M13, m.M23, m.M33);
    }
    public static Matrix3x3 Transpose(float m11, float m12, float m13,
        float m21, float m22, float m23,
        float m31, float m32, float m33)
    {
        return new Matrix3x3(
            m11, m21, m31,
            m12, m22, m32,
            m13, m23, m33);
    }
    public Matrix3x3 Inverse()
    {
        float det = M11 * (M22 * M33 - M23 * M32) -
                    M12 * (M21 * M33 - M23 * M31) +
                    M13 * (M21 * M32 - M22 * M31);

        if (Math.Abs(det) < 1e-6f)
        {
            return Identity; // Identity as fallback
        }

        float invDet = 1f / det;

        return new Matrix3x3(
            invDet * (M22 * M33 - M23 * M32), invDet * (M13 * M32 - M12 * M33), invDet * (M12 * M23 - M13 * M22),
            invDet * (M23 * M31 - M21 * M33), invDet * (M11 * M33 - M13 * M31), invDet * (M13 * M21 - M11 * M23),
            invDet * (M21 * M32 - M22 * M31), invDet * (M12 * M31 - M11 * M32), invDet * (M11 * M22 - M12 * M21));
    }

    public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b)
    {
        return new Matrix3x3(
            a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
            a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
            a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,
            a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
            a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
            a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,
            a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
            a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
            a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33);
    }

    public static Vector3 operator *(Matrix3x3 m, Vector3 v)
    {
        return new Vector3(
            m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z,
            m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z,
            m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z);
    }
}