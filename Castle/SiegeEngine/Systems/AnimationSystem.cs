// Folder: SiegeEngine.Core
// File: Systems/AnimationSystem.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Interfaces;
using Silk.NET.Maths;
using System;
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
                var animComp = entity.GetComponent<AnimationComponent>();
                if (modelComp != null && animComp != null && animComp.Playing && modelComp.Model != null && modelComp.Model.Skeleton != null)
                {
                    animComp.Time += deltaTime;
                    var animation = modelComp.Model.Animations.Find(a => a.Name == animComp.CurrentAnimation);
                    if (animation != null)
                    {
                        var localTransforms = animation.GetBoneTransforms(animComp.Time);
                        var globalTransforms = modelComp.Model.Skeleton.ComputeGlobalTransforms(localTransforms);
                        var finalTransforms = modelComp.Model.Skeleton.ComputeFinalTransforms(globalTransforms);
                        var normalTransforms = new Matrix3x3[finalTransforms.Length];
                        for (int i = 0; i < finalTransforms.Length; i++)
                        {
                            Matrix4x4 mat = finalTransforms[i];
                            normalTransforms[i] = new Matrix3x3(
                                mat.M11, mat.M12, mat.M13,
                                mat.M21, mat.M22, mat.M23,
                                mat.M31, mat.M32, mat.M33
                            ).Inverse();
                        }
                        modelComp.Model.Skeleton.UpdateTransforms(finalTransforms);
                        modelComp.NormalBoneTransforms = normalTransforms;
                    }
                }
            }
        }
    }
}