// Folder: SiegeEngine/Core/Rendering/Compute
// File: ComputeProgram.cs
using System;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;

namespace SiegeEngine.Core.GPU.Compute
{
    public class ComputeProgram : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly GpuHandle _pipeline;
        private bool _disposed;

        public uint ProgramId => _pipeline.Id;
        public GpuHandle Pipeline => _pipeline;
        public ShaderId ShaderId { get; }

        public ComputeProgram(IRenderContext renderContext, ShaderId id)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            ShaderId = id;
            PipelineDesc desc = ShaderCatalog.Describe(id, _renderContext);
            if (string.IsNullOrEmpty(desc.ComputeSource))
                throw new InvalidOperationException($"ShaderId '{id}' is not compute.");
            _pipeline = _renderContext.CreatePipeline(desc);
            if (!_pipeline.IsValid)
                throw new Exception("Compute pipeline creation failed.");
        }

        public void Use()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.BindPipeline(_pipeline);
        }

        public void Dispatch(uint groupsX, uint groupsY = 1, uint groupsZ = 1)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComputeProgram));
            _renderContext.Dispatch(groupsX, groupsY, groupsZ);
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
