// Folder: SiegeEngine/ContextManagement
// File: OpenGLEnums.cs
using Silk.NET.OpenGL;
namespace SiegeEngine.Core.Rendering.ContextManagement
{
    public class OpenGLEnums : AbstractRenderEnums
    {
        public override int ArrayBuffer => (int)GLEnum.ArrayBuffer;
        public override int ElementArrayBuffer => (int)GLEnum.ElementArrayBuffer;
        public override int StaticDraw => (int)GLEnum.StaticDraw;
        public override int DynamicDraw => (int)GLEnum.DynamicDraw;
        public override int Float => (int)GLEnum.Float;
        public override int Int => (int)GLEnum.Int;
        public override int Points => (int)GLEnum.Points;
        public override int Lines => (int)GLEnum.Lines;
        public override int TriangleFan => (int)GLEnum.TriangleFan;
        public override int Triangles => (int)GLEnum.Triangles;
        public override int UnsignedInt => (int)GLEnum.UnsignedInt;
        public override int ColorBufferBit => (int)GLEnum.ColorBufferBit;
        public override int DepthBufferBit => (int)GLEnum.DepthBufferBit;
        public override int Blend => (int)GLEnum.Blend;
        public override int SrcAlpha => (int)GLEnum.SrcAlpha;
        public override int OneMinusSrcAlpha => (int)GLEnum.OneMinusSrcAlpha;
        public override int DepthTest => (int)GLEnum.DepthTest;
        public override int Texture0 => (int)GLEnum.Texture0;
        public override int InternalRgb => (int)GLEnum.Rgb;
        public override int InternalRgba => (int)GLEnum.Rgba;
        public override int PixelRgb => (int)GLEnum.Rgb;
        public override int PixelRgba => (int)GLEnum.Rgba;
        public override int PixelBgr => (int)GLEnum.Bgr;
        public override int PixelBgra => (int)GLEnum.Bgra;
        public override int UnsignedByte => (int)GLEnum.UnsignedByte;
        public override int Nearest => (int)GLEnum.Nearest;
        public override int Linear => (int)GLEnum.Linear;
        public override int LinearMipmapLinear => (int)GLEnum.LinearMipmapLinear;
        public override int ClampToEdge => (int)GLEnum.ClampToEdge;
        public override int Repeat => (int)GLEnum.Repeat;
        public override int VertexShader => (int)GLEnum.VertexShader;
        public override int FragmentShader => (int)GLEnum.FragmentShader;
        public override int CompileStatus => (int)GLEnum.CompileStatus;
        public override int LinkStatus => (int)GLEnum.LinkStatus;
        public override int UnpackAlignment => (int)GLEnum.UnpackAlignment;
        public override int ScissorTest => (int)GLEnum.ScissorTest;
        public override int TextureMinFilter => (int)GLEnum.TextureMinFilter;
        public override int TextureMagFilter => (int)GLEnum.TextureMagFilter;
        public override int TextureWrapS => (int)GLEnum.TextureWrapS;
        public override int TextureWrapT => (int)GLEnum.TextureWrapT;
        public override int TextureLodBias => (int)GLEnum.TextureLodBias;
        public override int TextureMaxAnisotropyExt => (int)GLEnum.TextureMaxAnisotropy;
        public override int MaxTextureMaxAnisotropyExt => (int)GLEnum.MaxTextureMaxAnisotropy;
        public override int Texture2D => (int)GLEnum.Texture2D;
        public override int Framebuffer => (int)GLEnum.Framebuffer;
        public override int FramebufferComplete => (int)GLEnum.FramebufferComplete;
        public override int ColorAttachment0 => (int)GLEnum.ColorAttachment0;
        public override int Less => (int)GLEnum.Less;
        public override int NoError => (int)GLEnum.NoError;
        public override int Rgba => (int)GLEnum.Rgba;
        public override int CullFace => (int)GLEnum.CullFace;
        public override int Back => (int)GLEnum.Back;
        public override int CounterClockwise => (int)GLEnum.Ccw;
        public override int Clockwise => (int)GLEnum.CW;
        public override int LineSmooth => (int)GLEnum.LineSmooth;
        public override int TextureCubeMap => (int)GLEnum.TextureCubeMap;
        public override int TextureCubeMapPositiveX => (int)GLEnum.TextureCubeMapPositiveX;
        public override int TextureCubeMapNegativeX => (int)GLEnum.TextureCubeMapNegativeX;
        public override int TextureCubeMapPositiveY => (int)GLEnum.TextureCubeMapPositiveY;
        public override int TextureCubeMapNegativeY => (int)GLEnum.TextureCubeMapNegativeY;
        public override int TextureCubeMapPositiveZ => (int)GLEnum.TextureCubeMapPositiveZ;
        public override int TextureCubeMapNegativeZ => (int)GLEnum.TextureCubeMapNegativeZ;
        public override int TextureWrapR => (int)GLEnum.TextureWrapR;
    }
}