using Silk.NET.OpenGL;
using Silk.NET.GLFW;
using System;

namespace SiegeEngine.ContextManagement
{
    public unsafe class OpenGLRenderContext : IRenderContext
    {
        private readonly GL _gl;
        private readonly Glfw _glfw;

        public OpenGLRenderContext(Glfw glfw, GL gl)
        {
            _glfw = glfw ?? throw new ArgumentNullException(nameof(glfw));
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            if (_glfw.GetCurrentContext() == null)
                throw new InvalidOperationException("No current OpenGL context");
        }

        public uint GenVertexArray()
        {
            _gl.GenVertexArrays(1, out uint array);
            return array;
        }

        public void GenVertexArrays(uint n, out uint arrays) => _gl.GenVertexArrays(n, out arrays);

        public uint GenBuffer()
        {
            _gl.GenBuffers(1, out uint buffer);
            return buffer;
        }

        public void GenBuffers(uint n, out uint buffers) => _gl.GenBuffers(n, out buffers);

        public void BindVertexArray(uint array) => _gl.BindVertexArray(array);

        public void BindBuffer(BufferTargetARB target, uint buffer) => _gl.BindBuffer(target, buffer);

        public void BufferData(BufferTargetARB target, uint size, void* data, BufferUsageARB usage) => _gl.BufferData(target, size, data, usage);

        public void BufferSubData(BufferTargetARB target, int offset, uint size, void* data) => _gl.BufferSubData(target, offset, size, data);

        public void EnableVertexAttribArray(uint index) => _gl.EnableVertexAttribArray(index);

        public void DisableVertexAttribArray(uint index) => _gl.DisableVertexAttribArray(index);

        public void VertexAttribPointer(uint index, int size, VertexAttribPointerType type, bool normalized, uint stride, void* pointer) => _gl.VertexAttribPointer(index, size, type, normalized, stride, pointer);

        public void DeleteVertexArray(uint array) => _gl.DeleteVertexArrays(1, ref array);

        public void DeleteBuffer(uint buffer) => _gl.DeleteBuffers(1, ref buffer);

        public void DeleteBuffers(uint n, uint* buffers) => _gl.DeleteBuffers(n, buffers);

        public void DrawArrays(PrimitiveType mode, int first, uint count) => _gl.DrawArrays(mode, first, count);

        public void DrawElements(PrimitiveType mode, uint count, DrawElementsType type, void* indices) => _gl.DrawElements(mode, count, type, indices);

        public void Clear(ClearBufferMask mask) => _gl.Clear(mask);

        public void ClearColor(float red, float green, float blue, float alpha) => _gl.ClearColor(red, green, blue, alpha);

        public void Viewport(int x, int y, uint width, uint height) => _gl.Viewport(x, y, width, height);

        public void Enable(EnableCap cap) => _gl.Enable(cap);

        public void Disable(EnableCap cap) => _gl.Disable(cap);

        public void BlendFunc(BlendingFactor src, BlendingFactor dst) => _gl.BlendFunc(src, dst);

        public void DepthMask(bool mask) => _gl.DepthMask(mask);

        public void DepthFunc(DepthFunction func) => _gl.DepthFunc(func);

        public void ColorMask(bool r, bool g, bool b, bool a) => _gl.ColorMask(r, g, b, a);

        public void ActiveTexture(TextureUnit unit) => _gl.ActiveTexture(unit);

        public void BindFramebuffer(FramebufferTarget target, uint framebuffer) => _gl.BindFramebuffer(target, framebuffer);

        public GLEnum CheckFramebufferStatus(FramebufferTarget target) => _gl.CheckFramebufferStatus(target);

        public void DrawBuffer(DrawBufferMode mode) => _gl.DrawBuffer(mode);

        public void ReadBuffer(ReadBufferMode mode) => _gl.ReadBuffer(mode);

        public void GenTextures(uint n, out uint textures) => _gl.GenTextures(n, out textures);

        public void BindTexture(TextureTarget target, uint texture) => _gl.BindTexture(target, texture);

        public void TexImage2D(TextureTarget target, int level, InternalFormat internalformat, uint width, uint height, int border, GLEnum format, GLEnum type, void* pixels) => _gl.TexImage2D(target, level, internalformat, width, height, border, format, type, pixels);

        public void TexParameter(TextureTarget target, TextureParameterName pname, int param) => _gl.TexParameter(target, pname, param);

        public void TexParameterf(TextureTarget target, TextureParameterName pname, float param) => _gl.TexParameter(target, pname, param);

        public void PixelStore(PixelStoreParameter pname, int param) => _gl.PixelStore(pname, param);

        public void DeleteTexture(uint texture) => _gl.DeleteTextures(1, ref texture);

        public void DeleteTextures(uint n, uint* textures) => _gl.DeleteTextures(n, textures);

        public uint CreateProgram() => _gl.CreateProgram();

        public uint CreateShader(ShaderType type) => _gl.CreateShader(type);

        public void ShaderSource(uint shader, string source) => _gl.ShaderSource(shader, source);

        public void CompileShader(uint shader) => _gl.CompileShader(shader);

        public void GetShader(uint shader, ShaderParameterName param, out int value) => _gl.GetShader(shader, param, out value);

        public string GetShaderInfoLog(uint shader) => _gl.GetShaderInfoLog(shader);

        public void AttachShader(uint program, uint shader) => _gl.AttachShader(program, shader);

        public void DetachShader(uint program, uint shader) => _gl.DetachShader(program, shader);

        public void LinkProgram(uint program) => _gl.LinkProgram(program);

        public void GetProgram(uint program, ProgramPropertyARB prop, out int value) => _gl.GetProgram(program, prop, out value);

        public string GetProgramInfoLog(uint program) => _gl.GetProgramInfoLog(program);

        public void DeleteShader(uint shader) => _gl.DeleteShader(shader);

        public void DeleteProgram(uint program) => _gl.DeleteProgram(program);

        public void UseProgram(uint program) => _gl.UseProgram(program);

        public int GetUniformLocation(uint program, string name) => _gl.GetUniformLocation(program, name);

        public void Uniform1(int location, float value) => _gl.Uniform1(location, value);

        public void Uniform1(int location, int value) => _gl.Uniform1(location, value);

        public void Uniform4(int location, float x, float y, float z, float w) => _gl.Uniform4(location, x, y, z, w);

        public void UniformMatrix4(int location, uint count, bool transpose, float* value) => _gl.UniformMatrix4(location, count, transpose, value);

        public GLEnum GetError() => _gl.GetError();

        public bool IsVertexArray(uint array) => _gl.IsVertexArray(array);

        public bool IsBuffer(uint buffer) => _gl.IsBuffer(buffer);

        public bool IsTexture(uint texture) => _gl.IsTexture(texture);

        public void GenerateMipmap(TextureTarget target) => _gl.GenerateMipmap(target);

        public bool IsExtensionPresent(string extension) => _gl.IsExtensionPresent(extension);

        public void GetFloat(GetPName pname, out float param) => _gl.GetFloat(pname, out param);
    }
}