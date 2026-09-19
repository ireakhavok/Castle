// Folder: SiegeEngine/Core/GPU/ContextManagement
// File: IRenderContext.cs
using System;
using System.Numerics;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    public unsafe interface IRenderContext
    {
        AbstractRenderEnums Enums { get; }
        int ViewportWidth { get; }
        int ViewportHeight { get; }
        RenderBackend Backend { get; }

        void Clear(int mask);
        void ClearColor(float red, float green, float blue, float alpha);
        void Viewport(int x, int y, uint width, uint height);
        void Enable(int cap);
        void Disable(int cap);
        void BlendFunc(int src, int dst);
        void DepthMask(bool mask);
        void DepthFunc(int func);
        void ColorMask(bool r, bool g, bool b, bool a);
        void Scissor(int x, int y, uint width, uint height);
        void CullFace(int mode);
        void FrontFace(int mode);
        void LineWidth(float width);
        int GetError();
        bool IsExtensionPresent(string extension);
        void GetFloat(int pname, out float param);
        void GetInteger(int pname, out int data);
        void GetInteger(int pname, int* data);

        void ReadPixels(int x, int y, uint width, uint height, int format, int type, void* data);
        void ClearBufferuiv(int buffer, int drawbuffer, uint* value);
        void MemoryBarrier(int barriers);
        uint FenceSync(int condition, uint flags);
        int ClientWaitSync(uint sync, uint flags, ulong timeout);
        void DeleteSync(uint sync);

        GpuHandle CreatePipeline(in PipelineDesc desc);
        GpuHandle CreateBuffer(in BufferDesc desc);
        GpuHandle CreateTexture(in TextureDesc desc);
        GpuHandle CreateRenderTarget(in RenderTargetDesc desc);
        GpuHandle GetRenderTargetColor(GpuHandle target);
        GpuHandle GetRenderTargetDepth(GpuHandle target);
        void BindRenderTargetFace(GpuHandle target, int face);
        void Destroy(GpuHandle handle);
        void BindPipeline(GpuHandle pipeline);
        void BindUniformBlock(int slot);
        void BindUniformBlocks();
        void BindVertexBuffer(GpuHandle buffer, int slot, int stride, int offset);
        void BindIndexBuffer(GpuHandle buffer);
        void BindMesh(GpuHandle vertex, GpuHandle index, int stride);
        void BindMesh(GpuHandle vertex, GpuHandle index, VertexLayout layout);
        void BindBuffer(GpuHandle buffer);
        GpuHandle GetBoundRenderTarget();
        void BindTextureSlot(int slot, GpuHandle texture);
        void BindRenderTarget(GpuHandle target);
        void BindDefaultRenderTarget();
        void BindStorageBuffer(GpuHandle buffer, int slot);
        void BindUniformBuffer(GpuHandle buffer, int slot);
        void* Map(GpuHandle buffer, MapAccess access);
        void* Map(GpuHandle buffer, int offset, uint length, MapAccess access);
        void Unmap(GpuHandle buffer);
        void UpdateBuffer(GpuHandle buffer, ReadOnlySpan<byte> data, int offset = 0);
        void UpdateTexture(GpuHandle texture, int width, int height, int format, int type, void* pixels);
        void UpdateCubeFace(GpuHandle texture, int face, int width, int height, int format, int type, void* pixels);
        void UpdateCubemapFace(GpuHandle texture, int faceTarget, int width, int height, int format, int type, void* pixels);
        void SetTextureParam(GpuHandle texture, int pname, int param);
        void GenerateMipmaps(GpuHandle texture);
        void DrawFullscreen();
        void SetTextureParams(GpuHandle texture, int minFilter, int magFilter, int wrapS, int wrapT);
        void SetConstants<T>(int slot, in T data) where T : unmanaged;
        bool TryGetConstants<T>(int slot, out T data) where T : unmanaged;
        void BindCamera(in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 model);
        void DrawIndexed(int indexCount);
        void Draw(int vertexCount);
        void Dispatch(uint groupsX, uint groupsY = 1, uint groupsZ = 1);
    }
}
