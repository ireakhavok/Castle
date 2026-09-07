// Folder: SiegeEngine.Core.AssetParsing.Model
// File: Skeleton.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.AssetObjects;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class Skeleton
    {
        public List<Bone> Bones { get; set; } = new List<Bone>();

        private int[] _rootIndices;
        private int[][] _childIndices;
        private int _hierarchyBoneCount = -1;

        public Matrix4x4[] ComputeGlobalTransforms(Matrix4x4[] localTransforms)
        {
            return ComputeGlobalTransforms(localTransforms, null);
        }

        public Matrix4x4[] ComputeGlobalTransforms()
        {
            return ComputeGlobalTransforms(null, null);
        }

        public Matrix4x4[] ComputeGlobalTransforms(Matrix4x4[] localTransforms, Matrix4x4[] dest)
        {
            int n = Bones.Count;
            if (dest == null || dest.Length != n)
                dest = new Matrix4x4[n];
            EnsureHierarchy();
            bool animated = localTransforms != null && localTransforms.Length == n;
            for (int r = 0; r < _rootIndices.Length; r++)
                ComputeGlobalRecursive(_rootIndices[r], Matrix4x4.Identity, dest, animated ? localTransforms : null);
            return dest;
        }

        private void EnsureHierarchy()
        {
            int n = Bones.Count;
            if (_rootIndices != null && _childIndices != null && _hierarchyBoneCount == n && _childIndices.Length == n)
                return;
            _hierarchyBoneCount = n;
            int[] childCounts = n == 0 ? Array.Empty<int>() : new int[n];
            int rootCount = 0;
            for (int i = 0; i < n; i++)
            {
                int p = Bones[i].ParentIndex;
                if (p < 0 || p >= n)
                    rootCount++;
                else
                    childCounts[p]++;
            }
            _rootIndices = new int[rootCount];
            _childIndices = new int[n][];
            int[] fill = n == 0 ? Array.Empty<int>() : new int[n];
            for (int i = 0; i < n; i++)
                _childIndices[i] = childCounts[i] == 0 ? Array.Empty<int>() : new int[childCounts[i]];
            int ri = 0;
            for (int i = 0; i < n; i++)
            {
                int p = Bones[i].ParentIndex;
                if (p < 0 || p >= n)
                    _rootIndices[ri++] = i;
                else
                    _childIndices[p][fill[p]++] = i;
            }
        }

        private void ComputeGlobalRecursive(int idx, Matrix4x4 parentGlobal, Matrix4x4[] globals, Matrix4x4[] localTransforms)
        {
            var bone = Bones[idx];
            Matrix4x4 local = localTransforms != null ? localTransforms[idx] : bone.LocalRest;
            Matrix4x4 childGlobal;

            bool parentOk = Matrix4x4.Decompose(parentGlobal, out Vector3 parentScale, out Quaternion parentRot, out Vector3 parentTrans);
            bool localOk = Matrix4x4.Decompose(local, out Vector3 childScale, out Quaternion childRot, out Vector3 childTrans);

            // If either decompose fails we refuse to collapse the hierarchy to Identity for a frame.
            // Fall back to the simple, always-stable matrix multiply. This removes the rare one-frame hitch
            // while preserving the full InheritType path for the vast majority of frames.
            if (!parentOk || !localOk)
            {
                childGlobal = local * parentGlobal;
            }
            else
            {
                Matrix4x4 parentR = Matrix4x4.CreateFromQuaternion(parentRot);
                Matrix4x4 parentT = Matrix4x4.CreateTranslation(parentTrans);
                Matrix4x4 parentS = Matrix4x4.CreateScale(parentScale);

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
            }

            childGlobal = childGlobal * bone.GeometricTransform;
            globals[idx] = childGlobal;

            int[] children = _childIndices[idx];
            for (int c = 0; c < children.Length; c++)
                ComputeGlobalRecursive(children[c], childGlobal, globals, localTransforms);
        }

        public void LogBoneHierarchy()
        {
            EnsureHierarchy();
            FBXParserBase.Log("Bone Hierarchy:");
            for (int r = 0; r < _rootIndices.Length; r++)
            {
                LogBoneHierarchy(_rootIndices[r], 0);
            }
        }

        private void LogBoneHierarchy(int idx, int level)
        {
            var bone = Bones[idx];
            string indent = new string(' ', level * 2);
            FBXParserBase.Log($"{indent}Bone {idx}: {bone.Name}, ParentIndex={bone.ParentIndex}, LocalRest Translation={bone.LocalRest.Translation}");
            int[] children = _childIndices[idx];
            for (int c = 0; c < children.Length; c++)
                LogBoneHierarchy(children[c], level + 1);
        }
    }
}
