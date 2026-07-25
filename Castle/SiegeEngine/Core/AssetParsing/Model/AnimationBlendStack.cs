// Folder: SiegeEngine.Core.AssetParsing.Model
// File: AnimationBlendStack.cs
using System;
using System.Collections.Generic;
using System.IO;
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
            // Pack + editor grid convention: top-of-grid = -1 = forward. Positive player forward (W) must therefore produce negative blend Y.
            float y = Math.Clamp(moveInput.Y, -1f, 1f) * (moveInput.Y >= 0 ? -BlendConfig.YMax : BlendConfig.YMin);
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
            if (model == null || model.Skeleton == null) return null;
            int boneCount = model.Skeleton.Bones.Count;
            if (Clips.Count == 0)
            {
                var rest = new Matrix4x4[boneCount];
                for (int b = 0; b < boneCount; b++) rest[b] = model.Skeleton.Bones[b].LocalRest;
                return rest;
            }
            if (Clips.Count == 1)
            {
                var clip = Clips[0];
                if (string.IsNullOrEmpty(clip.AnimationPath))
                {
                    var rest = new Matrix4x4[boneCount];
                    for (int b = 0; b < boneCount; b++) rest[b] = model.Skeleton.Bones[b].LocalRest;
                    return rest;
                }
                if (isPlaying) clip.LocalTime += deltaTime * clip.PlaybackSpeed;
                string desiredName = Path.GetFileNameWithoutExtension(clip.AnimationPath).ToLowerInvariant();
                var anim = model.Animations.FirstOrDefault(a => string.Equals(a.Name, desiredName, StringComparison.OrdinalIgnoreCase))
                           ?? model.Animations.LastOrDefault();
                if (anim == null || anim.Keyframes.Count == 0)
                {
                    var rest = new Matrix4x4[boneCount];
                    for (int b = 0; b < boneCount; b++) rest[b] = model.Skeleton.Bones[b].LocalRest;
                    return rest;
                }
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
                var lerpedLocals = new Matrix4x4[boneCount];
                for (int b = 0; b < Math.Min(boneCount, l0.Count); b++)
                {
                    if (Matrix4x4.Decompose(l0[b], out Vector3 s0, out Quaternion r0, out Vector3 p0) &&
                        Matrix4x4.Decompose(l1[b], out Vector3 s1, out Quaternion r1, out Vector3 p1))
                    {
                        Vector3 p = Vector3.Lerp(p0, p1, frac);
                        Quaternion r = Quaternion.Normalize(Quaternion.Slerp(r0, r1, frac));
                        Vector3 s = Vector3.Lerp(s0, s1, frac);
                        lerpedLocals[b] = model.Skeleton.Bones[b].ComputeLocal(p, r, s);
                    }
                    else
                    {
                        lerpedLocals[b] = l0[b];
                    }
                }
                for (int b = Math.Min(boneCount, l0.Count); b < boneCount; b++)
                    lerpedLocals[b] = model.Skeleton.Bones[b].LocalRest;
                return lerpedLocals;
            }
            // Multi-clip inverse-distance weighting
            float totalWeight = 0f;
            var weights = new float[Clips.Count];
            for (int i = 0; i < Clips.Count; i++)
            {
                float dist = Vector3.Distance(params3D, Clips[i].BlendCoordinate);
                float vecFactor = 1f;
                if (Clips[i].BlendCoordinate.LengthSquared() < 0.1f)
                    vecFactor = Math.Clamp(1f - (params3D.Length() * 0.6f), 0.15f, 1f);
                weights[i] = vecFactor / (dist * dist + 0.0001f);
                totalWeight += weights[i];
            }
            if (totalWeight <= 0f)
            {
                var rest = new Matrix4x4[boneCount];
                for (int b = 0; b < boneCount; b++) rest[b] = model.Skeleton.Bones[b].LocalRest;
                return rest;
            }
            for (int i = 0; i < weights.Length; i++) weights[i] /= totalWeight;

            // Sample every clip once (advance LocalTime, resolve anim, produce per-bone p/r/s)
            var samplePos = new Vector3[Clips.Count][];
            var sampleRot = new Quaternion[Clips.Count][];
            var sampleScale = new Vector3[Clips.Count][];
            var sampleValid = new bool[Clips.Count];
            for (int c = 0; c < Clips.Count; c++)
            {
                samplePos[c] = new Vector3[boneCount];
                sampleRot[c] = new Quaternion[boneCount];
                sampleScale[c] = new Vector3[boneCount];
                sampleValid[c] = false;
                var clip = Clips[c];
                if (string.IsNullOrEmpty(clip.AnimationPath)) continue;
                if (isPlaying) clip.LocalTime += deltaTime * clip.PlaybackSpeed;
                string desiredName = Path.GetFileNameWithoutExtension(clip.AnimationPath).ToLowerInvariant();
                var anim = model.Animations.FirstOrDefault(a => string.Equals(a.Name, desiredName, StringComparison.OrdinalIgnoreCase));
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
                int maxB = Math.Min(boneCount, l0.Count);
                for (int b = 0; b < maxB; b++)
                {
                    if (Matrix4x4.Decompose(l0[b], out Vector3 s0, out Quaternion r0, out Vector3 p0) &&
                        Matrix4x4.Decompose(l1[b], out Vector3 s1, out Quaternion r1, out Vector3 p1))
                    {
                        samplePos[c][b] = Vector3.Lerp(p0, p1, frac);
                        sampleRot[c][b] = Quaternion.Normalize(Quaternion.Slerp(r0, r1, frac));
                        sampleScale[c][b] = Vector3.Lerp(s0, s1, frac);
                    }
                    else
                    {
                        samplePos[c][b] = Vector3.Zero;
                        sampleRot[c][b] = Quaternion.Identity;
                        sampleScale[c][b] = Vector3.One;
                    }
                }
                for (int b = maxB; b < boneCount; b++)
                {
                    samplePos[c][b] = Vector3.Zero;
                    sampleRot[c][b] = Quaternion.Identity;
                    sampleScale[c][b] = Vector3.One;
                }
                sampleValid[c] = true;
            }

            // Highest-weight valid clip becomes the rotation base for successive weighted Slerp
            int baseClip = -1;
            float maxW = -1f;
            for (int c = 0; c < Clips.Count; c++)
            {
                if (sampleValid[c] && weights[c] > maxW)
                {
                    maxW = weights[c];
                    baseClip = c;
                }
            }
            if (baseClip < 0)
            {
                var rest = new Matrix4x4[boneCount];
                for (int b = 0; b < boneCount; b++) rest[b] = model.Skeleton.Bones[b].LocalRest;
                return rest;
            }

            var finalPos = new Vector3[boneCount];
            var finalRot = new Quaternion[boneCount];
            var finalScale = new Vector3[boneCount];
            for (int b = 0; b < boneCount; b++)
            {
                finalPos[b] = Vector3.Zero;
                finalScale[b] = Vector3.Zero;
                // Start rotation from the highest-weight clip for this bone
                finalRot[b] = sampleRot[baseClip][b];
                float rotWeightSum = weights[baseClip];
                for (int c = 0; c < Clips.Count; c++)
                {
                    if (!sampleValid[c]) continue;
                    float w = weights[c];
                    finalPos[b] += samplePos[c][b] * w;
                    finalScale[b] += sampleScale[c][b] * w;
                    if (c == baseClip) continue;
                    Quaternion r = sampleRot[c][b];
                    if (Quaternion.Dot(finalRot[b], r) < 0f) r = Quaternion.Negate(r);
                    float t = w / (rotWeightSum + w);
                    finalRot[b] = Quaternion.Slerp(finalRot[b], r, t);
                    rotWeightSum += w;
                }
                finalRot[b] = Quaternion.Normalize(finalRot[b]);
            }

            var blendedLocals = new Matrix4x4[boneCount];
            for (int b = 0; b < boneCount; b++)
            {
                blendedLocals[b] = model.Skeleton.Bones[b].ComputeLocal(finalPos[b], finalRot[b], finalScale[b]);
            }
            return blendedLocals;
        }
    }
}