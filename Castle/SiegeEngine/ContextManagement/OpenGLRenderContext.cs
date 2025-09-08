using Silk.NET.OpenGL;
using Silk.NET.GLFW;
using System;

namespace SiegeEngine.ContextManagement
{
    public unsafe class OpenGLRenderContext : IRenderContext
    {
        private readonly GL _gl;
        private readonly Glfw _glfw;
        private readonly AbstractRenderEnums _enums = new OpenGLEnums();

        public AbstractRenderEnums Enums => _enums;

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
        public void BindBuffer(int target, uint buffer) => _gl.BindBuffer((BufferTargetARB)target, buffer);
        public void BufferData(int target, uint size, void* data, int usage) => _gl.BufferData((BufferTargetARB)target, size, data, (BufferUsageARB)usage);
        public void BufferSubData(int target, int offset, uint size, void* data) => _gl.BufferSubData((BufferTargetARB)target, offset, size, data);
        public void EnableVertexAttribArray(uint index) => _gl.EnableVertexAttribArray(index);
        public void DisableVertexAttribArray(uint index) => _gl.DisableVertexAttribArray(index);
        public void VertexAttribPointer(uint index, int size, int type, bool normalized, uint stride, void* pointer) => _gl.VertexAttribPointer(index, size, (VertexAttribPointerType)type, normalized, stride, pointer);
        public void DeleteVertexArray(uint array) => _gl.DeleteVertexArrays(1, ref array);
        public void DeleteBuffer(uint buffer) => _gl.DeleteBuffers(1, ref buffer);
        public void DeleteBuffers(uint n, uint* buffers) => _gl.DeleteBuffers(n, buffers);
        public void DrawArrays(int mode, int first, uint count) => _gl.DrawArrays((PrimitiveType)mode, first, count);
        public void DrawElements(int mode, uint count, int type, void* indices) => _gl.DrawElements((PrimitiveType)mode, count, (DrawElementsType)type, indices);
        public void Clear(int mask) => _gl.Clear((ClearBufferMask)mask);
        public void ClearColor(float red, float green, float blue, float alpha) => _gl.ClearColor(red, green, blue, alpha);
        public void Viewport(int x, int y, uint width, uint height) => _gl.Viewport(x, y, width, height);
        public void Enable(int cap) => _gl.Enable((EnableCap)cap);
        public void Disable(int cap) => _gl.Disable((EnableCap)cap);
        public void BlendFunc(int src, int dst) => _gl.BlendFunc((BlendingFactor)src, (BlendingFactor)dst);
        public void DepthMask(bool mask) => _gl.DepthMask(mask);
        public void DepthFunc(int func) => _gl.DepthFunc((DepthFunction)func);
        public void ColorMask(bool r, bool g, bool b, bool a) => _gl.ColorMask(r, g, b, a);
        public void ActiveTexture(int unit) => _gl.ActiveTexture((TextureUnit)unit);
        public void BindFramebuffer(int target, uint framebuffer) => _gl.BindFramebuffer((FramebufferTarget)target, framebuffer);
        public int CheckFramebufferStatus(int target) => (int)_gl.CheckFramebufferStatus((FramebufferTarget)target);
        public void DrawBuffer(int mode) => _gl.DrawBuffer((DrawBufferMode)mode);
        public void ReadBuffer(int mode) => _gl.ReadBuffer((ReadBufferMode)mode);
        public void GenTextures(uint n, out uint textures) => _gl.GenTextures(n, out textures);
        public void BindTexture(int target, uint texture) => _gl.BindTexture((TextureTarget)target, texture);
        public void TexImage2D(int target, int level, int internalformat, uint width, uint height, int border, int format, int type, void* pixels) => _gl.TexImage2D((TextureTarget)target, level, (InternalFormat)internalformat, width, height, border, (GLEnum)format, (GLEnum)type, pixels);
        public void TexParameter(int target, int pname, int param) => _gl.TexParameter((TextureTarget)target, (TextureParameterName)pname, param);
        public void TexParameterf(int target, int pname, float param) => _gl.TexParameter((TextureTarget)target, (TextureParameterName)pname, param);
        public void PixelStore(int pname, int param) => _gl.PixelStore((PixelStoreParameter)pname, param);
        public void DeleteTexture(uint texture) => _gl.DeleteTextures(1, ref texture);
        public void DeleteTextures(uint n, uint* textures) => _gl.DeleteTextures(n, textures);
        public uint CreateProgram() => _gl.CreateProgram();
        public uint CreateShader(int type) => _gl.CreateShader((ShaderType)type);
        public void ShaderSource(uint shader, string source) => _gl.ShaderSource(shader, source);
        public void CompileShader(uint shader) => _gl.CompileShader(shader);
        public void GetShader(uint shader, int param, out int value) => _gl.GetShader(shader, (ShaderParameterName)param, out value);
        public string GetShaderInfoLog(uint shader) => _gl.GetShaderInfoLog(shader);
        public void AttachShader(uint program, uint shader) => _gl.AttachShader(program, shader);
        public void DetachShader(uint program, uint shader) => _gl.DetachShader(program, shader);
        public void LinkProgram(uint program) => _gl.LinkProgram(program);
        public void GetProgram(uint program, int prop, out int value) => _gl.GetProgram(program, (ProgramPropertyARB)prop, out value);
        public string GetProgramInfoLog(uint program) => _gl.GetProgramInfoLog(program);
        public void DeleteShader(uint shader) => _gl.DeleteShader(shader);
        public void DeleteProgram(uint program) => _gl.DeleteProgram(program);
        public void UseProgram(uint program) => _gl.UseProgram(program);
        public int GetUniformLocation(uint program, string name) => _gl.GetUniformLocation(program, name);
        public void Uniform1(int location, float value) => _gl.Uniform1(location, value);
        public void Uniform1(int location, int value) => _gl.Uniform1(location, value);
        public void Uniform4(int location, float x, float y, float z, float w) => _gl.Uniform4(location, x, y, z, w);
        public void UniformMatrix4(int location, uint count, bool transpose, float* value) => _gl.UniformMatrix4(location, count, transpose, value);
        public int GetError() => (int)_gl.GetError();
        public bool IsVertexArray(uint array) => _gl.IsVertexArray(array);
        public bool IsBuffer(uint buffer) => _gl.IsBuffer(buffer);
        public bool IsTexture(uint texture) => _gl.IsTexture(texture);
        public void GenerateMipmap(int target) => _gl.GenerateMipmap((TextureTarget)target);
        public bool IsExtensionPresent(string extension) => _gl.IsExtensionPresent(extension);
        public void GetFloat(int pname, out float param) => _gl.GetFloat((GetPName)pname, out param);
    }
}