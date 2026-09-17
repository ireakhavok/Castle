// Folder: SiegeEngine/Core/GPU/ContextManagement
// File: PipelineDesc.cs
using SiegeEngine.Core.GPU.Shaders;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    public struct GpuRenderState
    {
        public bool DepthTest;
        public bool DepthWrite;
        public bool Blend;
        public int CullMode;
        public int Primitive;
    }

    public struct ShaderSourceSet
    {
        public string Vertex;
        public string Fragment;
        public string Compute;
        public string VertexHlsl;
        public string FragmentHlsl;
        public string ComputeHlsl;
        public bool IsCompute => !string.IsNullOrEmpty(Compute) && string.IsNullOrEmpty(Vertex);
    }

    public struct PipelineDesc
    {
        public ShaderId ShaderId;
        public VertexLayout Layout;
        public GpuRenderState State;
        public string VertexSource;
        public string FragmentSource;
        public string ComputeSource;
    }
}
