// Folder: SiegeEngine/ContextManagement
// File: AbstractRenderEnums.cs
using System;
namespace SiegeEngine.Core.Rendering.ContextManagement
{
    public abstract class AbstractRenderEnums
    {
        public abstract int ArrayBuffer { get; }
        public abstract int ElementArrayBuffer { get; }
        public abstract int StaticDraw { get; }
        public abstract int DynamicDraw { get; }
        public abstract int Float { get; }
        public abstract int Int { get; }
        public abstract int Points { get; }
        public abstract int Lines { get; }
        public abstract int TriangleFan { get; }
        public abstract int Triangles { get; }
        public abstract int UnsignedInt { get; }
        public abstract int ColorBufferBit { get; }
        public abstract int DepthBufferBit { get; }
        public abstract int Blend { get; }
        public abstract int SrcAlpha { get; }
        public abstract int OneMinusSrcAlpha { get; }
        public abstract int DepthTest { get; }
        public abstract int Texture0 { get; }
        public abstract int InternalRgb { get; }
        public abstract int InternalRgba { get; }
        public abstract int PixelRgb { get; }
        public abstract int PixelRgba { get; }
        public abstract int PixelBgr { get; }
        public abstract int PixelBgra { get; }
        public abstract int UnsignedByte { get; }
        public abstract int Nearest { get; }
        public abstract int Linear { get; }
        public abstract int LinearMipmapLinear { get; }
        public abstract int ClampToEdge { get; }
        public abstract int Repeat { get; }
        public abstract int VertexShader { get; }
        public abstract int FragmentShader { get; }
        public abstract int CompileStatus { get; }
        public abstract int LinkStatus { get; }
        public abstract int UnpackAlignment { get; }
        public abstract int ScissorTest { get; }
        public abstract int TextureMinFilter { get; }
        public abstract int TextureMagFilter { get; }
        public abstract int TextureWrapS { get; }
        public abstract int TextureWrapT { get; }
        public abstract int TextureLodBias { get; }
        public abstract int TextureMaxAnisotropyExt { get; }
        public abstract int MaxTextureMaxAnisotropyExt { get; }
        public abstract int Texture2D { get; }
        public abstract int Framebuffer { get; }
        public abstract int FramebufferComplete { get; }
        public abstract int ColorAttachment0 { get; }
        public abstract int Less { get; }
        public abstract int NoError { get; }
        public abstract int Rgba { get; }
        public abstract int CullFace { get; }
        public abstract int Back { get; }
        public abstract int Clockwise { get; }
        public abstract int CounterClockwise { get; }
        public abstract int LineSmooth { get; }
    }
}