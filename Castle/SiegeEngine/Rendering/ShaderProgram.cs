using SiegeEngine.Interfaces;
using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace SiegeEngine.Rendering
{
    public class ShaderProgram : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly uint _program;
        private bool _disposed;

        public ShaderProgram(IRenderContext renderContext, string vertexShaderSource, string fragmentShaderSource)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            if (string.IsNullOrEmpty(vertexShaderSource))
                throw new ArgumentNullException(nameof(vertexShaderSource));
            if (string.IsNullOrEmpty(fragmentShaderSource))
                throw new ArgumentNullException(nameof(fragmentShaderSource));

            uint vertexShader = _renderContext.CreateShader(ShaderType.VertexShader);
            _renderContext.ShaderSource(vertexShader, vertexShaderSource);
            _renderContext.CompileShader(vertexShader);
            _renderContext.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vsStatus);
            if (vsStatus != 1)
            {
                string infoLog = _renderContext.GetShaderInfoLog(vertexShader);
                _renderContext.DeleteShader(vertexShader);
                throw new Exception($"Vertex shader compilation failed: {infoLog}");
            }

            uint fragmentShader = _renderContext.CreateShader(ShaderType.FragmentShader);
            _renderContext.ShaderSource(fragmentShader, fragmentShaderSource);
            _renderContext.CompileShader(fragmentShader);
            _renderContext.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fsStatus);
            if (fsStatus != 1)
            {
                string infoLog = _renderContext.GetShaderInfoLog(fragmentShader);
                _renderContext.DeleteShader(vertexShader);
                _renderContext.DeleteShader(fragmentShader);
                throw new Exception($"Fragment shader compilation failed: {infoLog}");
            }

            _program = _renderContext.CreateProgram();
            _renderContext.AttachShader(_program, vertexShader);
            _renderContext.AttachShader(_program, fragmentShader);
            _renderContext.LinkProgram(_program);
            _renderContext.GetProgram(_program, ProgramPropertyARB.LinkStatus, out int linkStatus);
            if (linkStatus != 1)
            {
                string infoLog = _renderContext.GetProgramInfoLog(_program);
                _renderContext.DetachShader(_program, vertexShader);
                _renderContext.DetachShader(_program, fragmentShader);
                _renderContext.DeleteShader(vertexShader);
                _renderContext.DeleteShader(fragmentShader);
                _renderContext.DeleteProgram(_program);
                throw new Exception($"Shader program linking failed: {infoLog}");
            }

            _renderContext.DetachShader(_program, vertexShader);
            _renderContext.DetachShader(_program, fragmentShader);
            _renderContext.DeleteShader(vertexShader);
            _renderContext.DeleteShader(fragmentShader);
        }

        public void Use()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShaderProgram));
            _renderContext.UseProgram(_program);
        }

        public void SetUniform(string name, float value)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            int location = _renderContext.GetUniformLocation(_program, name);
            if (location == -1)
                throw new ArgumentException($"Uniform '{name}' not found in shader program.", nameof(name));
            _renderContext.Uniform1(location, value);
        }

        public void SetUniform(string name, int value)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            int location = _renderContext.GetUniformLocation(_program, name);
            if (location == -1)
                throw new ArgumentException($"Uniform '{name}' not found in shader program.", nameof(name));
            _renderContext.Uniform1(location, value);
        }

        public void SetUniform(string name, float x, float y, float z, float w)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            int location = _renderContext.GetUniformLocation(_program, name);
            if (location == -1)
                throw new ArgumentException($"Uniform '{name}' not found in shader program.", nameof(name));
            _renderContext.Uniform4(location, x, y, z, w);
        }

        public unsafe void SetMatrix4(string name, Matrix4x4 matrix)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            int location = _renderContext.GetUniformLocation(_program, name);
            if (location == -1)
                throw new ArgumentException($"Uniform '{name}' not found in shader program.", nameof(name));

            float[] matrixArray = new float[16]
            {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            };
            fixed (float* matrixPtr = matrixArray)
            {
                _renderContext.UniformMatrix4(location, 1, false, matrixPtr);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _renderContext.DeleteProgram(_program);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting shader program: {ex.Message}");
                }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}