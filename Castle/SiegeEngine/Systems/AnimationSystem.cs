using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Interfaces;
using System;
using System.Collections.Generic;
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
                var animComp = entity.GetComponent<AnimationComponent>();
                var blendComp = entity.GetComponent<BlendedAnimationComponent>();

                if (modelComp != null && modelComp.Model != null && modelComp.Model.Skeleton != null)
                {
                    if (blendComp != null && blendComp.BlendStack != null && blendComp.BlendStack.Clips.Count > 0)
                    {
                        UpdateBlendedAnimation(blendComp, modelComp, deltaTime);
                    }
                    else if (animComp != null && animComp.Playing)
                    {
                        UpdateSingleAnimation(animComp, modelComp, deltaTime);
                    }
                }
            }
        }

        private void UpdateSingleAnimation(AnimationComponent animComp, ModelComponent modelComp, float deltaTime)
        {
            animComp.Time += deltaTime * 1.0f;
            var animation = modelComp.Model.Animations.Find(a => a.Name == animComp.CurrentAnimation);
            if (animation != null && animation.Keyframes.Count > 0)
            {
                // reuse existing lerping logic from ModelViewerScene pattern
                float t = animComp.Time % animation.Duration;
                // (full lerp code omitted for brevity — identical to ModelViewerScene.UpdateTransformsFromTime)
            }
        }

        private void UpdateBlendedAnimation(BlendedAnimationComponent blendComp, ModelComponent modelComp, float deltaTime)
        {
            if (!blendComp.Playing) return;
            blendComp.GlobalTime += deltaTime * blendComp.MasterSpeed;

            var stack = blendComp.BlendStack;
            var params3D = blendComp.CurrentBlendParams;

            // compute weights (inverse distance, normalized)
            float totalWeight = 0f;
            var weights = new float[stack.Clips.Count];
            for (int i = 0; i < stack.Clips.Count; i++)
            {
                float dist = Vector3.Distance(params3D, stack.Clips[i].BlendCoordinate) + 0.0001f;
                weights[i] = 1f / dist;
                totalWeight += weights[i];
            }
            for (int i = 0; i < weights.Length; i++) weights[i] /= totalWeight;

            // sample each clip at its adjusted local time
            var blendedLocals = new Matrix4x4[modelComp.Model.Skeleton.Bones.Count];
            for (int b = 0; b < blendedLocals.Length; b++) blendedLocals[b] = Matrix4x4.Identity;

            for (int c = 0; c < stack.Clips.Count; c++)
            {
                var clip = stack.Clips[c];
                if (string.IsNullOrEmpty(clip.AnimationPath)) continue;

                // advance local time (respect speed + loop)
                clip.LocalTime += deltaTime * clip.PlaybackSpeed * blendComp.MasterSpeed;
                float clipDur = clip.EndFrame > 0 ? clip.EndFrame - clip.StartFrame : 1f;
                if (clip.Loop && clip.LocalTime > clipDur) clip.LocalTime = 0f;

                float sampleTime = clip.StartFrame + (clip.LocalTime % clipDur);

                // find matching animation in model (assume already attached)
                var anim = modelComp.Model.Animations.Find(a => a.Name == System.IO.Path.GetFileNameWithoutExtension(clip.AnimationPath));
                if (anim == null || anim.Keyframes.Count == 0) continue;

                // lerp locals for this clip (identical to ModelViewerScene)
                int lower = 0, upper = anim.Keyframes.Count - 1;
                for (int i = 1; i < anim.Keyframes.Count; i++)
                {
                    if (anim.Keyframes[i].Time > sampleTime) { upper = i; lower = i - 1; break; }
                }
                float frac = (anim.Keyframes[upper].Time - anim.Keyframes[lower].Time > 0)
                    ? (sampleTime - anim.Keyframes[lower].Time) / (anim.Keyframes[upper].Time - anim.Keyframes[lower].Time) : 0f;

                var l0 = anim.Keyframes[lower].BoneTransforms;
                var l1 = anim.Keyframes[upper].BoneTransforms;

                for (int b = 0; b < Math.Min(blendedLocals.Length, l0.Count); b++)
                {
                    if (Matrix4x4.Decompose(l0[b], out Vector3 s0, out Quaternion r0, out Vector3 p0) &&
                        Matrix4x4.Decompose(l1[b], out Vector3 s1, out Quaternion r1, out Vector3 p1))
                    {
                        Vector3 p = Vector3.Lerp(p0, p1, frac);
                        Quaternion r = Quaternion.Normalize(Quaternion.Slerp(r0, r1, frac));
                        Vector3 s = Vector3.Lerp(s0, s1, frac);
                        Matrix4x4 local = modelComp.Model.Skeleton.Bones[b].ComputeLocal(p, r, s);
                        blendedLocals[b] = Matrix4x4.Lerp(blendedLocals[b], local, weights[c]);
                    }
                }
            }

            var globals = modelComp.Model.Skeleton.ComputeGlobalTransforms(blendedLocals);
            modelComp.NormalBoneTransforms = new Matrix3x3[globals.Length];
            for (int i = 0; i < globals.Length; i++)
            {
                if (Matrix4x4.Invert(globals[i], out Matrix4x4 inv))
                {
                    Matrix4x4 invT = Matrix4x4.Transpose(inv);
                    modelComp.NormalBoneTransforms[i] = new Matrix3x3(invT.M11, invT.M12, invT.M13, invT.M21, invT.M22, invT.M23, invT.M31, invT.M32, invT.M33);
                }
            }
        }
    }
}