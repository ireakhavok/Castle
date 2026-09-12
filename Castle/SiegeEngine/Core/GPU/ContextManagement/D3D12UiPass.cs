using System;
using System.Runtime.InteropServices;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    /// <summary>
    /// Colored NDC quad replay on an open D3D12 command list.
    /// </summary>
    internal static class D3D12UiPass
    {
        const int DXGI_FORMAT_R8G8B8A8_UNORM = 28;
        const int DXGI_FORMAT_R32G32_FLOAT = 16;
        const int DXGI_FORMAT_R32_UINT = 42;
        const int D3D12_HEAP_TYPE_UPLOAD = 2;
        const int D3D12_RESOURCE_DIMENSION_BUFFER = 1;
        const int D3D12_TEXTURE_LAYOUT_ROW_MAJOR = 1;
        const int D3D12_RESOURCE_STATE_GENERIC_READ = 1;
        const int D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST = 4;
        const int D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE = 3;
        const int D3D12_FILL_MODE_SOLID = 3;
        const int D3D12_CULL_MODE_NONE = 1;
        const int D3D12_BLEND_SRC_ALPHA = 5;
        const int D3D12_BLEND_INV_SRC_ALPHA = 6;
        const int D3D12_BLEND_OP_ADD = 1;
        const int D3D12_LOGIC_OP_NOOP = 1;
        const byte D3D12_COLOR_WRITE_ENABLE_ALL = 15;
        const int D3D12_ROOT_PARAMETER_TYPE_32BIT_CONSTANTS = 1;
        const int D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT = 1;
        const int D3D_ROOT_SIGNATURE_VERSION_1 = 1;
        const uint UploadBytes = 4 * 1024 * 1024;

        static readonly Guid IID_ID3D12Resource = new Guid("696442be-a72e-4059-bc79-5b5c98040fad");
        static readonly Guid IID_ID3D12RootSignature = new Guid("c54a6b66-72df-4ee8-8be5-a9466c944920");
        static readonly Guid IID_ID3D12PipelineState = new Guid("765a6a6b-dd1d-4064-bf6c-d3d6a9161e60");

        [DllImport("d3d12.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int D3D12SerializeRootSignature(nint desc, int version, out nint blob, out nint error);

        [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        static extern int D3DCompile(string src, nuint srcSize, string name, nint defines, nint include,
            string entry, string target, uint flags1, uint flags2, out nint code, out nint errors);

        static nint _root, _pso, _upload, _mapped;
        static ulong _uploadGpu;
        static bool _ready;
        static bool _tried;

        const string VsSrc = @"struct VSIn { float2 pos : POSITION; float2 uv : TEXCOORD; };
struct VSOut { float4 pos : SV_POSITION; float2 uv : TEXCOORD; };
VSOut VS(VSIn i) { VSOut o; o.pos = float4(i.pos.x, -i.pos.y, 0, 1); o.uv = i.uv; return o; }";
        const string PsSrc = @"cbuffer C : register(b0) { float4 color; };
float4 PS(float4 pos : SV_POSITION, float2 uv : TEXCOORD) : SV_TARGET { return color; }";

        public static void Draw(nint device, nint list, BackendRenderContext backend, int width, int height)
        {
            if (backend == null || backend.Draws.Count == 0) return;
            if (!_tried) Init(device);
            if (!_ready) return;
            SetPso(list, _pso);
            SetRoot(list, _root);
            Topology(list, D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            uint off = 0;
            foreach (var d in backend.Draws)
            {
                if (d.Verts == null || d.Verts.Length < 4) continue;
                int stride = d.Stride <= 0 ? 8 : d.Stride;
                int strideFloats = stride / 4;
                int vertCount = d.Verts.Length / Math.Max(2, strideFloats == 0 ? 2 : Math.Min(strideFloats, 4));
                if (stride == 8)
                    vertCount = d.Verts.Length / 2;
                else
                    vertCount = d.Verts.Length / (stride / 4);
                float[] packed = PackXyUv(d.Verts, stride);
                uint vbytes = (uint)(packed.Length * 4);
                int[] idx = d.Indices;
                if (idx == null || idx.Length == 0)
                    idx = new[] { 0, 1, 2, 0, 2, 3 };
                uint ibytes = (uint)(idx.Length * 4);
                off = (off + 255u) & ~255u;
                if (off + vbytes + ibytes + 256 > UploadBytes) break;
                Marshal.Copy(packed, 0, _mapped + (int)off, packed.Length);
                uint vOff = off;
                off += vbytes;
                off = (off + 255u) & ~255u;
                Marshal.Copy(idx, 0, _mapped + (int)off, idx.Length);
                uint iOff = off;
                off += ibytes;
                SetVb(list, _uploadGpu + vOff, vbytes, 16);
                SetIb(list, _uploadGpu + iOff, ibytes);
                SetColor(list, d.R, d.G, d.B, d.A);
                DrawIndexed(list, (uint)idx.Length);
            }
        }

        static float[] PackXyUv(float[] src, int stride)
        {
            int sf = Math.Max(2, stride / 4);
            int n = src.Length / sf;
            float[] dst = new float[n * 4];
            for (int i = 0; i < n; i++)
            {
                dst[i * 4] = src[i * sf];
                dst[i * 4 + 1] = src[i * sf + 1];
                dst[i * 4 + 2] = sf >= 4 ? src[i * sf + 2] : (i == 1 || i == 2 ? 1f : 0f);
                dst[i * 4 + 3] = sf >= 4 ? src[i * sf + 3] : (i >= 2 ? 1f : 0f);
            }
            return dst;
        }

        static void Init(nint device)
        {
            _tried = true;
            try
            {
                _root = CreateRoot(device);
                nint vs = Compile("VS", "vs_5_0", VsSrc);
                nint ps = Compile("PS", "ps_5_0", PsSrc);
                _pso = CreatePso(device, _root, vs, ps);
                ComVtable.Release(vs);
                ComVtable.Release(ps);
                var heap = new HeapProps { Type = D3D12_HEAP_TYPE_UPLOAD, CreationNodeMask = 1, VisibleNodeMask = 1 };
                var desc = new ResDesc
                {
                    Dimension = D3D12_RESOURCE_DIMENSION_BUFFER,
                    Width = UploadBytes,
                    Height = 1,
                    DepthOrArraySize = 1,
                    MipLevels = 1,
                    SampleDescCount = 1,
                    Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR
                };
                _upload = CreateCommitted(device, ref heap, ref desc);
                _uploadGpu = GetGpu(_upload);
                _mapped = Map(_upload);
                _ready = _root != nint.Zero && _pso != nint.Zero && _upload != nint.Zero;
                Console.WriteLine($"[DirectX12] UI pass ready={_ready}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DirectX12] UI pass failed: {ex.Message}");
                _ready = false;
            }
        }

        static nint CreateRoot(nint device)
        {
            var param = new RootParam
            {
                ParameterType = D3D12_ROOT_PARAMETER_TYPE_32BIT_CONSTANTS,
                Num32BitValues = 4
            };
            nint paramPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RootParam>());
            Marshal.StructureToPtr(param, paramPtr, false);
            var desc = new RootSigDesc
            {
                NumParameters = 1,
                pParameters = paramPtr,
                Flags = D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT
            };
            nint descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RootSigDesc>());
            Marshal.StructureToPtr(desc, descPtr, false);
            try
            {
                int hr = D3D12SerializeRootSignature(descPtr, D3D_ROOT_SIGNATURE_VERSION_1, out nint blob, out nint err);
                if (hr < 0) throw new InvalidOperationException("SerializeRootSignature 0x" + hr.ToString("X8") + " " + Ansi(err));
                nint rs = CreateRootSig(device, BlobPtr(blob), BlobLen(blob));
                ComVtable.Release(blob);
                ComVtable.Release(err);
                return rs;
            }
            finally
            {
                Marshal.FreeHGlobal(paramPtr);
                Marshal.FreeHGlobal(descPtr);
            }
        }

        static nint Compile(string entry, string target, string src)
        {
            int hr = D3DCompile(src, (nuint)src.Length, entry + ".hlsl", nint.Zero, nint.Zero, entry, target, 0, 0, out nint code, out nint err);
            if (hr < 0) throw new InvalidOperationException("D3DCompile " + entry + " " + Ansi(err));
            ComVtable.Release(err);
            return code;
        }

        static nint CreatePso(nint device, nint root, nint vs, nint ps)
        {
            nint namePos = Marshal.StringToHGlobalAnsi("POSITION");
            nint nameUv = Marshal.StringToHGlobalAnsi("TEXCOORD");
            var e0 = new InputElem { SemanticName = namePos, Format = DXGI_FORMAT_R32G32_FLOAT, AlignedByteOffset = 0 };
            var e1 = new InputElem { SemanticName = nameUv, Format = DXGI_FORMAT_R32G32_FLOAT, AlignedByteOffset = 8 };
            nint elems = Marshal.AllocHGlobal(Marshal.SizeOf<InputElem>() * 2);
            Marshal.StructureToPtr(e0, elems, false);
            Marshal.StructureToPtr(e1, elems + Marshal.SizeOf<InputElem>(), false);
            var pso = default(PsoDesc);
            pso.pRootSignature = root;
            pso.VS.pShaderBytecode = BlobPtr(vs);
            pso.VS.BytecodeLength = BlobLen(vs);
            pso.PS.pShaderBytecode = BlobPtr(ps);
            pso.PS.BytecodeLength = BlobLen(ps);
            pso.Blend.Rt0.BlendEnable = 1;
            pso.Blend.Rt0.SrcBlend = D3D12_BLEND_SRC_ALPHA;
            pso.Blend.Rt0.DestBlend = D3D12_BLEND_INV_SRC_ALPHA;
            pso.Blend.Rt0.BlendOp = D3D12_BLEND_OP_ADD;
            pso.Blend.Rt0.SrcBlendAlpha = 1;
            pso.Blend.Rt0.DestBlendAlpha = D3D12_BLEND_INV_SRC_ALPHA;
            pso.Blend.Rt0.BlendOpAlpha = D3D12_BLEND_OP_ADD;
            pso.Blend.Rt0.LogicOp = D3D12_LOGIC_OP_NOOP;
            pso.Blend.Rt0.RenderTargetWriteMask = D3D12_COLOR_WRITE_ENABLE_ALL;
            pso.SampleMask = 0xffffffff;
            pso.Raster.FillMode = D3D12_FILL_MODE_SOLID;
            pso.Raster.CullMode = D3D12_CULL_MODE_NONE;
            pso.Raster.DepthClipEnable = 1;
            pso.InputLayout.pInputElementDescs = elems;
            pso.InputLayout.NumElements = 2;
            pso.PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
            pso.NumRenderTargets = 1;
            pso.RTVFormat0 = DXGI_FORMAT_R8G8B8A8_UNORM;
            pso.SampleCount = 1;
            nint desc = Marshal.AllocHGlobal(Marshal.SizeOf<PsoDesc>());
            Marshal.StructureToPtr(pso, desc, false);
            try { return CreateGraphicsPso(device, desc); }
            finally
            {
                Marshal.FreeHGlobal(desc);
                Marshal.FreeHGlobal(elems);
                Marshal.FreeHGlobal(namePos);
                Marshal.FreeHGlobal(nameUv);
            }
        }

        static nint CreateRootSig(nint device, nint blob, ulong len)
        {
            nint iid = GuidPtr(IID_ID3D12RootSignature);
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateRsFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(device, 16), typeof(CreateRsFn));
                ComVtable.ThrowIfFailed(fn(device, 0, blob, len, iid, box), "CreateRootSignature");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(iid); Marshal.FreeHGlobal(box); }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreateRsFn(nint self, uint node, nint blob, ulong len, nint iid, nint pp);

        static nint CreateGraphicsPso(nint device, nint desc)
        {
            nint iid = GuidPtr(IID_ID3D12PipelineState);
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreatePsoFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(device, 10), typeof(CreatePsoFn));
                ComVtable.ThrowIfFailed(fn(device, desc, iid, box), "CreateGraphicsPipelineState");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(iid); Marshal.FreeHGlobal(box); }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreatePsoFn(nint self, nint desc, nint iid, nint pp);

        static nint CreateCommitted(nint device, ref HeapProps heap, ref ResDesc desc)
        {
            nint hp = Marshal.AllocHGlobal(Marshal.SizeOf<HeapProps>());
            Marshal.StructureToPtr(heap, hp, false);
            nint dp = Marshal.AllocHGlobal(Marshal.SizeOf<ResDesc>());
            Marshal.StructureToPtr(desc, dp, false);
            nint iid = GuidPtr(IID_ID3D12Resource);
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateCommittedFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(device, 27), typeof(CreateCommittedFn));
                ComVtable.ThrowIfFailed(fn(device, hp, 0, dp, D3D12_RESOURCE_STATE_GENERIC_READ, nint.Zero, iid, box), "CreateCommittedResource");
                return Marshal.ReadIntPtr(box);
            }
            finally
            {
                Marshal.FreeHGlobal(hp); Marshal.FreeHGlobal(dp); Marshal.FreeHGlobal(iid); Marshal.FreeHGlobal(box);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreateCommittedFn(nint self, nint heap, int flags, nint desc, int state, nint clear, nint iid, nint pp);

        static ulong GetGpu(nint res)
        {
            var fn = (GetGpuFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(res, 11), typeof(GetGpuFn));
            return fn(res);
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate ulong GetGpuFn(nint self);

        static nint Map(nint res)
        {
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (MapFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(res, 8), typeof(MapFn));
                ComVtable.ThrowIfFailed(fn(res, 0, nint.Zero, box), "Map");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(box); }
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int MapFn(nint self, uint sub, nint range, nint pp);

        static void SetPso(nint list, nint pso)
        {
            var fn = (SetPtrFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(list, 25), typeof(SetPtrFn));
            fn(list, pso);
        }
        static void SetRoot(nint list, nint rs)
        {
            var fn = (SetPtrFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(list, 30), typeof(SetPtrFn));
            fn(list, rs);
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void SetPtrFn(nint self, nint p);

        static void Topology(nint list, int t)
        {
            var fn = (TopoFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(list, 20), typeof(TopoFn));
            fn(list, t);
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void TopoFn(nint self, int t);

        static void SetVb(nint list, ulong gpu, uint size, uint stride)
        {
            var v = new VbView { BufferLocation = gpu, SizeInBytes = size, StrideInBytes = stride };
            nint p = Marshal.AllocHGlobal(Marshal.SizeOf<VbView>());
            Marshal.StructureToPtr(v, p, false);
            var fn = (IaVbFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(list, 44), typeof(IaVbFn));
            fn(list, 0, 1, p);
            Marshal.FreeHGlobal(p);
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void IaVbFn(nint self, uint start, uint n, nint views);

        static void SetIb(nint list, ulong gpu, uint size)
        {
            var v = new IbView { BufferLocation = gpu, SizeInBytes = size, Format = DXGI_FORMAT_R32_UINT };
            nint p = Marshal.AllocHGlobal(Marshal.SizeOf<IbView>());
            Marshal.StructureToPtr(v, p, false);
            var fn = (IaIbFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(list, 43), typeof(IaIbFn));
            fn(list, p);
            Marshal.FreeHGlobal(p);
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void IaIbFn(nint self, nint view);

        static void SetColor(nint list, float r, float g, float b, float a)
        {
            float[] c = { r, g, b, a };
            nint p = Marshal.AllocHGlobal(16);
            Marshal.Copy(c, 0, p, 4);
            var fn = (RootConstFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(list, 36), typeof(RootConstFn));
            fn(list, 0, 4, p, 0);
            Marshal.FreeHGlobal(p);
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void RootConstFn(nint self, uint index, uint n, nint src, uint dest);

        static void DrawIndexed(nint list, uint count)
        {
            var fn = (DrawIdxFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(list, 13), typeof(DrawIdxFn));
            fn(list, count, 1, 0, 0, 0);
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void DrawIdxFn(nint self, uint idx, uint inst, uint startIdx, int baseV, uint startI);

        static nint BlobPtr(nint blob)
        {
            var fn = (BlobPtrFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(blob, 3), typeof(BlobPtrFn));
            return fn(blob);
        }
        static ulong BlobLen(nint blob)
        {
            var fn = (BlobLenFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(blob, 4), typeof(BlobLenFn));
            return fn(blob);
        }
        static string Ansi(nint blob)
        {
            if (blob == nint.Zero) return "";
            try { nint p = BlobPtr(blob); return p == nint.Zero ? "" : Marshal.PtrToStringAnsi(p) ?? ""; }
            catch { return ""; }
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate nint BlobPtrFn(nint self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate ulong BlobLenFn(nint self);

        static nint GuidPtr(Guid g)
        {
            nint p = Marshal.AllocHGlobal(16);
            Marshal.Copy(g.ToByteArray(), 0, p, 16);
            return p;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RootParam
        {
            public int ParameterType;
            public int Pad0;
            public uint ShaderRegister;
            public uint RegisterSpace;
            public uint Num32BitValues;
            public uint PadUnion;
            public int ShaderVisibility;
            public int Pad1;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct RootSigDesc
        {
            public uint NumParameters;
            public uint Pad0;
            public nint pParameters;
            public uint NumStaticSamplers;
            public uint Pad1;
            public nint pStaticSamplers;
            public int Flags;
            public int Pad2;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct ShaderBc { public nint pShaderBytecode; public ulong BytecodeLength; }
        [StructLayout(LayoutKind.Sequential)]
        struct StreamOut
        {
            public nint pSODeclaration;
            public uint NumEntries;
            public uint Pad0;
            public nint pBufferStrides;
            public uint NumStrides;
            public uint RasterizedStream;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct RtBlend
        {
            public int BlendEnable, LogicOpEnable, SrcBlend, DestBlend, BlendOp, SrcBlendAlpha, DestBlendAlpha, BlendOpAlpha, LogicOp;
            public byte RenderTargetWriteMask, Pad0, Pad1, Pad2;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct BlendDesc
        {
            public int AlphaToCoverageEnable, IndependentBlendEnable;
            public RtBlend Rt0, Rt1, Rt2, Rt3, Rt4, Rt5, Rt6, Rt7;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct Rasterizer
        {
            public int FillMode, CullMode, FrontCounterClockwise, DepthBias;
            public float DepthBiasClamp, SlopeScaledDepthBias;
            public int DepthClipEnable, MultisampleEnable, AntialiasedLineEnable;
            public uint ForcedSampleCount;
            public int ConservativeRaster;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct DepthOp { public int Fail, DepthFail, Pass, Func; }
        [StructLayout(LayoutKind.Sequential)]
        struct DepthStencil
        {
            public int DepthEnable, DepthWriteMask, DepthFunc, StencilEnable;
            public byte StencilReadMask, StencilWriteMask, Pad0, Pad1;
            public DepthOp Front, Back;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct InputLayout { public nint pInputElementDescs; public uint NumElements; public uint Pad; }
        [StructLayout(LayoutKind.Sequential)]
        struct PsoDesc
        {
            public nint pRootSignature;
            public ShaderBc VS, PS, DS, HS, GS;
            public StreamOut StreamOutput;
            public BlendDesc Blend;
            public uint SampleMask;
            public Rasterizer Raster;
            public DepthStencil DepthStencil;
            public InputLayout InputLayout;
            public int IBStripCutValue, PrimitiveTopologyType;
            public uint NumRenderTargets;
            public int RTVFormat0, RTVFormat1, RTVFormat2, RTVFormat3, RTVFormat4, RTVFormat5, RTVFormat6, RTVFormat7;
            public int DSVFormat;
            public uint SampleCount, SampleQuality;
            public uint NodeMask;
            public nint CachedBlob;
            public ulong CachedBlobSize;
            public int Flags;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct InputElem
        {
            public nint SemanticName;
            public uint SemanticIndex;
            public int Format;
            public uint InputSlot;
            public uint AlignedByteOffset;
            public int InputSlotClass;
            public uint InstanceDataStepRate;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct HeapProps
        {
            public int Type, CPUPageProperty, MemoryPoolPreference;
            public uint CreationNodeMask, VisibleNodeMask;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct ResDesc
        {
            public int Dimension;
            public uint Pad;
            public ulong Alignment, Width;
            public uint Height;
            public ushort DepthOrArraySize, MipLevels;
            public int Format;
            public uint SampleDescCount, SampleDescQuality;
            public int Layout;
            public uint Flags;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct VbView { public ulong BufferLocation; public uint SizeInBytes, StrideInBytes; }
        [StructLayout(LayoutKind.Sequential)]
        struct IbView { public ulong BufferLocation; public uint SizeInBytes; public int Format; }
    }
}
