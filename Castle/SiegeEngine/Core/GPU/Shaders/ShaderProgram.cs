// Folder: SiegeEngine.Core.GPU
// File: ShaderProgram.cs
using System;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Shaders
{
    public class ShaderProgram : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly GpuHandle _pipeline;
        private bool _disposed;

        public ShaderId ShaderId { get; private set; }
        public GpuHandle Pipeline => _pipeline;

        ShaderProgram(IRenderContext renderContext, in PipelineDesc desc)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _pipeline = _renderContext.CreatePipeline(desc);
        }

        public static ShaderProgram FromId(IRenderContext renderContext, ShaderId id)
        {
            if (renderContext == null)
                throw new ArgumentNullException(nameof(renderContext));
            PipelineDesc desc = ShaderCatalog.Describe(id, renderContext);
            if (!string.IsNullOrEmpty(desc.ComputeSource) && string.IsNullOrEmpty(desc.VertexSource))
                throw new InvalidOperationException($"ShaderId '{id}' is compute.");
            var program = new ShaderProgram(renderContext, desc);
            program.ShaderId = id;
            return program;
        }

        public void Use()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShaderProgram));
            _renderContext.BindPipeline(_pipeline);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try { if (_pipeline.IsValid) _renderContext.Destroy(_pipeline); }
                catch (Exception ex) { Console.WriteLine($"Error deleting shader program: {ex.Message}"); }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
