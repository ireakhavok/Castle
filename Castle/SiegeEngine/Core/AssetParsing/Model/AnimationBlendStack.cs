using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
namespace SiegeEngine.Core.AssetParsing.Model
{
    public class AnimationBlendStack
    {
        public string Name { get; set; } = "New Blend Stack";
        public List<AnimationClipEntry> Clips { get; set; } = new List<AnimationClipEntry>();
        public Vector3 DefaultBlendParams { get; set; } = Vector3.Zero;
        public string SharedSkeletonPath { get; set; }
        public string SharedMeshPath { get; set; }
        [JsonIgnore]
        public FBXModel CachedModel { get; set; }
        public MovementBlendConfig BlendConfig { get; set; } = new MovementBlendConfig();
        public bool SnapEnabled { get; set; } = true;
        public class MovementBlendConfig
        {
            public float XMin { get; set; } = -1f;
            public float XMax { get; set; } = 1f;
            public float YMin { get; set; } = -1f;
            public float YMax { get; set; } = 1f;
            public float ZMin { get; set; } = -2f;
            public float ZMax { get; set; } = 2f;
            public float DeadZone { get; set; } = 0.1f;
            public float SnapStep { get; set; } = 0.25f;
        }
        public Vector3 MapPlayerInputToBlendCoord(Vector2 moveInput, float vertical = 0f)
        {
            if (moveInput.Length() < BlendConfig.DeadZone) moveInput = Vector2.Zero;
            float x = Math.Clamp(moveInput.X, -1f, 1f) * (moveInput.X >= 0 ? BlendConfig.XMax : -BlendConfig.XMin);
            float y = Math.Clamp(moveInput.Y, -1f, 1f) * (moveInput.Y >= 0 ? BlendConfig.YMax : -BlendConfig.YMin);
            float z = Math.Clamp(vertical, BlendConfig.ZMin, BlendConfig.ZMax);
            return new Vector3(x, y, z);
        }
        public Vector3 SnapCoordinate(Vector3 coord)
        {
            if (!SnapEnabled || BlendConfig.SnapStep <= 0f) return coord;
            float step = BlendConfig.SnapStep;
            return new Vector3(
                (float)Math.Round(coord.X / step) * step,
                (float)Math.Round(coord.Y / step) * step,
                (float)Math.Round(coord.Z / step) * step);
        }
        public void AddClip(string path, Vector3 coord, float start = 0f, float end = -1f, float speed = 1f, bool loop = true)
        {
            Vector3 finalCoord = SnapEnabled ? SnapCoordinate(coord) : coord;
            Clips.Add(new AnimationClipEntry { AnimationPath = path, BlendCoordinate = finalCoord, StartFrame = start, EndFrame = end, PlaybackSpeed = speed, Loop = loop });
        }
        public void RemoveClip(int index)
        {
            if (index >= 0 && index < Clips.Count) Clips.RemoveAt(index);
        }
        public AnimationClipEntry GetClipAt(Vector3 params3D)
        {
            if (Clips.Count == 0) return null;
            AnimationClipEntry best = Clips[0];
            float bestDist = float.MaxValue;
            foreach (var clip in Clips)
            {
                float dist = Vector3.Distance(params3D, clip.BlendCoordinate);
                if (dist < bestDist) { bestDist = dist; best = clip; }
            }
            return best;
        }
        public Matrix4x4[] ComputeBlendedLocals(Vector3 params3D, float deltaTime, bool isPlaying, FBXModel model)
        {
            if (Clips.Count == 0 || model == null || model.Skeleton == null) return null;
            if (Clips.Count == 1)
            {
                // single-clip path left to caller (UpdateTransformsFromTime style)
                return null;
            }
            float totalWeight = 0f;
            var weights = new float[Clips.Count];
            for (int i = 0; i < Clips.Count; i++)
            {
                float dist = Vector3.Distance(params3D, Clips[i].BlendCoordinate);
                float vecFactor = 1f;
                if (Clips[i].BlendCoordinate.LengthSquared() < 0.1f) // idle clip
                    vecFactor = Math.Clamp(1f - (params3D.Length() * 0.6f), 0.15f, 1f);
                weights[i] = vecFactor / (dist * dist + 0.0001f);
                totalWeight += weights[i];
            }
            for (int i = 0; i < weights.Length; i++) weights[i] /= totalWeight;
            var finalPos = new Vector3[model.Skeleton.Bones.Count];
            var finalRot = new Quaternion[model.Skeleton.Bones.Count];
            var finalScale = new Vector3[model.Skeleton.Bones.Count];
            for (int b = 0; b < model.Skeleton.Bones.Count; b++)
            {
                finalPos[b] = Vector3.Zero;
                finalRot[b] = new Quaternion(0, 0, 0, 0);
                finalScale[b] = Vector3.Zero;
            }
            bool firstClip = true;
            for (int c = 0; c < Clips.Count; c++)
            {
                var clip = Clips[c];
                if (string.IsNullOrEmpty(clip.AnimationPath)) continue;
                if (isPlaying) clip.LocalTime += deltaTime * clip.PlaybackSpeed;
                var anim = model.Animations.Distinct().ElementAtOrDefault(c) ?? model.Animations.LastOrDefault();
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
                for (int b = 0; b < Math.Min(model.Skeleton.Bones.Count, l0.Count); b++)
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
            var blendedLocals = new Matrix4x4[model.Skeleton.Bones.Count];
            for (int b = 0; b < model.Skeleton.Bones.Count; b++)
            {
                Quaternion blendedR = Quaternion.Normalize(finalRot[b]);
                blendedLocals[b] = model.Skeleton.Bones[b].ComputeLocal(finalPos[b], blendedR, finalScale[b]);
            }
            return blendedLocals;
        }
    }
}