using Silk.NET.OpenGL;
using System;

namespace SiegeEngine.Interfaces
{
    public unsafe interface IRenderContext
    {
        uint GenVertexArray();
        void GenVertexArrays(uint n, out uint arrays);
        uint GenBuffer();
        void GenBuffers(uint n, out uint buffers);
        void BindVertexArray(uint array);
        void BindBuffer(BufferTargetARB target, uint buffer);
        void BufferData(BufferTargetARB target, uint size, void* data, BufferUsageARB usage);
        void BufferSubData(BufferTargetARB target, int offset, uint size, void* data);
        void EnableVertexAttribArray(uint index);
        void DisableVertexAttribArray(uint index);
        void VertexAttribPointer(uint index, int size, VertexAttribPointerType type, bool normalized, uint stride, void* pointer);
        void DeleteVertexArray(uint array);
        void DeleteBuffer(uint buffer);
        void DeleteBuffers(uint n, uint* buffers);
        void DrawArrays(PrimitiveType mode, int first, uint count);
        void DrawElements(PrimitiveType mode, uint count, DrawElementsType type, void* indices);
        void Clear(ClearBufferMask mask);
        void ClearColor(float red, float green, float blue, float alpha);
        void Viewport(int x, int y, uint width, uint height);
        void Enable(EnableCap cap);
        void Disable(EnableCap cap);
        void BlendFunc(BlendingFactor src, BlendingFactor dst);
        void DepthMask(bool mask);
        void DepthFunc(DepthFunction func);
        void ColorMask(bool r, bool g, bool b, bool a);
        void ActiveTexture(TextureUnit unit);
        void BindFramebuffer(FramebufferTarget target, uint framebuffer);
        GLEnum CheckFramebufferStatus(FramebufferTarget target);
        void DrawBuffer(DrawBufferMode mode);
        void ReadBuffer(ReadBufferMode mode);
        void GenTextures(uint n, out uint textures);
        void BindTexture(TextureTarget target, uint texture);
        void TexImage2D(TextureTarget target, int level, InternalFormat internalformat, uint width, uint height, int border, GLEnum format, GLEnum type, void* pixels);
        void TexParameter(TextureTarget target, TextureParameterName pname, int param);
        void TexParameterf(TextureTarget target, TextureParameterName pname, float param);
        void PixelStore(PixelStoreParameter pname, int param);
        void DeleteTexture(uint texture);
        void DeleteTextures(uint n, uint* textures);
        uint CreateProgram();
        uint CreateShader(ShaderType type);
        void ShaderSource(uint shader, string source);
        void CompileShader(uint shader);
        void GetShader(uint shader, ShaderParameterName param, out int value);
        string GetShaderInfoLog(uint shader);
        void AttachShader(uint program, uint shader);
        void DetachShader(uint program, uint shader);
        void LinkProgram(uint program);
        void GetProgram(uint program, ProgramPropertyARB prop, out int value);
        string GetProgramInfoLog(uint program);
        void DeleteShader(uint shader);
        void DeleteProgram(uint program);
        void UseProgram(uint program);
        int GetUniformLocation(uint program, string name);
        void Uniform1(int location, float value);
        void Uniform1(int location, int value);
        void Uniform4(int location, float x, float y, float z, float w);
        void UniformMatrix4(int location, uint count, bool transpose, float* value);
        GLEnum GetError();
        bool IsVertexArray(uint array);
        bool IsBuffer(uint buffer);
        bool IsTexture(uint texture);
        void GenerateMipmap(TextureTarget target);
        bool IsExtensionPresent(string extension);
        void GetFloat(GetPName pname, out float param);
    }
}