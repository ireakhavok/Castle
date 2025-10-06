// Folder: SiegeEngine.ContextManagement
// File: IRenderContext.cs
using System;

namespace SiegeEngine.ContextManagement
{
    public unsafe interface IRenderContext
    {
        AbstractRenderEnums Enums { get; }
        int ViewportWidth { get; }
        int ViewportHeight { get; }
        uint GenVertexArray();
        void GenVertexArrays(uint n, out uint arrays);
        uint GenBuffer();
        void GenBuffers(uint n, out uint buffers);
        void BindVertexArray(uint array);
        void BindBuffer(int target, uint buffer);
        void BufferData(int target, uint size, void* data, int usage);
        void BufferSubData(int target, int offset, uint size, void* data);
        void EnableVertexAttribArray(uint index);
        void DisableVertexAttribArray(uint index);
        void VertexAttribPointer(uint index, int size, int type, bool normalized, uint stride, void* pointer);
        void VertexAttribIPointer(uint index, int size, int type, uint stride, void* pointer);
        void DeleteVertexArray(uint array);
        void DeleteBuffer(uint buffer);
        void DeleteBuffers(uint n, uint* buffers);
        void DrawArrays(int mode, int first, uint count);
        void DrawElements(int mode, uint count, int type, void* indices);
        void Clear(int mask);
        void ClearColor(float red, float green, float blue, float alpha);
        void Viewport(int x, int y, uint width, uint height);
        void Enable(int cap);
        void Disable(int cap);
        void BlendFunc(int src, int dst);
        void DepthMask(bool mask);
        void DepthFunc(int func);
        void ColorMask(bool r, bool g, bool b, bool a);
        void ActiveTexture(int unit);
        void BindFramebuffer(int target, uint framebuffer);
        int CheckFramebufferStatus(int target);
        void DrawBuffer(int mode);
        void ReadBuffer(int mode);
        void GenTextures(uint n, out uint textures);
        void BindTexture(int target, uint texture);
        void TexImage2D(int target, int level, int internalformat, uint width, uint height, int border, int format, int type, void* pixels);
        void TexParameter(int target, int pname, int param);
        void TexParameterf(int target, int pname, float param);
        void PixelStore(int pname, int param);
        void DeleteTexture(uint texture);
        void DeleteTextures(uint n, uint* textures);
        uint CreateProgram();
        uint CreateShader(int type);
        void ShaderSource(uint shader, string source);
        void CompileShader(uint shader);
        void GetShader(uint shader, int param, out int value);
        string GetShaderInfoLog(uint shader);
        void AttachShader(uint program, uint shader);
        void DetachShader(uint program, uint shader);
        void LinkProgram(uint program);
        void GetProgram(uint program, int prop, out int value);
        string GetProgramInfoLog(uint program);
        void DeleteShader(uint shader);
        void DeleteProgram(uint program);
        void UseProgram(uint program);
        int GetUniformLocation(uint program, string name);
        void Uniform1(int location, float value);
        void Uniform1(int location, int value);
        void Uniform4(int location, float x, float y, float z, float w);
        void UniformMatrix4(int location, uint count, bool transpose, float* value);
        int GetError();
        bool IsVertexArray(uint array);
        bool IsBuffer(uint buffer);
        bool IsTexture(uint texture);
        void GenerateMipmap(int target);
        bool IsExtensionPresent(string extension);
        void GetFloat(int pname, out float param);
        void Scissor(int x, int y, uint width, uint height);
        void CullFace(int mode);
    }
}