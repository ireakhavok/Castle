using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    /// <summary>
    /// D3D11 immediate-mode helper used by both DirectX11 and DirectX12 (via 11On12).
    /// Draws NDC textured/solid triangles from the GL-like IRenderContext recording.
    /// Uses d3d11.dll + d3dcompiler_47.dll only (Microsoft).
    /// </summary>
    internal sealed class D3D11UiBatch
    {
        const int DXGI_FORMAT_R8G8B8A8_UNORM = 28;
        const int DXGI_FORMAT_R32G32_FLOAT = 16;
        const int DXGI_FORMAT_R32_UINT = 42;
        const int D3D11_BIND_VERTEX_BUFFER = 0x1;
        const int D3D11_BIND_INDEX_BUFFER = 0x2;
        const int D3D11_BIND_CONSTANT_BUFFER = 0x4;
        const int D3D11_BIND_SHADER_RESOURCE = 0x8;
        const int D3D11_USAGE_DEFAULT = 0;
        const int D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST = 4;
        const int D3D11_FILTER_MIN_MAG_MIP_LINEAR = 0x15;
        const int D3D11_TEXTURE_ADDRESS_WRAP = 1;
        const int D3D11_COMPARISON_NEVER = 1;
        const int D3D11_BLEND_SRC_ALPHA = 5;
        const int D3D11_BLEND_INV_SRC_ALPHA = 6;
        const int D3D11_BLEND_OP_ADD = 1;
        const byte D3D11_COLOR_WRITE_ENABLE_ALL = 0xF;

        static readonly Guid IID_ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        nint _device, _ctx;
        nint _vs, _ps, _layout, _sampler, _blend, _cbuf;
        nint _whiteSrv;
        bool _ready;

        readonly Dictionary<uint, GpuTex> _textures = new Dictionary<uint, GpuTex>();
        readonly Dictionary<uint, byte[]> _cpuBuffers = new Dictionary<uint, byte[]>();

        struct GpuTex
        {
            public nint Tex, Srv;
            public int W, H;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BufferDesc
        {
            public uint ByteWidth, Usage, BindFlags, CpuAccess, Misc, Stride;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SubData
        {
            public nint pSysMem;
            public uint Pitch, Slice;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Tex2DDesc
        {
            public uint Width, Height, MipLevels, ArraySize;
            public int Format;
            public uint SampleCount, SampleQuality;
            public uint Usage, BindFlags, CpuAccess, Misc;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct InputElem
        {
            public nint SemanticName;
            public uint SemanticIndex;
            public int Format;
            public uint InputSlot, AlignedByteOffset, InputSlotClass, InstanceDataStepRate;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SamplerDesc
        {
            public int Filter, AddressU, AddressV, AddressW;
            public float MipLODBias;
            public uint MaxAnisotropy;
            public int ComparisonFunc;
            public float Border0, Border1, Border2, Border3;
            public float MinLOD, MaxLOD;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BlendRt
        {
            public int BlendEnable, LogicOpEnable;
            public int SrcBlend, DestBlend, BlendOp, SrcBlendAlpha, DestBlendAlpha, BlendOpAlpha, LogicOp;
            public byte RenderTargetWriteMask;
            public byte pad0, pad1, pad2;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BlendDesc
        {
            public int AlphaToCoverage, IndependentBlend;
            public BlendRt RT0, RT1, RT2, RT3, RT4, RT5, RT6, RT7;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Viewport
        {
            public float X, Y, W, H, MinZ, MaxZ;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CBData
        {
            public float R, G, B, A, UseTex, VpW, VpH, Pad;
        }
        int _vpW = 1, _vpH = 1;

        public void Attach(nint device, nint context)
        {
            _device = device;
            _ctx = context;
            if (_device == nint.Zero || _ctx == nint.Zero) return;
            try
            {
                EnsurePipeline();
                _ready = true;
                Console.WriteLine("[D3D11Ui] Pipeline ready");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[D3D11Ui] Pipeline failed: {ex.Message}");
                _ready = false;
            }
        }

        public bool Ready => _ready && _device != nint.Zero;

        public void SetCpuBuffer(uint id, byte[] data)
        {
            if (id == 0) return;
            _cpuBuffers[id] = data ?? Array.Empty<byte>();
        }

        public void SetTextureRgba(uint id, int width, int height, byte[] rgba)
        {
            if (!_ready || id == 0 || width <= 0 || height <= 0 || rgba == null) return;
            DestroyTex(id);
            var desc = new Tex2DDesc
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = DXGI_FORMAT_R8G8B8A8_UNORM,
                SampleCount = 1,
                Usage = D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_SHADER_RESOURCE
            };
            nint pixels = Marshal.AllocHGlobal(rgba.Length);
            Marshal.Copy(rgba, 0, pixels, rgba.Length);
            var init = new SubData { pSysMem = pixels, Pitch = (uint)(width * 4) };
            nint descPtr = StructPtr(desc);
            nint initPtr = StructPtr(init);
            nint outPtr = OutBox();
            try
            {
                var fn = Get<CreateTex2DFn>(_device, 5);
                int hr = fn(_device, descPtr, initPtr, outPtr);
                ComVtable.ThrowIfFailed(hr, "CreateTexture2D");
                nint tex = Marshal.ReadIntPtr(outPtr);
                nint srvBox = OutBox();
                var srvFn = Get<CreateSrvFn>(_device, 7);
                hr = srvFn(_device, tex, nint.Zero, srvBox);
                ComVtable.ThrowIfFailed(hr, "CreateShaderResourceView");
                _textures[id] = new GpuTex { Tex = tex, Srv = Marshal.ReadIntPtr(srvBox), W = width, H = height };
                Marshal.FreeHGlobal(srvBox);
            }
            finally
            {
                Marshal.FreeHGlobal(pixels);
                Marshal.FreeHGlobal(descPtr);
                Marshal.FreeHGlobal(initPtr);
                Marshal.FreeHGlobal(outPtr);
            }
        }

        static byte[] PackXyUv(byte[] vb, int stride)
        {
            if (stride < 8) stride = 8;
            int verts = vb.Length / stride;
            var dst = new byte[verts * 16];
            int uvOff = (stride == 20 || stride == 24) ? 12 : 8;
            for (int i = 0; i < verts; i++)
            {
                int s = i * stride;
                Buffer.BlockCopy(vb, s, dst, i * 16, 8);
                if (stride >= uvOff + 8)
                    Buffer.BlockCopy(vb, s + uvOff, dst, i * 16 + 8, 8);
            }
            return dst;
        }

        public void DrawIndexed(uint vbo, uint ebo, uint texture, uint indexCount, float r, float g, float b, float a, bool useTexture)
        {
            if (!_ready || indexCount == 0) return;
            if (!_cpuBuffers.TryGetValue(vbo, out byte[] vb) || vb == null || vb.Length < 8) return;
            if (!_cpuBuffers.TryGetValue(ebo, out byte[] ib) || ib == null || ib.Length < 6) return;

            int idxStride = (indexCount > 0 && ib.Length == (int)indexCount * 2) ? 2 : 4;
            int nIdx = idxStride == 2 ? ib.Length / 2 : ib.Length / 4;
            int maxI = 0;
            for (int i = 0; i < nIdx; i++)
            {
                int v = idxStride == 2 ? BitConverter.ToUInt16(ib, i * 2) : BitConverter.ToInt32(ib, i * 4);
                if (v > maxI) maxI = v;
            }
            int stride = vb.Length / Math.Max(1, maxI + 1);
            if (stride < 8 || stride > 64) stride = 8;
            byte[] packed = PackXyUv(vb, stride);
            byte[] idx32 = ib;
            if (idxStride == 2)
            {
                idx32 = new byte[nIdx * 4];
                for (int i = 0; i < nIdx; i++)
                    BitConverter.GetBytes((int)BitConverter.ToUInt16(ib, i * 2)).CopyTo(idx32, i * 4);
            }

            nint vbGpu = CreateTempBuffer(packed, D3D11_BIND_VERTEX_BUFFER);
            nint ibGpu = CreateTempBuffer(idx32, D3D11_BIND_INDEX_BUFFER);
            if (vbGpu == nint.Zero || ibGpu == nint.Zero)
            {
                ComVtable.Release(vbGpu);
                ComVtable.Release(ibGpu);
                return;
            }

            BindPipeline();
            SetCBuffer(r, g, b, a, useTexture && texture != 0);

            nint srv = _whiteSrv;
            if (useTexture && texture != 0 && _textures.TryGetValue(texture, out GpuTex gt) && gt.Srv != nint.Zero)
                srv = gt.Srv;
            SetSrv(srv);

            nint vbBox = OutBox();
            Marshal.WriteIntPtr(vbBox, vbGpu);
            uint iaStride = 16, offset = 0;
            var setVb = Get<IaSetVbFn>(_ctx, 18);
            setVb(_ctx, 0, 1, vbBox, ref iaStride, ref offset);
            Marshal.FreeHGlobal(vbBox);

            var setIb = Get<IaSetIbFn>(_ctx, 19);
            setIb(_ctx, ibGpu, DXGI_FORMAT_R32_UINT, 0);

            var draw = Get<DrawIndexedFn>(_ctx, 12);
            draw(_ctx, indexCount, 0, 0);

            ComVtable.Release(vbGpu);
            ComVtable.Release(ibGpu);
        }

        public void SetRenderTarget(nint rtv, int width, int height)
        {
            if (_ctx == nint.Zero) return;
            nint box = OutBox();
            Marshal.WriteIntPtr(box, rtv);
            var om = Get<OmSetFn>(_ctx, 33);
            om(_ctx, 1, box, nint.Zero);
            Marshal.FreeHGlobal(box);

            _vpW = Math.Max(width, 1); _vpH = Math.Max(height, 1);
            var vp = new Viewport { W = width, H = height, MaxZ = 1f };
            nint vpPtr = StructPtr(vp);
            var setVp = Get<RsSetVpFn>(_ctx, 44);
            setVp(_ctx, 1, vpPtr);
            Marshal.FreeHGlobal(vpPtr);
        }

        public void ClearRtv(nint rtv, float r, float g, float b, float a)
        {
            if (_ctx == nint.Zero || rtv == nint.Zero) return;
            float[] color = { r, g, b, a };
            nint c = Marshal.AllocHGlobal(16);
            Marshal.Copy(color, 0, c, 4);
            var fn = Get<ClearRtvFn>(_ctx, 50);
            fn(_ctx, rtv, c);
            Marshal.FreeHGlobal(c);
        }

        public void Flush()
        {
            if (_ctx == nint.Zero) return;
            var fn = Get<FlushFn>(_ctx, 111);
            try { fn(_ctx); } catch { }
        }

        public void Dispose()
        {
            foreach (var kv in _textures)
            {
                ComVtable.Release(kv.Value.Srv);
                ComVtable.Release(kv.Value.Tex);
            }
            _textures.Clear();
            ComVtable.Release(_whiteSrv); _whiteSrv = nint.Zero;
            ComVtable.Release(_cbuf); _cbuf = nint.Zero;
            ComVtable.Release(_blend); _blend = nint.Zero;
            ComVtable.Release(_sampler); _sampler = nint.Zero;
            ComVtable.Release(_layout); _layout = nint.Zero;
            ComVtable.Release(_ps); _ps = nint.Zero;
            ComVtable.Release(_vs); _vs = nint.Zero;
            _ready = false;
        }

        void DestroyTex(uint id)
        {
            if (_textures.TryGetValue(id, out GpuTex t))
            {
                ComVtable.Release(t.Srv);
                ComVtable.Release(t.Tex);
                _textures.Remove(id);
            }
        }

        void EnsurePipeline()
        {
            const string hlsl = @"
cbuffer CB : register(b0) { float4 uColor; float uUseTex; float2 Viewport; float pad; };
Texture2D tex : register(t0);
SamplerState samp : register(s0);
struct VSIn { float2 pos : POSITION; float2 uv : TEXCOORD; };
struct PSIn { float4 pos : SV_POSITION; float2 uv : TEXCOORD; };
PSIn VSMain(VSIn i) {
    PSIn o;
    float2 p = i.pos;
    float2 vp = float2(max(Viewport.x, 1), max(Viewport.y, 1));
    if (max(abs(p.x), abs(p.y)) > 1.5)
        p = float2(p.x / vp.x * 2 - 1, 1 - p.y / vp.y * 2);
    o.pos = float4(p, 0, 1);
    o.uv = i.uv;
    return o;
}
float4 PSMain(PSIn i) : SV_TARGET {
    float4 t = uUseTex > 0.5 ? tex.Sample(samp, i.uv) : float4(1,1,1,1);
    return t * uColor;
}";
            nint vsBlob = Compile(hlsl, "VSMain", "vs_5_0");
            nint psBlob = Compile(hlsl, "PSMain", "ps_5_0");
            nint vsPtr = BlobPtr(vsBlob);
            nint vsLen = BlobLen(vsBlob);
            nint psPtr = BlobPtr(psBlob);
            nint psLen = BlobLen(psBlob);

            nint outVs = OutBox();
            Get<CreateVsFn>(_device, 12)(_device, vsPtr, vsLen, nint.Zero, outVs);
            _vs = Marshal.ReadIntPtr(outVs);
            Marshal.FreeHGlobal(outVs);
            if (_vs == nint.Zero) throw new InvalidOperationException("CreateVertexShader");

            nint outPs = OutBox();
            Get<CreatePsFn>(_device, 15)(_device, psPtr, psLen, nint.Zero, outPs);
            _ps = Marshal.ReadIntPtr(outPs);
            Marshal.FreeHGlobal(outPs);
            if (_ps == nint.Zero) throw new InvalidOperationException("CreatePixelShader");

            nint namePos = Marshal.StringToHGlobalAnsi("POSITION");
            nint nameUv = Marshal.StringToHGlobalAnsi("TEXCOORD");
            var elems = new[]
            {
                new InputElem { SemanticName = namePos, Format = DXGI_FORMAT_R32G32_FLOAT, AlignedByteOffset = 0 },
                new InputElem { SemanticName = nameUv, Format = DXGI_FORMAT_R32G32_FLOAT, AlignedByteOffset = 8 }
            };
            nint elemPtr = Marshal.AllocHGlobal(Marshal.SizeOf<InputElem>() * 2);
            Marshal.StructureToPtr(elems[0], elemPtr, false);
            Marshal.StructureToPtr(elems[1], elemPtr + Marshal.SizeOf<InputElem>(), false);
            nint outLay = OutBox();
            Get<CreateLayoutFn>(_device, 11)(_device, elemPtr, 2, vsPtr, vsLen, outLay);
            _layout = Marshal.ReadIntPtr(outLay);
            Marshal.FreeHGlobal(outLay);
            Marshal.FreeHGlobal(elemPtr);
            Marshal.FreeHGlobal(namePos);
            Marshal.FreeHGlobal(nameUv);
            ComVtable.Release(vsBlob);
            ComVtable.Release(psBlob);
            if (_layout == nint.Zero) throw new InvalidOperationException("CreateInputLayout");

            var samp = new SamplerDesc
            {
                Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR,
                AddressU = D3D11_TEXTURE_ADDRESS_WRAP,
                AddressV = D3D11_TEXTURE_ADDRESS_WRAP,
                AddressW = D3D11_TEXTURE_ADDRESS_WRAP,
                ComparisonFunc = D3D11_COMPARISON_NEVER,
                MaxLOD = float.MaxValue,
                MaxAnisotropy = 1
            };
            nint sampPtr = StructPtr(samp);
            nint outSamp = OutBox();
            Get<CreateSampFn>(_device, 23)(_device, sampPtr, outSamp);
            _sampler = Marshal.ReadIntPtr(outSamp);
            Marshal.FreeHGlobal(sampPtr);
            Marshal.FreeHGlobal(outSamp);

            var blend = new BlendDesc();
            blend.RT0.BlendEnable = 1;
            blend.RT0.SrcBlend = D3D11_BLEND_SRC_ALPHA;
            blend.RT0.DestBlend = D3D11_BLEND_INV_SRC_ALPHA;
            blend.RT0.BlendOp = D3D11_BLEND_OP_ADD;
            blend.RT0.SrcBlendAlpha = D3D11_BLEND_SRC_ALPHA;
            blend.RT0.DestBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA;
            blend.RT0.BlendOpAlpha = D3D11_BLEND_OP_ADD;
            blend.RT0.RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
            nint blendPtr = StructPtr(blend);
            nint outBlend = OutBox();
            Get<CreateBlendFn>(_device, 20)(_device, blendPtr, outBlend);
            _blend = Marshal.ReadIntPtr(outBlend);
            Marshal.FreeHGlobal(blendPtr);
            Marshal.FreeHGlobal(outBlend);

            _cbuf = CreateTempBuffer(new byte[32], D3D11_BIND_CONSTANT_BUFFER);

            byte[] white = { 255, 255, 255, 255 };
            SetTextureRgba(0xFFFFFFFFu, 1, 1, white);
            if (_textures.TryGetValue(0xFFFFFFFFu, out GpuTex wt))
                _whiteSrv = wt.Srv;
        }

        void BindPipeline()
        {
            Get<VsSetFn>(_ctx, 11)(_ctx, _vs, nint.Zero, 0);
            Get<PsSetFn>(_ctx, 9)(_ctx, _ps, nint.Zero, 0);
            Get<IaSetLayoutFn>(_ctx, 17)(_ctx, _layout);
            Get<IaSetTopoFn>(_ctx, 24)(_ctx, D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            nint sampBox = OutBox();
            Marshal.WriteIntPtr(sampBox, _sampler);
            Get<PsSetSampFn>(_ctx, 10)(_ctx, 0, 1, sampBox);
            Marshal.FreeHGlobal(sampBox);
            float[] factor = { 1, 1, 1, 1 };
            nint fac = Marshal.AllocHGlobal(16);
            Marshal.Copy(factor, 0, fac, 4);
            Get<OmSetBlendFn>(_ctx, 35)(_ctx, _blend, fac, 0xFFFFFFFF);
            Marshal.FreeHGlobal(fac);
            nint cbBox = OutBox();
            Marshal.WriteIntPtr(cbBox, _cbuf);
            Get<SetCbFn>(_ctx, 7)(_ctx, 0, 1, cbBox);
            Get<SetCbFn>(_ctx, 16)(_ctx, 0, 1, cbBox);
            Marshal.FreeHGlobal(cbBox);
        }

        void SetCBuffer(float r, float g, float b, float a, bool useTex)
        {
            var cb = new CBData { R = r, G = g, B = b, A = a, UseTex = useTex ? 1f : 0f, VpW = _vpW, VpH = _vpH };
            nint ptr = StructPtr(cb);
            Get<UpdateSubFn>(_ctx, 48)(_ctx, _cbuf, 0, nint.Zero, ptr, 0, 0);
            Marshal.FreeHGlobal(ptr);
        }

        void SetSrv(nint srv)
        {
            nint box = OutBox();
            Marshal.WriteIntPtr(box, srv);
            Get<PsSetSrvFn>(_ctx, 8)(_ctx, 0, 1, box);
            Marshal.FreeHGlobal(box);
        }

        nint CreateTempBuffer(byte[] data, int bind)
        {
            if (data == null || data.Length == 0) return nint.Zero;
            int size = (data.Length + 15) & ~15;
            var desc = new BufferDesc { ByteWidth = (uint)size, Usage = D3D11_USAGE_DEFAULT, BindFlags = (uint)bind };
            nint mem = Marshal.AllocHGlobal(size);
            Marshal.Copy(data, 0, mem, data.Length);
            if (size > data.Length)
            {
                for (int i = data.Length; i < size; i++) Marshal.WriteByte(mem, i, 0);
            }
            var init = new SubData { pSysMem = mem };
            nint descPtr = StructPtr(desc);
            nint initPtr = StructPtr(init);
            nint outPtr = OutBox();
            int hr = Get<CreateBufFn>(_device, 3)(_device, descPtr, initPtr, outPtr);
            nint buf = hr >= 0 ? Marshal.ReadIntPtr(outPtr) : nint.Zero;
            Marshal.FreeHGlobal(mem);
            Marshal.FreeHGlobal(descPtr);
            Marshal.FreeHGlobal(initPtr);
            Marshal.FreeHGlobal(outPtr);
            return buf;
        }

        static nint Compile(string src, string entry, string target)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(src);
            int hr = D3D12Native.D3DCompile(src, (ulong)bytes.Length, "ui.hlsl", nint.Zero, nint.Zero, entry, target, 0, 0, out nint blob, out nint err);
            if (hr < 0)
            {
                string msg = err != nint.Zero ? BlobString(err) : $"hr=0x{hr:X8}";
                ComVtable.Release(err);
                throw new InvalidOperationException($"D3DCompile {entry}: {msg}");
            }
            ComVtable.Release(err);
            return blob;
        }

        static nint BlobPtr(nint blob)
        {
            var fn = Get<BlobPtrFn>(blob, 3);
            return fn(blob);
        }

        static nint BlobLen(nint blob)
        {
            var fn = Get<BlobLenFn>(blob, 4);
            return (nint)(long)fn(blob);
        }

        static string BlobString(nint blob)
        {
            nint p = BlobPtr(blob);
            nint n = BlobLen(blob);
            if (p == nint.Zero || n == 0) return "";
            return Marshal.PtrToStringAnsi(p, (int)n) ?? "";
        }

        static nint StructPtr<T>(T value) where T : struct
        {
            nint p = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
            Marshal.StructureToPtr(value, p, false);
            return p;
        }

        static nint OutBox()
        {
            nint p = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(p, nint.Zero);
            return p;
        }

        static T Get<T>(nint obj, int slot) where T : Delegate
            => Marshal.GetDelegateForFunctionPointer<T>(ComVtable.Slot(obj, slot));

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateBufFn(nint s, nint desc, nint init, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateTex2DFn(nint s, nint desc, nint init, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateSrvFn(nint s, nint res, nint desc, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateVsFn(nint s, nint code, nint len, nint link, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreatePsFn(nint s, nint code, nint len, nint link, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateLayoutFn(nint s, nint elems, uint n, nint code, nint len, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateSampFn(nint s, nint desc, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateBlendFn(nint s, nint desc, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void VsSetFn(nint s, nint sh, nint cls, uint n);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void PsSetFn(nint s, nint sh, nint cls, uint n);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void IaSetLayoutFn(nint s, nint layout);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void IaSetTopoFn(nint s, int topo);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void IaSetVbFn(nint s, uint start, uint n, nint bufs, ref uint stride, ref uint offset);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void IaSetIbFn(nint s, nint buf, int format, uint offset);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void DrawIndexedFn(nint s, uint count, uint start, int baseVert);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void OmSetFn(nint s, uint n, nint rtvs, nint dsv);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void OmSetBlendFn(nint s, nint blend, nint factor, uint mask);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void RsSetVpFn(nint s, uint n, nint vp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void ClearRtvFn(nint s, nint rtv, nint color);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void UpdateSubFn(nint s, nint dst, uint sub, nint box, nint src, uint pitch, uint slice);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetCbFn(nint s, uint start, uint n, nint bufs);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void PsSetSrvFn(nint s, uint start, uint n, nint srvs);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void PsSetSampFn(nint s, uint start, uint n, nint sss);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void FlushFn(nint s);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate nint BlobPtrFn(nint s);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate ulong BlobLenFn(nint s);
    }
}
