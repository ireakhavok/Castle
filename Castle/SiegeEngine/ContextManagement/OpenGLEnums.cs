using Silk.NET.OpenGL;

namespace SiegeEngine.ContextManagement
{
    public class OpenGLEnums : AbstractRenderEnums
    {
        public override int ArrayBuffer => (int)BufferTargetARB.ArrayBuffer;
        public override int ElementArrayBuffer => (int)BufferTargetARB.ElementArrayBuffer;
        public override int StaticDraw => (int)BufferUsageARB.StaticDraw;
        public override int DynamicDraw => (int)BufferUsageARB.DynamicDraw;
        public override int Float => (int)VertexAttribPointerType.Float;
        public override int Points => (int)PrimitiveType.Points;
        public override int Lines => (int)PrimitiveType.Lines;
        public override int TriangleFan => (int)PrimitiveType.TriangleFan;
        public override int Triangles => (int)PrimitiveType.Triangles;
        public override int UnsignedInt => (int)DrawElementsType.UnsignedInt;
        public override int ColorBufferBit => (int)ClearBufferMask.ColorBufferBit;
        public override int DepthBufferBit => (int)ClearBufferMask.DepthBufferBit;
        public override int Blend => (int)EnableCap.Blend;
        public override int SrcAlpha => (int)BlendingFactor.SrcAlpha;
        public override int OneMinusSrcAlpha => (int)BlendingFactor.OneMinusSrcAlpha;
        public override int DepthTest => (int)EnableCap.DepthTest;
        public override int Texture0 => (int)TextureUnit.Texture0;
        public override int InternalRgb => (int)InternalFormat.Rgb;
        public override int InternalRgba => (int)InternalFormat.Rgba;
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
        public override int VertexShader => (int)ShaderType.VertexShader;
        public override int FragmentShader => (int)ShaderType.FragmentShader;
        public override int CompileStatus => (int)ShaderParameterName.CompileStatus;
        public override int LinkStatus => (int)ProgramPropertyARB.LinkStatus;
        public override int UnpackAlignment => (int)PixelStoreParameter.UnpackAlignment;
        public override int ScissorTest => (int)EnableCap.ScissorTest;
        public override int TextureMinFilter => (int)TextureParameterName.TextureMinFilter;
        public override int TextureMagFilter => (int)TextureParameterName.TextureMagFilter;
        public override int TextureWrapS => (int)TextureParameterName.TextureWrapS;
        public override int TextureWrapT => (int)TextureParameterName.TextureWrapT;
        public override int TextureLodBias => (int)TextureParameterName.TextureLodBias;
        public override int TextureMaxAnisotropyExt => 0x84FE;
        public override int MaxTextureMaxAnisotropyExt => 0x84FF;
        public override int Texture2D => (int)TextureTarget.Texture2D;
        public override int Framebuffer => (int)FramebufferTarget.Framebuffer;
        public override int FramebufferComplete => (int)GLEnum.FramebufferComplete;
        public override int ColorAttachment0 => (int)DrawBufferMode.ColorAttachment0;
        public override int Less => (int)DepthFunction.Less;
        public override int NoError => (int)GLEnum.NoError;
    }
}