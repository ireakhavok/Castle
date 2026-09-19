// Folder: SiegeEngine/Core/Rendering/Compute
// File: ShaderStorageBuffer.cs
using System;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Compute
{
    public unsafe class ShaderStorageBuffer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private GpuHandle _buffer;
        private uint _sizeInBytes;
        private bool _disposed;

        public uint BufferId => _buffer.Id;
        public GpuHandle Handle => _buffer;
        public uint SizeInBytes => _sizeInBytes;

        public ShaderStorageBuffer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _buffer = _renderContext.CreateBuffer(new BufferDesc
            {
                Target = _renderContext.Enums.ShaderStorageBuffer,
                Usage = _renderContext.Enums.DynamicDraw,
                ByteSize = 0
            });
        }

        public void SetData(uint sizeInBytes, void* data, int usage)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            _sizeInBytes = sizeInBytes;
            if (!_buffer.IsValid)
            {
                _buffer = _renderContext.CreateBuffer(new BufferDesc
                {
                    Target = _renderContext.Enums.ShaderStorageBuffer,
                    Usage = usage != 0 ? usage : _renderContext.Enums.DynamicDraw,
                    ByteSize = (int)sizeInBytes
                });
            }
            if (data == null)
            {
                _renderContext.UpdateBuffer(_buffer, ReadOnlySpan<byte>.Empty);
                return;
            }
            _renderContext.UpdateBuffer(_buffer, new ReadOnlySpan<byte>((byte*)data, (int)sizeInBytes));
        }

        public void SetSubData(int offset, uint size, void* data)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            if (data == null || size == 0) return;
            _renderContext.UpdateBuffer(_buffer, new ReadOnlySpan<byte>((byte*)data, (int)size), offset);
        }

        public void BindBase(uint bindingPoint)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            _renderContext.BindStorageBuffer(_buffer, (int)bindingPoint);
        }

        public void* Map(MapAccess access)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            return _renderContext.Map(_buffer, access);
        }

        public void* MapRange(int offset, uint length, MapAccess access)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            return _renderContext.Map(_buffer, offset, length, access);
        }

        public bool Unmap()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderStorageBuffer));
            _renderContext.Unmap(_buffer);
            return true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try { if (_buffer.IsValid) _renderContext.Destroy(_buffer); }
                catch (Exception ex) { Console.WriteLine("Error deleting SSBO: " + ex.Message); }
                _buffer = default;
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
