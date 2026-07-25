// Folder: SiegeEngine.Systems
// File: AnimationSystem.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace SiegeEngine.Systems
{
    public class AnimationSystem : GameSystem
    {
        public AnimationSystem(IGameServer server) : base(server)
        {
        }
        public override void Update(float deltaTime)
        {
            foreach (var entity in _server.GetEntities())
            {
                var modelComp = entity.GetComponent<ModelComponent>();
                var blendComp = entity.GetComponent<BlendedAnimationComponent>();
                if (modelComp != null && modelComp.Model != null && modelComp.Model.Skeleton != null &&
                    blendComp != null && blendComp.Pack != null)
                {
                    UpdateBlendedAnimation(blendComp, modelComp, deltaTime);
                }
            }
        }
        private void UpdateBlendedAnimation(BlendedAnimationComponent blendComp, ModelComponent modelComp, float deltaTime)
        {
            if (!blendComp.Playing || blendComp.Pack == null) return;
            // MMO performance: static characters skip blending entirely
            if (blendComp.IsStatic)
            {
                return;
            }
            // Always advance while Playing so LocalTime progresses for idle and continuous locomotion
            blendComp.GlobalTime += deltaTime * blendComp.MasterSpeed;

            // Own a long-lived stack so LocalTime advances continuously.
            // CreateBlendStack deep-copies once; subsequent frames reuse the same instance.
            if (blendComp.RuntimeStack == null)
            {
                blendComp.RuntimeStack = blendComp.Pack.CreateBlendStack();
            }
            var stack = blendComp.RuntimeStack;
            var params3D = blendComp.CurrentBlendParams;

            // ComputeBlendedLocals is contractually non-null when model.Skeleton exists.
            // Always write the resulting matrices; residual locomotion matrices are architecturally impossible.
            var blendedLocals = stack.ComputeBlendedLocals(params3D, deltaTime, blendComp.Playing, modelComp.Model);
            var globals = modelComp.Model.Skeleton.ComputeGlobalTransforms(blendedLocals);
            int boneCount = modelComp.Model.Skeleton.Bones.Count;
            modelComp.BoneMatrices = new Matrix4x4[boneCount];
            modelComp.NormalBoneTransforms = new Matrix3x3[boneCount];
            // Exact pipeline from ModelViewerScene (BindPose * global)
            for (int i = 0; i < boneCount; i++)
            {
                modelComp.BoneMatrices[i] = modelComp.Model.Skeleton.Bones[i].BindPose * globals[i];
                if (Matrix4x4.Invert(modelComp.BoneMatrices[i], out Matrix4x4 inv))
                {
                    Matrix4x4 invT = Matrix4x4.Transpose(inv);
                    modelComp.NormalBoneTransforms[i] = new Matrix3x3(
                        invT.M11, invT.M12, invT.M13,
                        invT.M21, invT.M22, invT.M23,
                        invT.M31, invT.M32, invT.M33);
                }
                else
                {
                    modelComp.NormalBoneTransforms[i] = Matrix3x3.Identity;
                }
            }
        }
    }
}