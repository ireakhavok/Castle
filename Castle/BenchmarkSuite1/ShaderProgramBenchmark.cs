using BenchmarkDotNet.Attributes;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.ContextManagement;
using System.Numerics;
using Microsoft.VSDiagnostics;

namespace SiegeEngine.Benchmarks
{
    [CPUUsageDiagnoser]
    public unsafe class ShaderProgramBenchmark
    {
        private ShaderProgram _shader;
        private FakeRenderContext _ctx;
        [GlobalSetup]
        public void Setup()
        {
            _ctx = new FakeRenderContext();
            // Minimal shader sources; FakeRenderContext will accept them
            _shader = new ShaderProgram(_ctx, "#version 330 core\nvoid main(){}", "#version 330 core\nvoid main(){}");
        }

        [Benchmark]
        public void SetUniformFloat()
        {
            // Call the user method that looks up uniform locations each time
            _shader.SetUniform("u_value", 1.0f);
        }

        // A simple fake IRenderContext implementation that satisfies ShaderProgram
        private unsafe class FakeRenderContext : IRenderContext
        {
            public AbstractRenderEnums Enums { get; } = new OpenGLEnums();
            public int ViewportWidth => 800;
            public int ViewportHeight => 600;

            public uint GenVertexArray() => 0;
            public void GenVertexArrays(uint n, out uint arrays)
            {
                arrays = 0;
            }

            public uint GenBuffer() => 0;
            public void GenBuffers(uint n, out uint buffers)
            {
                buffers = 0;
            }

            public void BindVertexArray(uint array)
            {
            }

            public void BindBuffer(int target, uint buffer)
            {
            }

            public void BufferData(int target, uint size, void* data, int usage)
            {
            }

            public void BufferSubData(int target, int offset, uint size, void* data)
            {
            }

            public void EnableVertexAttribArray(uint index)
            {
            }

            public void DisableVertexAttribArray(uint index)
            {
            }

            public void VertexAttribPointer(uint index, int size, int type, bool normalized, uint stride, void* pointer)
            {
            }

            public void VertexAttribIPointer(uint index, int size, int type, uint stride, void* pointer)
            {
            }

            public void DeleteVertexArray(uint array)
            {
            }

            public void DeleteBuffer(uint buffer)
            {
            }

            public void DeleteBuffers(uint n, uint* buffers)
            {
            }

            public void DrawArrays(int mode, int first, uint count)
            {
            }

            public void DrawElements(int mode, uint count, int type, void* indices)
            {
            }

            public void Clear(int mask)
            {
            }

            public void ClearColor(float red, float green, float blue, float alpha)
            {
            }

            public void Viewport(int x, int y, uint width, uint height)
            {
            }

            public void Enable(int cap)
            {
            }

            public void Disable(int cap)
            {
            }

            public void BlendFunc(int src, int dst)
            {
            }

            public void DepthMask(bool mask)
            {
            }

            public void DepthFunc(int func)
            {
            }

            public void ColorMask(bool r, bool g, bool b, bool a)
            {
            }

            public void ActiveTexture(int unit)
            {
            }

            public void BindFramebuffer(int target, uint framebuffer)
            {
            }

            public int CheckFramebufferStatus(int target) => 0;
            public void DrawBuffer(int mode)
            {
            }

            public void ReadBuffer(int mode)
            {
            }

            public void GenTextures(uint n, out uint textures)
            {
                textures = 0;
            }

            public void BindTexture(int target, uint texture)
            {
            }

            public void TexImage2D(int target, int level, int internalformat, uint width, uint height, int border, int format, int type, void* pixels)
            {
            }

            public void TexParameter(int target, int pname, int param)
            {
            }

            public void TexParameterf(int target, int pname, float param)
            {
            }

            public void PixelStore(int pname, int param)
            {
            }

            public void DeleteTexture(uint texture)
            {
            }

            public void DeleteTextures(uint n, uint* textures)
            {
            }

            public uint CreateProgram() => 1;
            public uint CreateShader(int type) => 1;
            public void ShaderSource(uint shader, string source)
            {
            }

            public void CompileShader(uint shader)
            {
            }

            public void GetShader(uint shader, int param, out int value)
            {
                value = 1;
            }

            public string GetShaderInfoLog(uint shader) => string.Empty;
            public void AttachShader(uint program, uint shader)
            {
            }

            public void DetachShader(uint program, uint shader)
            {
            }

            public void LinkProgram(uint program)
            {
            }

            public void GetProgram(uint program, int prop, out int value)
            {
                value = 1;
            }

            public string GetProgramInfoLog(uint program) => string.Empty;
            public void DeleteShader(uint shader)
            {
            }

            public void DeleteProgram(uint program)
            {
            }

            public void UseProgram(uint program)
            {
            }

            public int GetUniformLocation(uint program, string name)
            {
                // Simulate a lookup cost if desired; return a constant location
                return 3;
            }

            public void Uniform1(int location, float value)
            {
            }

            public void Uniform1(int location, int value)
            {
            }

            public void Uniform3(int location, float x, float y, float z)
            {
            }

            public void Uniform4(int location, float x, float y, float z, float w)
            {
            }

            public void UniformMatrix4(int location, uint count, bool transpose, float* value)
            {
            }

            public void UniformMatrix3(int location, uint count, bool transpose, float* value)
            {
            }

            public int GetError() => 0;
            public bool IsVertexArray(uint array) => false;
            public bool IsBuffer(uint buffer) => false;
            public bool IsTexture(uint texture) => false;
            public void GenerateMipmap(int target)
            {
            }

            public bool IsExtensionPresent(string extension) => false;
            public void GetFloat(int pname, out float param)
            {
                param = 0;
            }

            public void Scissor(int x, int y, uint width, uint height)
            {
            }

            public void CullFace(int mode)
            {
            }

            public void FrontFace(int mode)
            {
            }
        }
    }
}