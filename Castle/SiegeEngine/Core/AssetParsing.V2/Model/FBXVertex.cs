// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/FBXVertex.cs
using System;

namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class FBXVertex
    {
        public float X, Y, Z;
        public float Nx, Ny, Nz;
        public float U, V;
        public float MatIdx;
        public float Tx, Ty, Tz;
        public int BoneID0, BoneID1, BoneID2, BoneID3;
        public float Weight0, Weight1, Weight2, Weight3;
        // Constructor will be added iteratively
    }
}