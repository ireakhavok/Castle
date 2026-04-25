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
        private Vector3 _lastBlendParams = Vector3.Zero;

        public AnimationSystem(IGameServer server) : base(server) { }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _server.GetEntities())
            {
                var modelComp = entity.GetComponent<ModelComponent>();
                var blendComp = entity.GetComponent<BlendedAnimationComponent>();
                if (modelComp != null && modelComp.Model != null && modelComp.Model.Skeleton != null && blendComp != null && blendComp.Pack != null)
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
                return; // last pose already stored in modelComp
            }

            // Cheap early-out: skip expensive blend if params haven't moved meaningfully
            float paramDelta = Vector3.Distance(blendComp.CurrentBlendParams, _lastBlendParams);
            if (paramDelta < 0.001f)
            {
                return;
            }
            _lastBlendParams = blendComp.CurrentBlendParams;

            blendComp.GlobalTime += deltaTime * blendComp.MasterSpeed;

            var stack = blendComp.Pack.CreateBlendStack();
            var params3D = blendComp.CurrentBlendParams;
            var blendedLocals = stack.ComputeBlendedLocals(params3D, deltaTime, blendComp.Playing, modelComp.Model);
            if (blendedLocals == null) return;

            var globals = modelComp.Model.Skeleton.ComputeGlobalTransforms(blendedLocals);
            modelComp.NormalBoneTransforms = new Matrix3x3[globals.Length];
            for (int i = 0; i < globals.Length; i++)
            {
                if (Matrix4x4.Invert(globals[i], out Matrix4x4 inv))
                {
                    Matrix4x4 invT = Matrix4x4.Transpose(inv);
                    modelComp.NormalBoneTransforms[i] = new Matrix3x3(
                        invT.M11, invT.M12, invT.M13,
                        invT.M21, invT.M22, invT.M23,
                        invT.M31, invT.M32, invT.M33);
                }
            }
        }
    }
}