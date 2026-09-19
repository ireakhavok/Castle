// Folder: SiegeEngine/Core/GPU/ContextManagement
// File: GpuHandle.cs
using System;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    public enum GpuResourceKind : byte
    {
        None = 0,
        Pipeline = 1,
        Buffer = 2,
        Texture = 3,
        RenderTarget = 4
    }

    public enum MapAccess : byte
    {
        Read = 1,
        Write = 2,
        ReadWrite = 3
    }

    public readonly struct GpuHandle : IEquatable<GpuHandle>
    {
        public readonly uint Id;
        public readonly uint Generation;
        public readonly GpuResourceKind Kind;

        public GpuHandle(uint id, uint generation, GpuResourceKind kind)
        {
            Id = id;
            Generation = generation;
            Kind = kind;
        }

        public bool IsValid => Kind != GpuResourceKind.None && Id != 0;

        public static GpuHandle Invalid => default;

        public bool Equals(GpuHandle other)
        {
            return Id == other.Id && Generation == other.Generation && Kind == other.Kind;
        }

        public override bool Equals(object obj)
        {
            return obj is GpuHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Id;
                hash = (hash * 397) ^ (int)Generation;
                hash = (hash * 397) ^ (byte)Kind;
                return hash;
            }
        }

        public static bool operator ==(GpuHandle left, GpuHandle right) => left.Equals(right);
        public static bool operator !=(GpuHandle left, GpuHandle right) => !left.Equals(right);
    }
}
