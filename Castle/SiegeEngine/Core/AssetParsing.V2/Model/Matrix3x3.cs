// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/Matrix3x3.cs
using System;
using System.Numerics;
namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class Matrix3x3
    {
        public float M11, M12, M13;
        public float M21, M22, M23;
        public float M31, M32, M33;
        public Matrix3x3(float m11, float m12, float m13, float m21, float m22, float m23, float m31, float m32, float m33)
        {
            M11 = m11;
            M12 = m12;
            M13 = m13;
            M21 = m21;
            M22 = m22;
            M23 = m23;
            M31 = m31;
            M32 = m32;
            M33 = m33;
        }
        public static Matrix3x3 Identity { get; } = new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);
    }
}