using System;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing
{
    public static class FBXCoordinateUtils
    {
        public static Vector3 RemapVector(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            Vector3 result = Vector3.Zero;
            float[] comps = new float[] { v.X, v.Y, v.Z };
            for (int src = 0; src < 3; src++)
            {
                float val = comps[src] * signs[src];
                int tgt = sourceToTarget[src];
                if (tgt == 0) result.X = val;
                else if (tgt == 1) result.Y = val;
                else if (tgt == 2) result.Z = val;
            }
            return result;
        }
        public static Vector3 RemapScale(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            Vector3 result = Vector3.Zero;
            float[] comps = new float[] { v.X, v.Y, v.Z };
            for (int src = 0; src < 3; src++)
            {
                float val = Math.Abs(comps[src]) * Math.Abs(signs[src]);
                int tgt = sourceToTarget[src];
                if (tgt == 0) result.X = val;
                else if (tgt == 1) result.Y = val;
                else if (tgt == 2) result.Z = val;
            }
            return result;
        }
        public static Vector3 RemapRotation(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            Vector3 result = Vector3.Zero;
            float[] comps = new float[] { v.X, v.Y, v.Z };
            for (int src = 0; src < 3; src++)
            {
                float val = comps[src] * signs[src];
                int tgt = sourceToTarget[src];
                if (tgt == 0) result.X = val;
                else if (tgt == 1) result.Y = val;
                else if (tgt == 2) result.Z = val;
            }
            return result;
        }
        public static int RemapRotationOrder(int order, int[] sourceToTarget)
        {
            int[] seq_source = GetOrderSequence(order);
            int[] seq_target = new int[3];
            for (int i = 0; i < 3; i++)
            {
                seq_target[i] = sourceToTarget[seq_source[i]];
            }
            return GetOrderFromSequence(seq_target);
        }
        private static int[] GetOrderSequence(int order)
        {
            switch (order)
            {
                case 0: return new int[] { 0, 1, 2 }; // XYZ
                case 1: return new int[] { 0, 2, 1 }; // XZY
                case 2: return new int[] { 1, 2, 0 }; // YZX
                case 3: return new int[] { 1, 0, 2 }; // YXZ
                case 4: return new int[] { 2, 0, 1 }; // ZXY
                case 5: return new int[] { 2, 1, 0 }; // ZYX
                default: return new int[] { 0, 1, 2 };
            }
        }
        private static int GetOrderFromSequence(int[] seq)
        {
            string s = string.Join("", seq);
            switch (s)
            {
                case "012": return 0;
                case "021": return 1;
                case "120": return 2;
                case "102": return 3;
                case "201": return 4;
                case "210": return 5;
                default: return 0;
            }
        }
        public static float CalculateDeterminant(Matrix4x4 m)
        {
            float a = m.M22 * (m.M33 * m.M44 - m.M34 * m.M43) - m.M23 * (m.M32 * m.M44 - m.M34 * m.M42) + m.M24 * (m.M32 * m.M43 - m.M33 * m.M42);
            float b = m.M21 * (m.M33 * m.M44 - m.M34 * m.M43) - m.M23 * (m.M31 * m.M44 - m.M34 * m.M41) + m.M24 * (m.M31 * m.M43 - m.M33 * m.M41);
            float c = m.M21 * (m.M32 * m.M44 - m.M34 * m.M42) - m.M22 * (m.M31 * m.M44 - m.M34 * m.M41) + m.M24 * (m.M31 * m.M42 - m.M32 * m.M41);
            float d = m.M21 * (m.M32 * m.M43 - m.M33 * m.M42) - m.M22 * (m.M31 * m.M43 - m.M33 * m.M41) + m.M23 * (m.M31 * m.M42 - m.M32 * m.M41);
            return m.M11 * a - m.M12 * b + m.M13 * c - m.M14 * d;
        }
        public static int[] GetInversePermutation(int[] perm)
        {
            int[] inv = new int[perm.Length];
            for (int i = 0; i < perm.Length; i++)
            {
                inv[perm[i]] = i;
            }
            return inv;
        }
        public static Vector3 UnremapVector(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            return RemapVector(v, GetInversePermutation(sourceToTarget), signs);
        }
        public static Vector3 UnremapScale(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            return RemapScale(v, GetInversePermutation(sourceToTarget), signs);
        }
        public static Vector3 UnremapRotation(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            return RemapRotation(v, GetInversePermutation(sourceToTarget), signs);
        }
        public static int UnremapRotationOrder(int order, int[] sourceToTarget)
        {
            return RemapRotationOrder(order, GetInversePermutation(sourceToTarget));
        }
    }
}