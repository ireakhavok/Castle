using SiegeEngine.Core.AssetParsing.Model;
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
            blendComp.GlobalTime += deltaTime * blendComp.MasterSpeed;
            var stack = blendComp.Pack.CreateBlendStack();
            var params3D = blendComp.CurrentBlendParams;
            var blendedLocals = stack.ComputeBlendedLocals(params3D, deltaTime, blendComp.Playing, modelComp.Model);
            if (blendedLocals == null) return; // single-clip or edge case handled by caller if needed
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