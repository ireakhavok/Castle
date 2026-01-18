// Folder: SiegeEngine.Core
// File: AssetParsing.V2/FBXCoordinateUtils.cs
using System;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing.V2
{
    public static class FBXCoordinateUtils
    {
        public static Vector3 RemapVector(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            float[] comp = { v.X * signs[0], v.Y * signs[1], v.Z * signs[2] };
            return new Vector3(comp[sourceToTarget[0]], comp[sourceToTarget[1]], comp[sourceToTarget[2]]);
        }

        public static Matrix4x4 RemapMatrix(Matrix4x4 m, int[] sourceToTarget, int[] signs)
        {
            Matrix4x4 p = new Matrix4x4();
            for (int t = 0; t < 3; t++)
            {
                int s = sourceToTarget[t];
                float sign = signs[s];
                if (t == 0)
                {
                    if (s == 0) p.M11 = sign;
                    if (s == 1) p.M12 = sign;
                    if (s == 2) p.M13 = sign;
                }
                else if (t == 1)
                {
                    if (s == 0) p.M21 = sign;
                    if (s == 1) p.M22 = sign;
                    if (s == 2) p.M23 = sign;
                }
                else if (t == 2)
                {
                    if (s == 0) p.M31 = sign;
                    if (s == 1) p.M32 = sign;
                    if (s == 2) p.M33 = sign;
                }
            }
            p.M44 = 1;
            Matrix4x4.Invert(p, out var invP);
            return p * m * invP;
        }
    }
}