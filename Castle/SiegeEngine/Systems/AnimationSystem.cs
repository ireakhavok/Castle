// SiegeEngine.Core
// File: Systems/AnimationSystem.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Interfaces;
using System;
using System.Numerics;

namespace SiegeEngine.Systems
{
    // System to update animation components on entities, computing bone transforms each frame.
    public class AnimationSystem : GameSystem
    {
        public AnimationSystem(IGameServer server) : base(server)
        {
        }
        // Updates all animated entities by advancing time, computing local/global/final transforms.
        public override void Update(float deltaTime)
        {
            foreach (var entity in _server.GetEntities())
            {
                var modelComp = entity.GetComponent<ModelComponent>();
                var animComp = entity.GetComponent<AnimationComponent>();
                if (modelComp != null && animComp != null && animComp.Playing && modelComp.Model != null && modelComp.Model.Skeleton != null)
                {
                    animComp.Time += deltaTime;
                    var animation = modelComp.Model.Animations.Find(a => a.Name == animComp.CurrentAnimation);
                    if (animation != null)
                    {
                        var localTransforms = new Matrix4x4[modelComp.Model.Skeleton.Bones.Count]; // Assume GetBoneTransforms or similar
                        // Note: V2 Animation may need method to compute locals
                        // Placeholder: Fill localTransforms appropriately
                        var globalTransforms = modelComp.Model.Skeleton.ComputeGlobalTransforms(localTransforms);
                        var normalTransforms = new Matrix3x3[globalTransforms.Length];
                        for (int i = 0; i < globalTransforms.Length; i++)
                        {
                            Matrix4x4 mat = globalTransforms[i];
                            normalTransforms[i] = new Matrix3x3(
                                mat.M11, mat.M12, mat.M13,
                                mat.M21, mat.M22, mat.M23,
                                mat.M31, mat.M32, mat.M33
                            ).Inverse();
                        }
                        modelComp.NormalBoneTransforms = normalTransforms;
                    }
                }
            }
        }
    }
}