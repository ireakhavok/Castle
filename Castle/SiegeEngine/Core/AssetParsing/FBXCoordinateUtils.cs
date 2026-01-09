// Folder: SiegeEngine.Core
// File: AssetParsing/FBXCoordinateUtils.cs
using System;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing
{
    // This static class provides utility methods for remapping coordinates, scales, rotations, and rotation orders
    // between different coordinate systems, typically from FBX source to engine target system.
    // It handles axis permutations and sign flips to convert between systems like Y-up to Z-up.
    public static class FBXCoordinateUtils
    {
        // Remaps a vector (like position or translation) from source axes to target axes with sign adjustments.
        // sourceToTarget: permutation array where index is source axis (0=X,1=Y,2=Z), value is target axis.
        // signs: sign multipliers for each source axis.
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

        // Remaps a scale vector, using absolute values for magnitudes since scales are positive,
        // but applies absolute signs (as scales don't have direction like vectors).
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

        // Remaps a rotation vector (Euler angles in degrees) with sign adjustments.
        // Rotations require careful handling as axis flips can reverse rotation directions.
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

        // Remaps the Euler rotation order by permuting the sequence based on axis mapping.
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

        // Returns the axis sequence for a given rotation order (0=XYZ, 1=XZY, etc.).
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

        // Converts an axis sequence back to a rotation order index.
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

        // Computes the determinant of a 4x4 matrix, used potentially for handedness checks.
        public static float CalculateDeterminant(Matrix4x4 m)
        {
            float a = m.M22 * (m.M33 * m.M44 - m.M34 * m.M43) - m.M23 * (m.M32 * m.M44 - m.M34 * m.M42) + m.M24 * (m.M32 * m.M43 - m.M33 * m.M42);
            float b = m.M21 * (m.M33 * m.M44 - m.M34 * m.M43) - m.M23 * (m.M31 * m.M44 - m.M34 * m.M41) + m.M24 * (m.M31 * m.M43 - m.M33 * m.M41);
            float c = m.M21 * (m.M32 * m.M44 - m.M34 * m.M42) - m.M22 * (m.M31 * m.M44 - m.M34 * m.M41) + m.M24 * (m.M31 * m.M42 - m.M32 * m.M41);
            float d = m.M21 * (m.M32 * m.M43 - m.M33 * m.M42) - m.M22 * (m.M31 * m.M43 - m.M33 * m.M41) + m.M23 * (m.M31 * m.M42 - m.M32 * m.M41);
            return m.M11 * a - m.M12 * b + m.M13 * c - m.M14 * d;
        }

        // Computes the inverse permutation array for reversing the axis mapping.
        public static int[] GetInversePermutation(int[] perm)
        {
            int[] inv = new int[perm.Length];
            for (int i = 0; i < perm.Length; i++)
            {
                inv[perm[i]] = i;
            }
            return inv;
        }

        // Unremaps a vector by applying the inverse permutation (target to source).
        public static Vector3 UnremapVector(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            return RemapVector(v, GetInversePermutation(sourceToTarget), signs);
        }

        // Unremaps a scale using inverse permutation.
        public static Vector3 UnremapScale(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            return RemapScale(v, GetInversePermutation(sourceToTarget), signs);
        }

        // Unremaps a rotation using inverse permutation.
        public static Vector3 UnremapRotation(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            return RemapRotation(v, GetInversePermutation(sourceToTarget), signs);
        }

        // Unremaps the rotation order using inverse permutation.
        public static int UnremapRotationOrder(int order, int[] sourceToTarget)
        {
            return RemapRotationOrder(order, GetInversePermutation(sourceToTarget));
        }
    }
}