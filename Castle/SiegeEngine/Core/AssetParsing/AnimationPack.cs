using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.AssetParsing.Model;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class AnimationPack
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<Animation> Animations { get; set; } = new List<Animation>();
        public List<AnimationClipEntry> Clips { get; set; } = new List<AnimationClipEntry>();
        public Dictionary<string, int> BoneNameToIndex { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Vector3 DefaultBlendParams { get; set; } = Vector3.Zero;

        public AnimationPack() { }

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
                stack.Clips.Add(clip);
            }
            return stack;
        }
    }
}