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
        private readonly OpenGLRenderContext _gl;
        private uint _vao;
        private uint _vbo;
        private uint _ebo;
        private uint _vertexCount;
        private uint _indexCount;
        private int _stride;
        private GpuHandle _vertexHandle;
        private GpuHandle _indexHandle;
        private bool _disposed;
        public uint Vao => _vao;
        public int Stride => _stride;
        public GpuHandle VertexHandle => _vertexHandle;
        public GpuHandle IndexHandle => _indexHandle;
        public VertexBuffer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _gl = Gl.Of(renderContext);
            _vao = _gl.GenVertexArray();
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
            _vbo = _vertexHandle.Id;
            _ebo = _indexHandle.Id;
        }
        public void Bind()
        {
            _gl.BindVertexArray(_vao);
        }
        public uint GetVertexCount() => _vertexCount;
        public uint GetIndexCount() => _indexCount;
        public void Dispose()
        {
            if (!_disposed)
            {
                _gl.DeleteVertexArray(_vao);
                if (_vertexHandle.IsValid)
                    _renderContext.Destroy(_vertexHandle);
                if (_indexHandle.IsValid)
                    _renderContext.Destroy(_indexHandle);
                _disposed = true;
            }
        }

        void BindUpload(int strideBytes, int layout)
        {
            _stride = strideBytes;
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(_renderContext.Enums.ArrayBuffer, _vbo);
            _gl.BindBuffer(_renderContext.Enums.ElementArrayBuffer, _ebo);
            uint stride = (uint)strideBytes;
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false, stride, (void*)0);
            if (layout == 7)
            {
                _gl.EnableVertexAttribArray(1);
                _gl.VertexAttribPointer(1, 4, _renderContext.Enums.Float, false, stride, (void*)(3 * sizeof(float)));
            }
            else if (layout == 9)
            {
                _gl.EnableVertexAttribArray(1);
                _gl.VertexAttribPointer(1, 4, _renderContext.Enums.Float, false, stride, (void*)(3 * sizeof(float)));
                _gl.EnableVertexAttribArray(2);
                _gl.VertexAttribPointer(2, 2, _renderContext.Enums.Float, false, stride, (void*)(7 * sizeof(float)));
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
                    vertices.Add(pos.X); vertices.Add(pos.Y); vertices.Add(pos.Z);
                    vertices.Add(0.0f); vertices.Add(0.5f); vertices.Add(1.0f); vertices.Add(1.0f);
                }
            }
            var indices = new List<uint>();
            for (uint i = 0; i < vertices.Count / 7; i++) indices.Add(i);
            _vertexCount = (uint)(vertices.Count / 7);
            _indexCount = (uint)indices.Count;
            UploadFloats(vertices.ToArray(), indices.ToArray(), 7, colorUv: false);
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
            UploadFloats(vertexData, indices.ToArray(), 7, colorUv: false);
        }

        public void UpdateCustomWithUV(List<float> vertices, List<uint> indices)
        {
            _vertexCount = (uint)(vertices.Count / 9);
            _indexCount = (uint)indices.Count;
            UploadFloats(vertices.ToArray(), indices.ToArray(), 9, colorUv: true);
        }

        public void UpdateWithPositionNormalUV(List<float> vertices, List<uint> indices)
        {
            _vertexCount = (uint)(vertices.Count / 9);
            _indexCount = (uint)indices.Count;
            _stride = 9 * sizeof(float);
            _gl.BindVertexArray(_vao);
            fixed (float* vertexPtr = vertices.ToArray())
            {
                _gl.BindBuffer(_renderContext.Enums.ArrayBuffer, _vbo);
                _gl.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(vertices.Count * sizeof(float)), vertexPtr, _renderContext.Enums.DynamicDraw);
            }
            uint[] idx = indices.ToArray();
            fixed (uint* indexPtr = idx)
            {
                _gl.BindBuffer(_renderContext.Enums.ElementArrayBuffer, _ebo);
                _gl.BufferData(_renderContext.Enums.ElementArrayBuffer, (uint)(idx.Length * sizeof(uint)), indexPtr, _renderContext.Enums.DynamicDraw);
            }
            uint stride = (uint)_stride;
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false, stride, (void*)0);
            _gl.EnableVertexAttribArray(3);
            _gl.VertexAttribPointer(3, 3, _renderContext.Enums.Float, false, stride, (void*)(3 * sizeof(float)));
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 2, _renderContext.Enums.Float, false, stride, (void*)(6 * sizeof(float)));
            _gl.EnableVertexAttribArray(4);
            _gl.VertexAttribPointer(4, 1, _renderContext.Enums.Float, false, stride, (void*)(8 * sizeof(float)));
        }

        public void UpdateVerticesPartial(List<float> vertices, int startVertexIndex, int vertexCount, int stride = 9)
        {
            if (vertexCount <= 0 || startVertexIndex < 0 || vertices == null) return;
            int startElement = startVertexIndex * stride;
            int elementCount = vertexCount * stride;
            if (startElement + elementCount > vertices.Count) return;
            int byteOffset = startElement * sizeof(float);
            uint byteSize = (uint)(elementCount * sizeof(float));
            float[] tempSlice = new float[elementCount];
            for (int i = 0; i < elementCount; i++)
                tempSlice[i] = vertices[startElement + i];
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(_renderContext.Enums.ArrayBuffer, _vbo);
            fixed (float* vertexPtr = tempSlice)
            {
                _gl.BufferSubData(_renderContext.Enums.ArrayBuffer, byteOffset, byteSize, vertexPtr);
            }
        }

        void UploadFloats(float[] vertexData, uint[] indices, int floatsPerVertex, bool colorUv)
        {
            _stride = floatsPerVertex * sizeof(float);
            _gl.BindVertexArray(_vao);
            fixed (float* vertexPtr = vertexData)
            {
                _gl.BindBuffer(_renderContext.Enums.ArrayBuffer, _vbo);
                _gl.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(vertexData.Length * sizeof(float)), vertexPtr, _renderContext.Enums.DynamicDraw);
            }
            fixed (uint* indexPtr = indices)
            {
                _gl.BindBuffer(_renderContext.Enums.ElementArrayBuffer, _ebo);
                _gl.BufferData(_renderContext.Enums.ElementArrayBuffer, (uint)(indices.Length * sizeof(uint)), indexPtr, _renderContext.Enums.DynamicDraw);
            }
            uint stride = (uint)_stride;
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false, stride, (void*)0);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 4, _renderContext.Enums.Float, false, stride, (void*)(3 * sizeof(float)));
            if (colorUv)
            {
                _gl.EnableVertexAttribArray(2);
                _gl.VertexAttribPointer(2, 2, _renderContext.Enums.Float, false, stride, (void*)(7 * sizeof(float)));
            }
        }
    }
}
