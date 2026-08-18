// Folder: SiegeEngine/Core/Rendering/Compute
// File: ComputeProgram.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Compute
{
    /// <summary>
    /// Minimal compute-shader program wrapper.
    /// Mirrors ShaderProgram but is restricted to a single compute stage.
    /// </summary>
    public class ComputeProgram : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly uint _program;
        private bool _disposed;
        private readonly Dictionary<string, int> _uniformLocations = new Dictionary<string, int>();

        public uint ProgramId => _program;

        public ComputeProgram(IRenderContext renderContext, string computeShaderSource)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            if (string.IsNullOrEmpty(computeShaderSource))
                throw new ArgumentNullException(nameof(computeShaderSource));

            uint computeShader = _renderContext.CreateShader(_renderContext.Enums.ComputeShader);
            _renderContext.ShaderSource(computeShader, computeShaderSource);
            _renderContext.CompileShader(computeShader);
            _renderContext.GetShader(computeShader, _renderContext.Enums.CompileStatus, out int compileStatus);
            if (compileStatus != 1)
            {
                string infoLog = _renderContext.GetShaderInfoLog(computeShader);
                _renderContext.DeleteShader(computeShader);
                throw new Exception($"Compute shader compilation failed: {infoLog}");
            }

            _program = _renderContext.CreateProgram();
            _renderContext.AttachShader(_program, computeShader);
            _renderContext.LinkProgram(_program);
            _renderContext.GetProgram(_program, _renderContext.Enums.LinkStatus, out int linkStatus);
            if (linkStatus != 1)
            {
                string infoLog = _renderContext.GetProgramInfoLog(_program);
                _renderContext.DetachShader(_program, computeShader);
                _renderContext.DeleteShader(computeShader);
                _renderContext.DeleteProgram(_program);
                throw new Exception($"Compute program linking failed: {infoLog}");
            }

            _renderContext.DetachShader(_program, computeShader);
            _renderContext.DeleteShader(computeShader);
        }

        private int GetLocation(string name)
        {
            if (_uniformLocations.TryGetValue(name, out int loc))
                return loc;
            loc = _renderContext.GetUniformLocation(_program, name);
            _uniformLocations[name] = loc;
            return loc;
        }

        public void Use()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.UseProgram(_program);
        }

        public void SetUniform(string name, float value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            int location = GetLocation(name);
            if (location == -1) return;
            _renderContext.Uniform1(location, value);
        }

        public void SetUniform(string name, int value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            int location = GetLocation(name);
            if (location == -1) return;
            _renderContext.Uniform1(location, value);
        }

        public void SetUniform(string name, float x, float y)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            int location = GetLocation(name);
            if (location == -1) return;
            _renderContext.Uniform2(location, x, y);
        }

        public void SetUniform(string name, float x, float y, float z)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            int location = GetLocation(name);
            if (location == -1) return;
            _renderContext.Uniform3(location, x, y, z);
        }

        public void SetUniform(string name, float x, float y, float z, float w)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            int location = GetLocation(name);
            if (location == -1) return;
            _renderContext.Uniform4(location, x, y, z, w);
        }

        public unsafe void SetMatrix4(string name, Matrix4x4 matrix)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            int location = GetLocation(name);
            if (location == -1) return;
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

        /// <summary>
        /// Dispatch the compute shader.
        /// </summary>
        public void Dispatch(uint groupsX, uint groupsY = 1, uint groupsZ = 1)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.DispatchCompute(groupsX, groupsY, groupsZ);
        }

        /// <summary>
        /// Insert a memory barrier so subsequent stages see the SSBO writes.
        /// </summary>
        public void Barrier()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.MemoryBarrier(_renderContext.Enums.ShaderStorageBarrierBit);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try { _renderContext.DeleteProgram(_program); }
                catch (Exception ex) { Console.WriteLine($"Error deleting compute program: {ex.Message}"); }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}