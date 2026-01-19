using SiegeEngine.Core.ContextManagement;
using System;
using System.Numerics;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.AssetParsing.V2.Model;
namespace SiegeEngine.Core.Rendering
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
            uint vertexShader = _renderContext.CreateShader(_renderContext.Enums.VertexShader);
            _renderContext.ShaderSource(vertexShader, vertexShaderSource);
            _renderContext.CompileShader(vertexShader);
            _renderContext.GetShader(vertexShader, _renderContext.Enums.CompileStatus, out int vsStatus);
            if (vsStatus != 1)
            {
                string infoLog = _renderContext.GetShaderInfoLog(vertexShader);
                _renderContext.DeleteShader(vertexShader);
                throw new Exception($"Vertex shader compilation failed: {infoLog}");
            }
            uint fragmentShader = _renderContext.CreateShader(_renderContext.Enums.FragmentShader);
            _renderContext.ShaderSource(fragmentShader, fragmentShaderSource);
            _renderContext.CompileShader(fragmentShader);
            _renderContext.GetShader(fragmentShader, _renderContext.Enums.CompileStatus, out int fsStatus);
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
            _renderContext.GetProgram(_program, _renderContext.Enums.LinkStatus, out int linkStatus);
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
        public void SetUniform(string name, float x, float y, float z)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            int location = _renderContext.GetUniformLocation(_program, name);
            if (location == -1)
                throw new ArgumentException($"Uniform '{name}' not found in shader program.", nameof(name));
            _renderContext.Uniform3(location, x, y, z);
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
        public unsafe void SetMatrix4Array(string name, Matrix4x4[] matrices)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            int location = _renderContext.GetUniformLocation(_program, name);
            if (location == -1)
                return;
            float[] data = new float[matrices.Length * 16];
            for (int i = 0; i < matrices.Length; i++)
            {
                data[i * 16 + 0] = matrices[i].M11;
                data[i * 16 + 1] = matrices[i].M12;
                data[i * 16 + 2] = matrices[i].M13;
                data[i * 16 + 3] = matrices[i].M14;
                data[i * 16 + 4] = matrices[i].M21;
                data[i * 16 + 5] = matrices[i].M22;
                data[i * 16 + 6] = matrices[i].M23;
                data[i * 16 + 7] = matrices[i].M24;
                data[i * 16 + 8] = matrices[i].M31;
                data[i * 16 + 9] = matrices[i].M32;
                data[i * 16 + 10] = matrices[i].M33;
                data[i * 16 + 11] = matrices[i].M34;
                data[i * 16 + 12] = matrices[i].M41;
                data[i * 16 + 13] = matrices[i].M42;
                data[i * 16 + 14] = matrices[i].M43;
                data[i * 16 + 15] = matrices[i].M44;
            }
            fixed (float* ptr = data)
            {
                _renderContext.UniformMatrix4(location, (uint)matrices.Length, false, ptr);
            }
        }
        public unsafe void SetMatrix3Array(string name, SiegeEngine.Core.AssetParsing.Model.Matrix3x3[] matrices)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            int location = _renderContext.GetUniformLocation(_program, name);
            if (location == -1)
                return;
            float[] data = new float[matrices.Length * 9];
            for (int i = 0; i < matrices.Length; i++)
            {
                data[i * 9 + 0] = matrices[i].M11;
                data[i * 9 + 1] = matrices[i].M12;
                data[i * 9 + 2] = matrices[i].M13;
                data[i * 9 + 3] = matrices[i].M21;
                data[i * 9 + 4] = matrices[i].M22;
                data[i * 9 + 5] = matrices[i].M23;
                data[i * 9 + 6] = matrices[i].M31;
                data[i * 9 + 7] = matrices[i].M32;
                data[i * 9 + 8] = matrices[i].M33;
            }
            fixed (float* ptr = data)
            {
                _renderContext.UniformMatrix3(location, (uint)matrices.Length, false, ptr);
            }
        }
        public unsafe void SetMatrix3Array(string name, SiegeEngine.Core.AssetParsing.V2.Model.Matrix3x3[] matrices)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            int location = _renderContext.GetUniformLocation(_program, name);
            if (location == -1)
                return;
            float[] data = new float[matrices.Length * 9];
            for (int i = 0; i < matrices.Length; i++)
            {
                data[i * 9 + 0] = matrices[i].M11;
                data[i * 9 + 1] = matrices[i].M12;
                data[i * 9 + 2] = matrices[i].M13;
                data[i * 9 + 3] = matrices[i].M21;
                data[i * 9 + 4] = matrices[i].M22;
                data[i * 9 + 5] = matrices[i].M23;
                data[i * 9 + 6] = matrices[i].M31;
                data[i * 9 + 7] = matrices[i].M32;
                data[i * 9 + 8] = matrices[i].M33;
            }
            fixed (float* ptr = data)
            {
                _renderContext.UniformMatrix3(location, (uint)matrices.Length, false, ptr);
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