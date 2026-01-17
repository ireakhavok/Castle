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
            float[] components = new float[] { v.X, v.Y, v.Z };
            float newX = signs[sourceToTarget[0]] * components[sourceToTarget[0]];
            float newY = signs[sourceToTarget[1]] * components[sourceToTarget[1]];
            float newZ = signs[sourceToTarget[2]] * components[sourceToTarget[2]];
            return new Vector3(newX, newY, newZ);
        }
        // Utils for coordinate remapping will be added iteratively
    }
}