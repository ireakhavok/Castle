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
            Clips.Add(new AnimationClipEntry
            {
                AnimationPath = path,
                BlendCoordinate = finalCoord,
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