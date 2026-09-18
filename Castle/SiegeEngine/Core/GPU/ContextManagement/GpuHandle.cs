// Folder: SiegeEngine/Core/GPU/ContextManagement
// File: GpuHandle.cs
namespace SiegeEngine.Core.GPU.ContextManagement
{
    public enum GpuResourceKind : byte
    {
        None = 0,
        Pipeline = 1,
        Buffer = 2,
        Texture = 3
    }

    public readonly struct GpuHandle
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
    }
}
