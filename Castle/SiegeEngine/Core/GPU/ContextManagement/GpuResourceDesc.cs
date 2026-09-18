// Folder: SiegeEngine/Core/GPU/ContextManagement
// File: GpuResourceDesc.cs
namespace SiegeEngine.Core.GPU.ContextManagement
{
    public struct BufferDesc
    {
        public int Target;
        public int Usage;
        public int ByteSize;
    }

    public struct TextureDesc
    {
        public int Target;
        public int InternalFormat;
        public int Width;
        public int Height;
    }
}
