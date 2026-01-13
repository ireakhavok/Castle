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
    // Represents a skeleton with bones, computes hierarchical transforms (local to global to final skinning matrices).
    public class Skeleton
    {
        public List<Bone> Bones { get; set; } = new List<Bone>();
        private Matrix4x4[] _currentTransforms;
        // Gets current final transforms for skinning, initializes if null.
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
        // Sets the current final transforms.
        public void UpdateTransforms(Matrix4x4[] transforms)
        {
            _currentTransforms = transforms;
        }
        // Computes global transforms from local ones, starting from roots.
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
        // Recursive helper to compute global transform for a bone and its children.
        private void ComputeGlobalRecursive(int idx, Matrix4x4[] localTransforms, Matrix4x4[] globalTransforms, Matrix4x4 parentGlobal)
        {
            globalTransforms[idx] = localTransforms[idx] * parentGlobal;
            foreach (var child in Bones[idx].Children)
            {
                int childIdx = Bones.IndexOf(child);
                if (childIdx != -1)
                {
                    ComputeGlobalRecursive(childIdx, localTransforms, globalTransforms, globalTransforms[idx]);
                }
            }
        }
        // Computes final skinning matrices: global * bindPose (inverse bind pose actually, but named BindPose).
        public Matrix4x4[] ComputeFinalTransforms(Matrix4x4[] globalTransforms)
        {
            var finalTransforms = new Matrix4x4[Bones.Count];
            int unmappedCount = 0;
            for (int i = 0; i < Bones.Count; i++)
            {
                finalTransforms[i] = globalTransforms[i] * Bones[i].BindPose;
                if (finalTransforms[i] == Matrix4x4.Identity)
                    unmappedCount++;
            }
            if (unmappedCount > 0)
            {
                Console.WriteLine($"Skeleton: {unmappedCount}/{Bones.Count} bones have identity final transforms - check mapping/skinning");
            }
            return finalTransforms;
        }
        // Computes inverse bind poses as inverse of rest global transforms.
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
        // Computes local transforms from global ones by inverting parent globals.
        public Matrix4x4[] ComputeLocalsFromGlobals(Matrix4x4[] globals)
        {
            var locals = new Matrix4x4[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
            {
                if (Bones[i].ParentIndex == -1)
                {
                    ComputeLocalsRecursive(i, globals, locals, Matrix4x4.Identity);
                }
            }
            return locals;
        }
        // Recursive helper to compute local transform for a bone and its children.
        private void ComputeLocalsRecursive(int idx, Matrix4x4[] globals, Matrix4x4[] locals, Matrix4x4 parentGlobal)
        {
            if (Matrix4x4.Invert(parentGlobal, out var invParent))
            {
                locals[idx] = globals[idx] * invParent;
            }
            else
            {
                locals[idx] = globals[idx];
            }
            foreach (var child in Bones[idx].Children)
            {
                int childIdx = Bones.IndexOf(child);
                if (childIdx != -1)
                {
                    ComputeLocalsRecursive(childIdx, globals, locals, globals[idx]);
                }
            }
        }
    }
}