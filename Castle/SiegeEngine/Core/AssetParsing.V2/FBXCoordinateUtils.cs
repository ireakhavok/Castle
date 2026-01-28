// Folder: SiegeEngine.Core
// File: AssetParsing.V2/FBXCoordinateUtils.cs
using System;
using System.Linq;
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
        public static Vector3 RemapRotation(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            return RemapVector(v, sourceToTarget, signs);
        }
        private static int[][] OrderSequences = new int[][]
        {
            new[] {0,1,2}, // 0: XYZ
            new[] {0,2,1}, // 1: XZY
            new[] {1,2,0}, // 2: YZX
            new[] {1,0,2}, // 3: YXZ
            new[] {2,0,1}, // 4: ZXY
            new[] {2,1,0}  // 5: ZYX
        };
        public static int[] GetOrderSequence(int order)
        {
            if (order < 0 || order > 5) return OrderSequences[0];
            return OrderSequences[order];
        }
        public static int GetOrderFromSequence(int[] seq)
        {
            for (int o = 0; o < 6; o++)
            {
                if (OrderSequences[o].SequenceEqual(seq)) return o;
            }
            return 0;
        }
        public static int RemapRotationOrder(int[] sourceToTarget, int order)
        {
            int[] seq = GetOrderSequence(order);
            int[] remappedSeq = new int[3];
            for (int i = 0; i < 3; i++)
            {
                remappedSeq[i] = Array.IndexOf(sourceToTarget, seq[i]);
            }
            return GetOrderFromSequence(remappedSeq);
        }
    }
}