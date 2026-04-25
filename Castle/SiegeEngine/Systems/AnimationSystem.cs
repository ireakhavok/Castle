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
                float t = animComp.Time % animation.Duration;
            }
        }

        private void UpdateBlendedAnimation(BlendedAnimationComponent blendComp, ModelComponent modelComp, float deltaTime)
        {
            if (!blendComp.Playing) return;
            blendComp.GlobalTime += deltaTime * blendComp.MasterSpeed;
            var stack = blendComp.BlendStack;
            var params3D = blendComp.CurrentBlendParams;
            float totalWeight = 0f;
            var weights = new float[stack.Clips.Count];
            for (int i = 0; i < stack.Clips.Count; i++)
            {
                float dist = Vector3.Distance(params3D, stack.Clips[i].BlendCoordinate) + 0.0001f;
                weights[i] = 1f / dist;
                totalWeight += weights[i];
            }
            for (int i = 0; i < weights.Length; i++) weights[i] /= totalWeight;

            var finalPos = new Vector3[modelComp.Model.Skeleton.Bones.Count];
            var finalRot = new Quaternion[modelComp.Model.Skeleton.Bones.Count];
            var finalScale = new Vector3[modelComp.Model.Skeleton.Bones.Count];
            for (int b = 0; b < modelComp.Model.Skeleton.Bones.Count; b++)
            {
                finalPos[b] = Vector3.Zero;
                finalRot[b] = new Quaternion(0, 0, 0, 0);
                finalScale[b] = Vector3.Zero;
            }

            bool firstClip = true;
            for (int c = 0; c < stack.Clips.Count; c++)
            {
                var clip = stack.Clips[c];
                if (string.IsNullOrEmpty(clip.AnimationPath)) continue;
                clip.LocalTime += deltaTime * clip.PlaybackSpeed * blendComp.MasterSpeed;
                var anim = (c < modelComp.Model.Animations.Count) ? modelComp.Model.Animations[c] : modelComp.Model.Animations.LastOrDefault();
                if (anim == null || anim.Keyframes.Count == 0) continue;
                float animDuration = anim.Duration > 0 ? anim.Duration : (anim.Keyframes.Count > 0 ? anim.Keyframes.Last().Time : 1f);
                float clipDur = clip.EndFrame > 0 ? clip.EndFrame - clip.StartFrame : animDuration;
                if (clip.Loop && clip.LocalTime > clipDur) clip.LocalTime = 0f;
                float sampleTime = clip.StartFrame + (clip.LocalTime % clipDur);
                float normalizedT = (sampleTime - clip.StartFrame) / Math.Max(animDuration, 0.001f);
                normalizedT = Math.Clamp(normalizedT, 0f, 1f);
                float lookupTime = clip.StartFrame + normalizedT * animDuration;
                int lower = 0, upper = anim.Keyframes.Count - 1;
                for (int i = 1; i < anim.Keyframes.Count; i++)
                {
                    if (anim.Keyframes[i].Time > lookupTime) { upper = i; lower = i - 1; break; }
                }
                float t0 = anim.Keyframes[lower].Time;
                float t1 = anim.Keyframes[upper].Time;
                float frac = (t1 - t0 > 0) ? (lookupTime - t0) / (t1 - t0) : 0f;
                var l0 = anim.Keyframes[lower].BoneTransforms;
                var l1 = anim.Keyframes[upper].BoneTransforms;
                for (int b = 0; b < Math.Min(modelComp.Model.Skeleton.Bones.Count, l0.Count); b++)
                {
                    if (Matrix4x4.Decompose(l0[b], out Vector3 s0, out Quaternion r0, out Vector3 p0) &&
                        Matrix4x4.Decompose(l1[b], out Vector3 s1, out Quaternion r1, out Vector3 p1))
                    {
                        Vector3 p = Vector3.Lerp(p0, p1, frac);
                        Quaternion r = Quaternion.Normalize(Quaternion.Slerp(r0, r1, frac));
                        Vector3 s = Vector3.Lerp(s0, s1, frac);

                        if (firstClip)
                        {
                            finalRot[b] = r;
                            firstClip = false;
                        }
                        else
                        {
                            if (Quaternion.Dot(finalRot[b], r) < 0f)
                                r = Quaternion.Negate(r);
                        }

                        finalPos[b] += p * weights[c];
                        finalRot[b].X += r.X * weights[c];
                        finalRot[b].Y += r.Y * weights[c];
                        finalRot[b].Z += r.Z * weights[c];
                        finalRot[b].W += r.W * weights[c];
                        finalScale[b] += s * weights[c];
                    }
                }
            }

            var blendedLocals = new Matrix4x4[modelComp.Model.Skeleton.Bones.Count];
            for (int b = 0; b < modelComp.Model.Skeleton.Bones.Count; b++)
            {
                Quaternion blendedR = Quaternion.Normalize(finalRot[b]);
                blendedLocals[b] = modelComp.Model.Skeleton.Bones[b].ComputeLocal(finalPos[b], blendedR, finalScale[b]);
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