// Folder: SiegeEngine/Core/Definitions
// File: SoundSource.cs
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    public class SoundSource
    {
        public int EntityId { get; set; } = -1; // Default to -1 for non-entity sounds
        public Vector3 Position { get; set; }
        public string Type { get; set; }
        public bool IsSensitive { get; set; }
        public string AudioClip { get; set; }
        public ulong SteamId { get; set; } = 0;
        public bool Loop { get; set; } = false;
        public float Volume { get; set; } = 1f;
    }
}