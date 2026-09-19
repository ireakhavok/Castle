// Folder: SiegeEngine/Core/Rendering/Compute
// File: ComputeProgram.cs
using System;
using System.Numerics;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Compute
{
    public class ComputeProgram : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly GpuHandle _pipeline;
        private bool _disposed;

        public uint ProgramId => _pipeline.Id;
        public GpuHandle Pipeline => _pipeline;

        public ComputeProgram(IRenderContext renderContext, string computeShaderSource)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            if (string.IsNullOrEmpty(computeShaderSource))
                throw new ArgumentNullException(nameof(computeShaderSource));
            _pipeline = _renderContext.CreatePipeline(new PipelineDesc { ComputeSource = computeShaderSource });
            if (!_pipeline.IsValid)
                throw new Exception("Compute pipeline creation failed.");
        }

        public void Use()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.BindPipeline(_pipeline);
        }

        public void SetUniform(string name, float value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.SetUniform(name, value);
        }

        public void SetUniform(string name, int value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.SetUniform(name, value);
        }

        public void SetUniform(string name, float x, float y)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.SetUniform(name, x, y);
        }

        public void SetUniform(string name, float x, float y, float z)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.SetUniform(name, x, y, z);
        }

        public void SetUniform(string name, float x, float y, float z, float w)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.SetUniform(name, x, y, z, w);
        }

        public unsafe void SetMatrix4(string name, Matrix4x4 matrix)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.SetUniformMatrix4(name, matrix);
        }

        public void Dispatch(uint groupsX, uint groupsY = 1, uint groupsZ = 1)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.DispatchCompute(groupsX, groupsY, groupsZ);
        }

        public void Barrier()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.MemoryBarrier(_renderContext.Enums.ShaderStorageBarrierBit);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try { if (_pipeline.IsValid) _renderContext.Destroy(_pipeline); }
                catch (Exception ex) { Console.WriteLine("Error deleting compute program: " + ex.Message); }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
