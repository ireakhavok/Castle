// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/Skeleton.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.AssetObjects;
namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class Skeleton
    {
        public List<Bone> Bones { get; set; } = new List<Bone>();
        public Matrix4x4[] ComputeGlobalTransforms()
        {
            Matrix4x4[] globals = new Matrix4x4[Bones.Count];
            foreach (var bone in Bones.Where(b => b.ParentIndex == -1))
            {
                ComputeGlobalRecursive(Bones.IndexOf(bone), Matrix4x4.Identity, globals);
            }
            if (Bones.Count > 0)
            {
                FBXParserBase.Log("Global Transforms:");
                for (int i = 0; i < Math.Min(3, Bones.Count); i++)
                {
                    FBXParserBase.Log($"Bone {i} ({Bones[i].Name}) Global:");
                    FBXMeshParser.PrintMatrix(globals[i]);
                }
            }
            return globals;
        }
        private void ComputeGlobalRecursive(int idx, Matrix4x4 parentGlobal, Matrix4x4[] globals)
        {
            globals[idx] = parentGlobal * Bones[idx].LocalRest;
            foreach (var child in Bones[idx].Children)
            {
                int childIdx = Bones.IndexOf(child);
                ComputeGlobalRecursive(childIdx, globals[idx], globals);
            }
        }
    }
}