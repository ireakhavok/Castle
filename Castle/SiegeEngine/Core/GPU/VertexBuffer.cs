// Folder: SiegeEngine.Core/Rendering
// File: VertexBuffer.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using System;
using System.Collections.Generic;
using System.Numerics;
namespace SiegeEngine.Core.GPU
{
    public unsafe class VertexBuffer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private uint _vertexCount;
        private uint _indexCount;
        private int _stride;
        private GpuHandle _vertexHandle;
        private GpuHandle _indexHandle;
        private bool _disposed;
        public int Stride => _stride;
        public GpuHandle VertexHandle => _vertexHandle;
        public GpuHandle IndexHandle => _indexHandle;
        public VertexBuffer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _vertexHandle = _renderContext.CreateBuffer(new BufferDesc
            {
                Target = _renderContext.Enums.ArrayBuffer,
                Usage = _renderContext.Enums.DynamicDraw
            });
            _indexHandle = _renderContext.CreateBuffer(new BufferDesc
            {
                Target = _renderContext.Enums.ElementArrayBuffer,
                Usage = _renderContext.Enums.DynamicDraw
            });
        }
        public void Bind()
        {
            _renderContext.BindMesh(_vertexHandle, _indexHandle, _stride);
        }
        public uint GetVertexCount() => _vertexCount;
        public uint GetIndexCount() => _indexCount;
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_vertexHandle.IsValid)
                    _renderContext.Destroy(_vertexHandle);
                if (_indexHandle.IsValid)
                    _renderContext.Destroy(_indexHandle);
                _disposed = true;
            }
        }

        void Upload(float[] vertexData, uint[] indices, int strideFloats)
        {
            _stride = strideFloats * sizeof(float);
            if (vertexData != null && vertexData.Length > 0)
            {
                fixed (float* p = vertexData)
                    _renderContext.UpdateBuffer(_vertexHandle, new ReadOnlySpan<byte>((byte*)p, vertexData.Length * sizeof(float)));
            }
            if (indices != null && indices.Length > 0)
            {
                fixed (uint* p = indices)
                    _renderContext.UpdateBuffer(_indexHandle, new ReadOnlySpan<byte>((byte*)p, indices.Length * sizeof(uint)));
            }
        }

        public void Update(List<Entity> entities)
        {
            var vertices = new List<float>();
            foreach (var entity in entities)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics != null && entity.Type == "Water")
                {
                    Vector3 pos = physics.Position;
                    vertices.Add(pos.X);
                    vertices.Add(pos.Y);
                    vertices.Add(pos.Z);
                    vertices.Add(0.0f);
                    vertices.Add(0.5f);
                    vertices.Add(1.0f);
                    vertices.Add(1.0f);
                }
            }
            var indices = new List<uint>();
            for (uint i = 0; i < vertices.Count / 7; i++) indices.Add(i);
            _vertexCount = (uint)(vertices.Count / 7);
            _indexCount = (uint)indices.Count;
            Upload(vertices.ToArray(), indices.ToArray(), 7);
        }

        public void UpdateCustom(List<Vertex> vertices, List<uint> indices)
        {
            _vertexCount = (uint)vertices.Count;
            _indexCount = (uint)indices.Count;
            var vertexData = new float[vertices.Count * 7];
            for (int i = 0; i < vertices.Count; i++)
            {
                vertexData[i * 7] = vertices[i].X;
                vertexData[i * 7 + 1] = vertices[i].Y;
                vertexData[i * 7 + 2] = vertices[i].Z;
                vertexData[i * 7 + 3] = vertices[i].R;
                vertexData[i * 7 + 4] = vertices[i].G;
                vertexData[i * 7 + 5] = vertices[i].B;
                vertexData[i * 7 + 6] = vertices[i].A;
            }
            Upload(vertexData, indices.ToArray(), 7);
        }

        public void UpdateCustomWithUV(List<float> vertices, List<uint> indices)
        {
            _vertexCount = (uint)(vertices.Count / 9);
            _indexCount = (uint)indices.Count;
            Upload(vertices.ToArray(), indices.ToArray(), 9);
        }

        public void UpdateWithPositionNormalUV(List<float> vertices, List<uint> indices)
        {
            _vertexCount = (uint)(vertices.Count / 9);
            _indexCount = (uint)indices.Count;
            Upload(vertices.ToArray(), indices.ToArray(), 9);
        }

        public void UpdateVerticesPartial(List<float> vertices, int startVertexIndex, int vertexCount, int stride = 9)
        {
            if (vertexCount <= 0 || startVertexIndex < 0 || vertices == null) return;
            int startElement = startVertexIndex * stride;
            int elementCount = vertexCount * stride;
            if (startElement + elementCount > vertices.Count) return;
            int byteOffset = startElement * sizeof(float);
            float[] tempSlice = new float[elementCount];
            for (int i = 0; i < elementCount; i++)
                tempSlice[i] = vertices[startElement + i];
            fixed (float* vertexPtr = tempSlice)
            {
                _renderContext.UpdateBuffer(_vertexHandle, new ReadOnlySpan<byte>((byte*)vertexPtr, elementCount * sizeof(float)), byteOffset);
            }
        }
    }
}
