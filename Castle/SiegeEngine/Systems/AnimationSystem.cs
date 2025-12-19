using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Interfaces;
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
                        modelComp.Model.Skeleton.UpdateTransforms(finalTransforms);
                    }
                }
            }
        }
    }
}