using SiegeEngine.ContextManagement;
using SiegeEngine.Definitions;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Rendering
{
    public unsafe class VertexBuffer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private uint _vao;
        private uint _vbo;
        private uint _ebo;
        private uint _vertexCount;
        private uint _indexCount;
        private bool _disposed;

        public VertexBuffer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _vao = _renderContext.GenVertexArray();
            _vbo = _renderContext.GenBuffer();
            _ebo = _renderContext.GenBuffer();
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
            for (uint i = 0; i < vertices.Count / 7; i++)
                indices.Add(i);

            _vertexCount = (uint)(vertices.Count / 7);
            _indexCount = (uint)indices.Count;

            _renderContext.BindVertexArray(_vao);
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (float* vertexPtr = vertices.ToArray())
            {
                _renderContext.BufferData(BufferTargetARB.ArrayBuffer, (uint)(vertices.Count * sizeof(float)), vertexPtr, BufferUsageARB.DynamicDraw);
            }

            _renderContext.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            fixed (uint* indexPtr = indices.ToArray())
            {
                _renderContext.BufferData(BufferTargetARB.ElementArrayBuffer, (uint)(indices.Count * sizeof(uint)), indexPtr, BufferUsageARB.DynamicDraw);
            }

            uint stride = 7 * sizeof(float);
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        }

        public void Bind()
        {
            _renderContext.BindVertexArray(_vao);
        }

        public uint GetVertexCount() => _vertexCount;

        public uint GetIndexCount() => _indexCount;

        public void Dispose()
        {
            if (!_disposed)
            {
                _renderContext.DeleteVertexArray(_vao);
                _renderContext.DeleteBuffer(_vbo);
                _renderContext.DeleteBuffer(_ebo);
                _disposed = true;
            }
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

            _renderContext.BindVertexArray(_vao);
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (float* vertexPtr = vertexData)
            {
                _renderContext.BufferData(BufferTargetARB.ArrayBuffer, (uint)(vertexData.Length * sizeof(float)), vertexPtr, BufferUsageARB.DynamicDraw);
            }

            _renderContext.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            fixed (uint* indexPtr = indices.ToArray())
            {
                _renderContext.BufferData(BufferTargetARB.ElementArrayBuffer, (uint)(indices.Count * sizeof(uint)), indexPtr, BufferUsageARB.DynamicDraw);
            }

            uint stride = 7 * sizeof(float);
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        }

        public void UpdateCustomWithUV(List<float> vertices, List<uint> indices)
        {
            _vertexCount = (uint)(vertices.Count / 9);
            _indexCount = (uint)indices.Count;

            _renderContext.BindVertexArray(_vao);
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (float* vertexPtr = vertices.ToArray())
            {
                _renderContext.BufferData(BufferTargetARB.ArrayBuffer, (uint)(vertices.Count * sizeof(float)), vertexPtr, BufferUsageARB.DynamicDraw);
            }

            _renderContext.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            fixed (uint* indexPtr = indices.ToArray())
            {
                _renderContext.BufferData(BufferTargetARB.ElementArrayBuffer, (uint)(indices.Count * sizeof(uint)), indexPtr, BufferUsageARB.DynamicDraw);
            }

            uint stride = 9 * sizeof(float);
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
            _renderContext.EnableVertexAttribArray(2);
            _renderContext.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(7 * sizeof(float)));
        }

        public void UpdateWithPositionNormalUV(List<float> vertices, List<uint> indices)
        {
            _vertexCount = (uint)(vertices.Count / 9); // Updated for material index
            _indexCount = (uint)indices.Count;

            _renderContext.BindVertexArray(_vao);
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (float* vertexPtr = vertices.ToArray())
            {
                _renderContext.BufferData(BufferTargetARB.ArrayBuffer, (uint)(vertices.Count * sizeof(float)), vertexPtr, BufferUsageARB.DynamicDraw);
            }

            _renderContext.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            fixed (uint* indexPtr = indices.ToArray())
            {
                _renderContext.BufferData(BufferTargetARB.ElementArrayBuffer, (uint)(indices.Count * sizeof(uint)), indexPtr, BufferUsageARB.DynamicDraw);
            }

            uint stride = 9 * sizeof(float);
            _renderContext.EnableVertexAttribArray(0); // Position
            _renderContext.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            _renderContext.EnableVertexAttribArray(3); // Normals
            _renderContext.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
            _renderContext.EnableVertexAttribArray(2); // UVs
            _renderContext.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
            _renderContext.EnableVertexAttribArray(4); // MaterialIndex
            _renderContext.VertexAttribPointer(4, 1, VertexAttribPointerType.Float, false, stride, (void*)(8 * sizeof(float)));
        }
    }
}