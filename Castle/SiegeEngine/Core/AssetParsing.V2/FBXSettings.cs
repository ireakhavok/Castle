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
        /// <summary>
        /// Maps source FBX axis indices to target engine axes.
        /// Index 0: Which source axis maps to engine X (0=X, 1=Y, 2=Z in source).
        /// Index 1: Which source axis maps to engine Y.
        /// Index 2: Which source axis maps to engine Z.
        /// Example: {0, 2, 1} means source X -> engine X, source Z -> engine Y, source Y -> engine Z.
        /// </summary>
        public int[] AxisMapping { get; set; } = new int[3] { 0, 1, 2 };
        /// <summary>
        /// Sign flips applied to each source axis value before mapping (1 = no flip, -1 = flip).
        /// Index corresponds to source axis: 0 for source X, 1 for source Y, 2 for source Z.
        /// Used to handle handedness differences (e.g., flip Y for forward direction).
        /// </summary>
        public int[] AxisSigns { get; set; } = new int[3] { 1, -1, 1 };
        public int[] InternalAxisMapping { get; set; } = new int[3] { 0, 1, 2 };
        public int[] InternalAxisSigns { get; set; } = new int[3] { 1, 1, 1 };
        public bool ImportMesh { get; set; } = true;
        public bool ImportArmature { get; set; } = false;
        public bool ImportAnimations { get; set; } = true;
        // Additional dynamic settings for coordinate frames, etc., can be added
        public static int EngineUpAxis = 2;
        public static int EngineUpSign = 1;
        public static int EngineFrontAxis = 1;
        public static int EngineFrontSign = -1;
        public static int EngineCoordAxis = 0;
        public static int EngineCoordSign = 1;
        public static (int[] mapping, int[] signs) DetectAxes(int upAxis, int upSign, int frontAxis, int frontSign, int coordAxis, int coordSign)
        {
            // Blender internal: UpAxis=2 (Z), sign=1; FrontAxis=1 (Y), sign=1; CoordAxis=0 (X), sign=1 (right-handed)
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