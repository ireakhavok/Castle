// Folder: SiegeEngine/Core/Rendering/Compute
// File: ShaderStorageBuffer.cs
using System;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Compute
{
    /// <summary>
    /// Simple SSBO wrapper for compute shaders.
    /// </summary>
    public unsafe class ShaderStorageBuffer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private uint _buffer;
        private uint _sizeInBytes;
        private bool _disposed;

        public uint BufferId => _buffer;
        public uint SizeInBytes => _sizeInBytes;

        public ShaderStorageBuffer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _buffer = _renderContext.GenBuffer();
        }

        /// <summary>
        /// Allocate / reallocate the buffer with the given size and usage.
        /// </summary>
        public void SetData(uint sizeInBytes, void* data, int usage)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            _sizeInBytes = sizeInBytes;
            _renderContext.BindBuffer(_renderContext.Enums.ShaderStorageBuffer, _buffer);
            _renderContext.BufferData(_renderContext.Enums.ShaderStorageBuffer, sizeInBytes, data, usage);
        }

        /// <summary>
        /// Update a portion of the buffer.
        /// </summary>
        public void SetSubData(int offset, uint size, void* data)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            _renderContext.BindBuffer(_renderContext.Enums.ShaderStorageBuffer, _buffer);
            _renderContext.BufferSubData(_renderContext.Enums.ShaderStorageBuffer, offset, size, data);
        }

        /// <summary>
        /// Bind this SSBO to the given binding point.
        /// </summary>
        public void BindBase(uint bindingPoint)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            _renderContext.BindBufferBase(_renderContext.Enums.ShaderStorageBuffer, bindingPoint, _buffer);
        }

        /// <summary>
        /// Map the entire buffer for reading or writing.
        /// Prefer MapRange for SSBO readback after compute.
        /// </summary>
        public void* Map(int access)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            _renderContext.BindBuffer(_renderContext.Enums.ShaderStorageBuffer, _buffer);
            return _renderContext.MapBuffer(_renderContext.Enums.ShaderStorageBuffer, access);
        }

        public void* MapRange(int offset, uint length, int access)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            _renderContext.BindBuffer(_renderContext.Enums.ShaderStorageBuffer, _buffer);
            return _renderContext.MapBufferRange(_renderContext.Enums.ShaderStorageBuffer, offset, length, access);
        }

        public bool Unmap()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            _renderContext.BindBuffer(_renderContext.Enums.ShaderStorageBuffer, _buffer);
            return _renderContext.UnmapBuffer(_renderContext.Enums.ShaderStorageBuffer);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try { _renderContext.DeleteBuffer(_buffer); }
                catch (Exception ex) { Console.WriteLine($"Error deleting SSBO: {ex.Message}"); }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}