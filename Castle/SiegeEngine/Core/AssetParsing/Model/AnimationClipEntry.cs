using System;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class AnimationClipEntry
    {
        public string AnimationPath { get; set; }
        public Vector3 BlendCoordinate { get; set; }
        public float StartFrame { get; set; } = 0f;
        public float EndFrame { get; set; } = -1f; // -1 = full duration
        public float PlaybackSpeed { get; set; } = 1.0f;
        public bool Loop { get; set; } = true;
        public float LocalTime { get; set; } = 0f;
    }
}