using System;
using System.Collections.Generic;
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

        public void AddClip(string path, Vector3 coord, float start = 0f, float end = -1f, float speed = 1f, bool loop = true)
        {
            Clips.Add(new AnimationClipEntry
            {
                AnimationPath = path,
                BlendCoordinate = coord,
                StartFrame = start,
                EndFrame = end,
                PlaybackSpeed = speed,
                Loop = loop
            });
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
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = clip;
                }
            }
            return best;
        }
    }
}