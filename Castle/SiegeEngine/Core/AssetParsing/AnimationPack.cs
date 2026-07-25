// Folder: SiegeEngine.Core.AssetParsing.Model
// File: AnimationPack.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using SiegeEngine.Core.AssetParsing.Model;
namespace SiegeEngine.Core.AssetParsing.Model
{
    public class AnimationPack
    {
        public string Id { get; set; }
        public string Name { get; set; }
        // Runtime-only. Never persist keyframes — reconstruct from SourceFBXPath / Clip paths via ModelManager cache.
        [JsonIgnore]
        public List<Animation> Animations { get; set; } = new List<Animation>();
        public List<AnimationClipEntry> Clips { get; set; } = new List<AnimationClipEntry>();
        public Dictionary<string, int> BoneNameToIndex { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Vector3 DefaultBlendParams { get; set; } = Vector3.Zero;
        public AnimationBlendStack.MovementBlendConfig BlendConfig { get; set; } = new AnimationBlendStack.MovementBlendConfig();
        public string SourceFBXPath { get; set; }
        public string SourceSkeletonPath { get; set; }
        // Material data (including world-aligned TextureSlots) is now part of the pack manifest
        // This allows full save/load of material configuration exactly like animation data
        public Material Material { get; set; }
        public AnimationPack()
        {
        }
        public AnimationPack(string id, string name)
        {
            Id = id;
            Name = name;
        }
        public void AddClip(AnimationClipEntry clip)
        {
            if (clip != null && !string.IsNullOrEmpty(clip.AnimationPath))
            {
                Clips.Add(clip);
            }
        }
        public AnimationBlendStack CreateBlendStack()
        {
            var stack = new AnimationBlendStack();
            foreach (var clip in Clips)
            {
                // Deep copy so every temporary stack owns independent LocalTime and never mutates the pack
                stack.Clips.Add(new AnimationClipEntry
                {
                    AnimationPath = clip.AnimationPath,
                    BlendCoordinate = clip.BlendCoordinate,
                    StartFrame = clip.StartFrame,
                    EndFrame = clip.EndFrame,
                    PlaybackSpeed = clip.PlaybackSpeed,
                    Loop = clip.Loop,
                    LocalTime = 0f
                });
            }
            stack.DefaultBlendParams = DefaultBlendParams;
            stack.BlendConfig = BlendConfig ?? new AnimationBlendStack.MovementBlendConfig();
            return stack;
        }
    }
}