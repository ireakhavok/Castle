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
            var bone = Bones[idx];
            Matrix4x4 local = bone.LocalRest;
            Matrix4x4 childGlobal;
            if (!Matrix4x4.Decompose(parentGlobal, out Vector3 parentScale, out Quaternion parentRot, out Vector3 parentTrans))
            {
                parentScale = Vector3.One;
                parentRot = Quaternion.Identity;
                parentTrans = Vector3.Zero;
            }
            Matrix4x4 parentR = Matrix4x4.CreateFromQuaternion(parentRot);
            Matrix4x4 parentT = Matrix4x4.CreateTranslation(parentTrans);
            Matrix4x4 parentS = Matrix4x4.CreateScale(parentScale);
            if (!Matrix4x4.Decompose(local, out Vector3 childScale, out Quaternion childRot, out Vector3 childTrans))
            {
                childScale = Vector3.One;
                childRot = Quaternion.Identity;
                childTrans = Vector3.Zero;
            }
            Matrix4x4 childR = Matrix4x4.CreateFromQuaternion(childRot);
            Matrix4x4 childT = Matrix4x4.CreateTranslation(childTrans);
            Matrix4x4 childS = Matrix4x4.CreateScale(childScale);
            switch (bone.InheritType)
            {
                case 0: // eInheritRrSs
                    childGlobal = childS * parentS * childR * childT * parentR * parentT;
                    break;
                case 1: // eInheritRSrs
                    childGlobal = childS * childR * childT * parentS * parentR * parentT;
                    break;
                case 2: // eInheritRrs
                    childGlobal = childS * childR * childT * parentR * parentT;
                    break;
                default:
                    childGlobal = local * parentGlobal;
                    break;
            }
            globals[idx] = childGlobal;
            foreach (var child in bone.Children)
            {
                int childIdx = Bones.IndexOf(child);
                ComputeGlobalRecursive(childIdx, childGlobal, globals);
            }
        }
        public void LogBoneHierarchy()
        {
            FBXParserBase.Log("Bone Hierarchy:");
            foreach (var bone in Bones.Where(b => b.ParentIndex == -1))
            {
                LogBoneHierarchy(Bones, bone, 0);
            }
        }
        public void LogBoneHierarchy(List<Bone> bones, Bone bone, int level)
        {
            string indent = new string(' ', level * 2);
            int idx = bones.IndexOf(bone);
            FBXParserBase.Log($"{indent}Bone {idx}: {bone.Name}, ParentIndex={bone.ParentIndex}, LocalRest Translation={bone.LocalRest.Translation}");
            foreach (var child in bone.Children)
            {
                LogBoneHierarchy(bones, child, level + 1);
            }
        }
    }
}