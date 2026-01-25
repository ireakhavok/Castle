// Folder: SiegeEngine.Core
// File: AssetParsing.V2/FBXSettings.cs
using System;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing.V2
{
    public class FBXSettings
    {
        public float ModelScale { get; set; } = 1f;
        public Matrix4x4 P4 { get; set; } = Matrix4x4.Identity;
        public Matrix4x4 InvP4 { get; set; } = Matrix4x4.Identity;
        public int[] AxisMapping { get; set; } = new int[3] { 0, 1, 2 };
        public int[] AxisSigns { get; set; } = new int[3] { 1, 1, 1 };
        public bool ImportMesh { get; set; } = true;
        public bool ImportArmature { get; set; } = false;
        public bool ImportAnimations { get; set; } = true;

        public static int EngineUpAxis = 2; // Z
        public static int EngineUpSign = 1;
        public static int EngineFrontAxis = 1; // Y
        public static int EngineFrontSign = -1; // Negative-Y forward
        public static int EngineCoordAxis = 0; // X
        public static int EngineCoordSign = 1;

        public static (int[] mapping, int[] signs) DetectAxes(int upAxis, int upSign, int frontAxis, int frontSign, int coordAxis, int coordSign)
        {
            int[] mapping = new int[3];
            mapping[EngineCoordAxis] = coordAxis;
            mapping[EngineFrontAxis] = frontAxis;
            mapping[EngineUpAxis] = upAxis;

            int[] signs = new int[3] { 1, 1, 1 };
            signs[coordAxis] = coordSign * EngineCoordSign;
            signs[frontAxis] = frontSign * EngineFrontSign;
            signs[upAxis] = upSign * EngineUpSign;

            return (mapping, signs);
        }
    }
}