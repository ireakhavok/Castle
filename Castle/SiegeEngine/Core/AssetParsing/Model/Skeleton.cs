// Folder: SiegeEngine.Core
// File: AssetParsing/Model/Skeleton.cs
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class Skeleton
    {
        public List<Bone> Bones { get; set; } = new List<Bone>();
        private Matrix4x4[] _currentTransforms;

        public Matrix4x4[] GetTransforms()
        {
            if (_currentTransforms == null)
            {
                _currentTransforms = new Matrix4x4[Bones.Count];
                for (int i = 0; i < Bones.Count; i++)
                    _currentTransforms[i] = Matrix4x4.Identity;
            }
            return _currentTransforms;
        }

        public void UpdateTransforms(Matrix4x4[] transforms)
        {
            _currentTransforms = transforms;
        }

        public Matrix4x4[] ComputeGlobalTransforms(Matrix4x4[] localTransforms)
        {
            var globalTransforms = new Matrix4x4[Bones.Count];
            // Find roots
            for (int i = 0; i < Bones.Count; i++)
            {
                if (Bones[i].ParentIndex == -1)
                {
                    ComputeGlobalRecursive(i, localTransforms, globalTransforms, Matrix4x4.Identity);
                }
            }
            return globalTransforms;
        }

        private void ComputeGlobalRecursive(int idx, Matrix4x4[] localTransforms, Matrix4x4[] globalTransforms, Matrix4x4 parentGlobal)
        {
            globalTransforms[idx] = parentGlobal * localTransforms[idx];
            foreach (var child in Bones[idx].Children)
            {
                int childIdx = Bones.IndexOf(child);
                if (childIdx != -1)
                {
                    ComputeGlobalRecursive(childIdx, localTransforms, globalTransforms, globalTransforms[idx]);
                }
            }
        }

        public Matrix4x4[] ComputeFinalTransforms(Matrix4x4[] globalTransforms)
        {
            var finalTransforms = new Matrix4x4[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
            {
                finalTransforms[i] = globalTransforms[i] * Bones[i].BindPose;
            }
            return finalTransforms;
        }

        public void ComputeBindPoses()
        {
            if (Bones.Count == 0) return;
            var restLocals = Bones.Select(b => b.LocalRest).ToArray();
            var restGlobals = ComputeGlobalTransforms(restLocals);
            for (int i = 0; i < Bones.Count; i++)
            {
                if (!Matrix4x4.Invert(restGlobals[i], out Matrix4x4 invRestGlobal))
                {
                    Bones[i].BindPose = Matrix4x4.Identity;
                    continue;
                }
                Bones[i].BindPose = invRestGlobal;
            }
        }
    }
}