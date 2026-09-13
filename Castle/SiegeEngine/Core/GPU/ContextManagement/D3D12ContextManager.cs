using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    /// <summary>
    /// D3D12 device + DXGI swapchain. UI is drawn through D3D11On12
    /// (d3d11.dll / d3dcompiler_47.dll — Microsoft) so GLSL is not required.
    /// </summary>
    public sealed class D3D12ContextManager : ContextManager
    {
        const int DXGI_FORMAT_R8G8B8A8_UNORM = 28;
        const uint DXGI_USAGE_RENDER_TARGET_OUTPUT = 32;
        const int DXGI_SWAP_EFFECT_FLIP_DISCARD = 4;
        const int D3D12_COMMAND_LIST_TYPE_DIRECT = 0;
        const int D3D12_DESCRIPTOR_HEAP_TYPE_RTV = 2;
        const int D3D12_RESOURCE_STATE_PRESENT = 0;
        const int D3D12_RESOURCE_STATE_RENDER_TARGET = 4;
        const int D3D11_BIND_RENDER_TARGET = 0x20;
        const int D3D11_BIND_SHADER_RESOURCE = 0x08;
        const int D3D11_BIND_VERTEX_BUFFER = 0x01;
        const int D3D11_USAGE_DEFAULT = 0;
        const int D3D11_USAGE_DYNAMIC = 2;
        const int D3D11_CPU_ACCESS_WRITE = 0x10000;
        const int D3D11_MAP_WRITE_DISCARD = 4;
        const int D3D11_MAP_WRITE_NO_OVERWRITE = 5;
        const int VbBytes = 1024 * 1024;
        const int D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST = 4;
        const int D3D11_FILTER_MIN_MAG_LINEAR_MIP_POINT = 0x14;
        const int D3D11_TEXTURE_ADDRESS_CLAMP = 3;
        const int D3D11_FILL_SOLID = 3;
        const int D3D11_CULL_NONE = 1;
        const int D3D11_SRV_DIMENSION_TEXTURE2D = 4;
        const uint D3D11_SDK_VERSION = 7;
        const int FrameCount = 2;

        static readonly Guid IID_ID3D12Resource = D3D12Native.IID_ID3D12Resource;
        static readonly Guid IID_IDXGIFactory4 = D3D12Native.IID_IDXGIFactory4;
        static readonly Guid IID_ID3D12CommandQueue = D3D12Native.IID_ID3D12CommandQueue;
        static readonly Guid IID_ID3D12Fence = D3D12Native.IID_ID3D12Fence;
        static readonly Guid IID_ID3D11On12Device = new Guid("85611e73-70a9-490e-9614-a9e302777904");
        static readonly Guid IID_ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        static readonly Guid IID_ID3D11Resource = new Guid("dc8e6308-e0e6-4ff3-9173-3ee2469dc052");

        [DllImport("dxgi.dll", EntryPoint = "CreateDXGIFactory1", CallingConvention = CallingConvention.StdCall)]
        static extern int CreateDXGIFactory1(ref Guid riid, out nint factory);

        [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int D3D11On12CreateDevice(
            nint pDevice, uint flags, int[] featureLevels, uint featureLevelsCount,
            nint[] ppCommandQueues, uint numQueues, uint nodeMask,
            out nint ppDevice, out nint ppImmediateContext, out int pChosenFeatureLevel);

        [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int D3DCompile(
            byte[] pSrcData, nint srcDataSize, string pSourceName, nint pDefines,
            nint pInclude, string pEntryPoint, string pTarget,
            uint flags1, uint flags2, out nint ppCode, out nint ppErrorMsgs);

        GlfwNoApiHost _host;
        D3D12RenderContext _backend;
        nint _device12, _queue, _factory, _swap, _fence;
        nint _device11, _ctx11, _on12;
        nint[] _bb12 = new nint[FrameCount];
        nint[] _wrapped11 = new nint[FrameCount];
        nint[] _rtv11 = new nint[FrameCount];
        nint _vs, _ps, _layout, _vb, _cb, _sampler, _rs, _blend, _whiteSrv, _whiteTex;
        ulong _fenceValue;
        nint _fenceEvent;
        int _frame;
        int _width, _height;
        int _vbOffset;
        bool _uiReady;
        bool _loggedLayout;
        bool _loggedMissingTex;
        readonly Dictionary<uint, nint> _gpuTex = new Dictionary<uint, nint>();
        readonly Dictionary<uint, nint> _gpuSrv = new Dictionary<uint, nint>();
        readonly Dictionary<uint, int> _gpuTexGen = new Dictionary<uint, int>();

        public override string BackendName => "DirectX12";
        public override bool DrawsUi => true;

        public override void Initialize(int width, int height, string title)
        {
            _width = Math.Max(width, 1);
            _height = Math.Max(height, 1);
            _host = new GlfwNoApiHost(_width, _height, title);
            _window = _host.WindowPtr;

            Guid devIid = D3D12Native.IID_ID3D12Device;
            int hr = D3D12Native.D3D12CreateDevice(nint.Zero, 0xB000, ref devIid, out _device12);
            ComVtable.ThrowIfFailed(hr, "D3D12CreateDevice");

            var qdesc = new D3D12Native.D3D12_COMMAND_QUEUE_DESC { Type = D3D12_COMMAND_LIST_TYPE_DIRECT };
            _queue = CreateCom(_device12, 8, ref qdesc, IID_ID3D12CommandQueue, "CreateCommandQueue");

            Guid factoryIid = IID_IDXGIFactory4;
            hr = CreateDXGIFactory1(ref factoryIid, out _factory);
            ComVtable.ThrowIfFailed(hr, "CreateDXGIFactory1");

            var scDesc = new D3D12Native.DXGI_SWAP_CHAIN_DESC
            {
                BufferDesc = new D3D12Native.DXGI_MODE_DESC
                {
                    Width = _width,
                    Height = _height,
                    RefreshRate = new D3D12Native.DXGI_RATIONAL { Numerator = 0, Denominator = 1 },
                    Format = DXGI_FORMAT_R8G8B8A8_UNORM
                },
                SampleDesc = new D3D12Native.DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
                BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
                BufferCount = (uint)FrameCount,
                OutputWindow = _host.Hwnd,
                Windowed = 1,
                SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD
            };
            _swap = CreateSwapChain(_factory, _queue, ref scDesc);
            for (uint i = 0; i < FrameCount; i++)
                _bb12[i] = GetSwapBuffer(_swap, i);

            _fence = CreateFence(_device12);
            _fenceEvent = CreateEventW(nint.Zero, false, false, null);

            int[] levels = { 0xB100, 0xB000 };
            nint[] queues = { _queue };
            hr = D3D11On12CreateDevice(_device12, 0, levels, (uint)levels.Length, queues, 1, 0,
                out _device11, out _ctx11, out int fl);
            ComVtable.ThrowIfFailed(hr, "D3D11On12CreateDevice");
            _on12 = QueryOn12(_device11);
            if (_on12 == nint.Zero)
                throw new InvalidOperationException("QueryInterface ID3D11On12Device failed");

            for (int i = 0; i < FrameCount; i++)
            {
                _wrapped11[i] = WrapResource(_bb12[i]);
                _rtv11[i] = CreateRtv11(_wrapped11[i]);
            }

            BuildUiPipeline();
            Console.WriteLine($"[DirectX12] Device+swapchain+11on12 hwnd=0x{_host.Hwnd:X} FL=0x{fl:X} ui={_uiReady}");

            _backend = new D3D12RenderContext(_width, _height);
            _backend.ClearR = 0.08f; _backend.ClearG = 0.08f; _backend.ClearB = 0.10f; _backend.ClearA = 1f;
            _renderContext = _backend;
            var control = new GlfwControlContext(_host.Glfw);
            control.SetPresentOverride(Present);
            _controlContext = control;
        }

        public override void Present()
        {
            if (_swap == nint.Zero || _ctx11 == nint.Zero) return;
            SyncSwapchainSize();
            if (_swap == nint.Zero || _ctx11 == nint.Zero) return;
            int idx = _frame % FrameCount;
            if (_wrapped11[idx] == nint.Zero || _rtv11[idx] == nint.Zero)
                return;

            Acquire(_wrapped11[idx]);

            nint rtvBox = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(rtvBox, _rtv11[idx]);
            var om = (OmSetFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 33), typeof(OmSetFn));
            om(_ctx11, 1, rtvBox, nint.Zero);
            Marshal.FreeHGlobal(rtvBox);

            float[] color = { _backend.ClearR, _backend.ClearG, _backend.ClearB, _backend.ClearA };
            nint colorPtr = Marshal.AllocHGlobal(16);
            Marshal.Copy(color, 0, colorPtr, 4);
            var clear = (ClearRtvFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 50), typeof(ClearRtvFn));
            clear(_ctx11, _rtv11[idx], colorPtr);
            Marshal.FreeHGlobal(colorPtr);

            if (_uiReady)
                FlushUi();

            ReleaseWrap(_wrapped11[idx]);
            Flush11();

            ComVtable.Call2i(_swap, 8, 1, 0);
            WaitGpu();
            _frame++;
        }

        void FlushUi()
        {
            var draws = _backend.TakeDraws();
            if (draws.Length == 0) return;

            SetRasterizer();
            SetBlend();
            SetTopology();
            SetShaders();
            BindSampler();
            _vbOffset = VbBytes; // force a DISCARD on the first upload this present

            if (!_loggedLayout && draws.Length > 0)
            {
                _loggedLayout = true;
                var s = draws[0];
                float minx = float.MaxValue, maxx = float.MinValue, miny = float.MaxValue, maxy = float.MinValue;
                if (s.Verts != null)
                {
                    for (int i = 0; i + 1 < s.Verts.Length; i += 4)
                    {
                        minx = Math.Min(minx, s.Verts[i]); maxx = Math.Max(maxx, s.Verts[i]);
                        miny = Math.Min(miny, s.Verts[i + 1]); maxy = Math.Max(maxy, s.Verts[i + 1]);
                    }
                }
                Console.WriteLine($"[DirectX12] UI layout draws={draws.Length} floats={s.VertFloats} idx={s.IndexCount} pos=[{minx:0.##},{miny:0.##}]-[{maxx:0.##},{maxy:0.##}] vp={_width}x{_height}");
            }
            foreach (var d in draws)
            {
                if (d.Verts == null || d.VertFloats < 8) continue;
                if (FullyOffNdc(d.Verts, d.VertFloats)) continue;
                int vpW = d.VpW > 0 ? d.VpW : _width;
                int vpH = d.VpH > 0 ? d.VpH : _height;
                if (vpW <= 0 || vpH <= 0) continue;
                SetViewport(d.VpX, d.VpY, vpW, vpH);
                bool clipOk = (d.ScissorOn && d.ScW > 0 && d.ScH > 0)
                    ? SetScissor(d.ScX, d.ScY, d.ScW, d.ScH)
                    : SetScissor(d.VpX, d.VpY, vpW, vpH);
                if (!clipOk) continue;
                if (d.UseTexture > 0.5f)
                {
                    nint srv = ResolveSrv(d.Texture, d.UseTexture);
                    if (srv == nint.Zero || srv == _whiteSrv)
                    {
                        if (!_loggedMissingTex)
                        {
                            _loggedMissingTex = true;
                            Console.WriteLine($"[DirectX12] textured draw skipped (no CPU tex id={d.Texture})");
                        }
                        continue;
                    }
                    BindSrv(srv);
                }
                else
                {
                    BindSrv(_whiteSrv);
                }
                UploadConstants(d.R, d.G, d.B, d.A, d.UseTexture);
                DrawVerts(d.Verts, d.VertFloats, d.IndexCount);
            }
        }

        void BuildUiPipeline()
        {
            const string hlsl = @"
cbuffer CB : register(b0) { float4 Color; float UseTexture; float2 Viewport; float Pad; };
Texture2D Tex : register(t0);
SamplerState Samp : register(s0);
struct VSIn { float2 pos : POSITION; float2 uv : TEXCOORD; };
struct VSOut { float4 pos : SV_POSITION; float2 uv : TEXCOORD; };
VSOut vs(VSIn i) {
    VSOut o;
    o.pos = float4(i.pos, 0, 1);
    o.uv = i.uv;
    return o;
}
float4 ps(VSOut i) : SV_TARGET {
    float4 t = UseTexture > 0.5 ? Tex.Sample(Samp, i.uv) : float4(1,1,1,1);
    return t * Color;
}";
            byte[] src = System.Text.Encoding.ASCII.GetBytes(hlsl);
            if (!Compile(src, "vs", "vs_5_0", out nint vsBlob) ||
                !Compile(src, "ps", "ps_5_0", out nint psBlob))
            {
                Console.WriteLine("[DirectX12] HLSL compile failed — clear-only this session");
                return;
            }
            nint vsPtr = BlobPtr(vsBlob); ulong vsLen = BlobLen(vsBlob);
            nint psPtr = BlobPtr(psBlob); ulong psLen = BlobLen(psBlob);
            _vs = CreateVertexShader(vsPtr, vsLen);
            _ps = CreatePixelShader(psPtr, psLen);

            var elems = new D3D11_INPUT_ELEMENT_DESC[2];
            elems[0] = new D3D11_INPUT_ELEMENT_DESC { SemanticName = "POSITION", Format = 16 /* R32G32_FLOAT */, AlignedByteOffset = 0 };
            elems[1] = new D3D11_INPUT_ELEMENT_DESC { SemanticName = "TEXCOORD", Format = 16, AlignedByteOffset = 8 };
            _layout = CreateInputLayout(elems, vsPtr, vsLen);
            ReleaseBlob(vsBlob); ReleaseBlob(psBlob);

            _vb = CreateBuffer11(1024 * 1024, D3D11_BIND_VERTEX_BUFFER, D3D11_USAGE_DYNAMIC, D3D11_CPU_ACCESS_WRITE);
            _cb = CreateBuffer11(32, 4 /* CONSTANT_BUFFER */, D3D11_USAGE_DYNAMIC, D3D11_CPU_ACCESS_WRITE);
            _sampler = CreateSampler();
            _rs = CreateRasterizer();
            _blend = CreateBlend();
            MakeWhiteTexture();
            _uiReady = _vs != nint.Zero && _ps != nint.Zero && _layout != nint.Zero && _vb != nint.Zero && _cb != nint.Zero;
        }

        bool Compile(byte[] src, string entry, string target, out nint blob)
        {
            blob = nint.Zero;
            int hr = D3DCompile(src, (nint)src.Length, "ui.hlsl", nint.Zero, nint.Zero, entry, target, 0, 0, out blob, out nint err);
            if (hr < 0)
            {
                string msg = "";
                if (err != nint.Zero)
                {
                    nint p = BlobPtr(err);
                    ulong n = BlobLen(err);
                    if (p != nint.Zero && n > 0) msg = Marshal.PtrToStringAnsi(p) ?? "";
                    ReleaseBlob(err);
                }
                Console.WriteLine($"[DirectX12] D3DCompile {entry} hr=0x{hr:X8} {msg}");
                return false;
            }
            if (err != nint.Zero) ReleaseBlob(err);
            return blob != nint.Zero;
        }

        nint ResolveSrv(uint texId, float useTex)
        {
            if (useTex <= 0.5f || texId == 0) return _whiteSrv;
            if (!_backend.TryGetTexture(texId, out var cpu) || cpu == null || cpu.Rgba == null || cpu.Width <= 0) return nint.Zero;
            if (_gpuTex.TryGetValue(texId, out nint existing) && existing != nint.Zero
                && _gpuTexGen.TryGetValue(texId, out int gen) && gen == cpu.Generation)
                return _gpuSrv[texId];
            nint tex = CreateTexture2D(cpu.Width, cpu.Height, cpu.Rgba);
            nint srv = CreateSrv(tex);
            _gpuTex[texId] = tex;
            _gpuSrv[texId] = srv;
            _gpuTexGen[texId] = cpu.Generation;
            return srv;
        }

        void MakeWhiteTexture()
        {
            byte[] px = { 255, 255, 255, 255 };
            _whiteTex = CreateTexture2D(1, 1, px);
            _whiteSrv = CreateSrv(_whiteTex);
        }


        static bool FullyOffNdc(float[] v, int floats)
        {
            if (v == null) return true;
            int n = Math.Min(floats, v.Length);
            bool any = false;
            for (int i = 0; i + 1 < n; i += 4)
            {
                float x = v[i], y = v[i + 1];
                if (x >= -1.05f && x <= 1.05f && y >= -1.05f && y <= 1.05f)
                    return false;
                any = true;
            }
            return any;
        }

        void SetViewport(int glX, int glY, int glW, int glH)
        {
            // GL Viewport origin is bottom-left. D3D11_VIEWPORT origin is top-left.
            int w = Math.Max(glW, 1);
            int h = Math.Max(glH, 1);
            float top = _height - (glY + h);
            var vp = new D3D11_VIEWPORT { TopLeftX = glX, TopLeftY = top, Width = w, Height = h, MinDepth = 0, MaxDepth = 1 };
            nint p = Marshal.AllocHGlobal(Marshal.SizeOf<D3D11_VIEWPORT>());
            Marshal.StructureToPtr(vp, p, false);
            var fn = (SetVpFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 44), typeof(SetVpFn));
            fn(_ctx11, 1, p);
            Marshal.FreeHGlobal(p);
        }

        bool SetScissor(int glX, int glY, int glW, int glH)
        {
            int w = Math.Max(glW, 1);
            int h = Math.Max(glH, 1);
            int top = _height - (glY + h);
            int left = glX;
            int right = glX + w;
            int bottom = top + h;
            if (left < 0) left = 0;
            if (top < 0) top = 0;
            if (right > _width) right = _width;
            if (bottom > _height) bottom = _height;
            if (right <= left || bottom <= top)
                return false;
            var rc = new D3D11_RECT { Left = left, Top = top, Right = right, Bottom = bottom };
            nint p = Marshal.AllocHGlobal(Marshal.SizeOf<D3D11_RECT>());
            Marshal.StructureToPtr(rc, p, false);
            var fn = (SetScissorFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 45), typeof(SetScissorFn));
            fn(_ctx11, 1, p);
            Marshal.FreeHGlobal(p);
            return true;
        }

        void SetTopology()
        {
            var fn = (SetTopoFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 24), typeof(SetTopoFn));
            fn(_ctx11, D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        }

        void SetShaders()
        {
            var vs = (SetVsFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 11), typeof(SetVsFn));
            vs(_ctx11, _vs, nint.Zero, 0);
            var ps = (SetPsFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 9), typeof(SetPsFn));
            ps(_ctx11, _ps, nint.Zero, 0);
            var il = (SetIlFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 17), typeof(SetIlFn));
            il(_ctx11, _layout);
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, _cb);
            var cb = (SetCbFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 7), typeof(SetCbFn));
            cb(_ctx11, 0, 1, box);
            var pcb = (SetCbFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 16), typeof(SetCbFn));
            pcb(_ctx11, 0, 1, box);
            Marshal.FreeHGlobal(box);
        }

        void BindSampler()
        {
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, _sampler);
            var fn = (SetSampFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 10), typeof(SetSampFn));
            fn(_ctx11, 0, 1, box);
            Marshal.FreeHGlobal(box);
        }

        void BindSrv(nint srv)
        {
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, srv);
            var fn = (SetSrvFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 8), typeof(SetSrvFn));
            fn(_ctx11, 0, 1, box);
            Marshal.FreeHGlobal(box);
        }

        bool DrawVerts(float[] src, int floats, uint indexCount)
        {
            int vertFloats = Math.Min(floats, src.Length);
            vertFloats -= vertFloats % 4;
            if (vertFloats < 12) return false;
            int verts = vertFloats / 4;
            float[] tri = src;
            int o = vertFloats;
            if (verts == 4)
            {
                tri = new float[24];
                o = 0;
                void Put(int vi)
                {
                    int s = vi * 4;
                    tri[o++] = src[s]; tri[o++] = src[s + 1]; tri[o++] = src[s + 2]; tri[o++] = src[s + 3];
                }
                Put(0); Put(1); Put(2);
                Put(0); Put(2); Put(3);
            }
            else if (verts % 3 != 0)
            {
                int keep = verts - (verts % 3);
                if (keep < 3) return false;
                o = keep * 4;
            }
            if (o < 12) return false;
            int maxVerts = VbBytes / 16;
            maxVerts -= maxVerts % 3;
            int totalVerts = o / 4;
            int start = 0;
            while (start < totalVerts)
            {
                int n = totalVerts - start;
                if (n > maxVerts) n = maxVerts;
                n -= n % 3;
                if (n < 3) break;
                int byteOff = UploadRing(tri, start * 4, n * 4);
                if (byteOff < 0) break;
                BindVb(byteOff);
                Draw(n);
                Flush11();
                start += n;
            }
            return start >= 3;
        }

        // 11-on-12 does not rename a 1MB dynamic VB on WRITE_DISCARD.
        // Discarding in place leaves every recorded Draw pointing at the last upload
        // (the below-fold rows of a tall list). Ring with NO_OVERWRITE so every
        // draw keeps its own bytes until Present/Flush11.
        int UploadRing(float[] data, int floatOffset, int floatCount)
        {
            int bytes = floatCount * 4;
            if (bytes <= 0 || bytes > VbBytes) return -1;
            int mapType;
            if (_vbOffset + bytes > VbBytes)
            {
                if (_vbOffset > 0 && _vbOffset < VbBytes)
                    Flush11();
                mapType = D3D11_MAP_WRITE_DISCARD;
                _vbOffset = 0;
            }
            else
            {
                mapType = D3D11_MAP_WRITE_NO_OVERWRITE;
            }
            nint mappedBox = Marshal.AllocHGlobal(nint.Size * 2);
            Marshal.WriteIntPtr(mappedBox, nint.Zero);
            var map = (MapFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 14), typeof(MapFn));
            int hr = map(_ctx11, _vb, 0, mapType, 0, mappedBox);
            int destOff = _vbOffset;
            if (hr >= 0)
            {
                nint dest = Marshal.ReadIntPtr(mappedBox);
                if (dest != nint.Zero)
                {
                    int n = Math.Min(floatCount, data.Length - floatOffset);
                    if (n > 0)
                        Marshal.Copy(data, floatOffset, dest + destOff, n);
                }
            }
            var unmap = (UnmapFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 15), typeof(UnmapFn));
            unmap(_ctx11, _vb, 0);
            Marshal.FreeHGlobal(mappedBox);
            if (hr < 0) return -1;
            _vbOffset += bytes;
            return destOff;
        }

        void BindVb(int byteOffset)
        {
            nint vbBox = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(vbBox, _vb);
            nint strideBox = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(strideBox, 16);
            nint offBox = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(offBox, byteOffset);
            var fn = (SetVbFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 18), typeof(SetVbFn));
            fn(_ctx11, 0, 1, vbBox, strideBox, offBox);
            Marshal.FreeHGlobal(vbBox); Marshal.FreeHGlobal(strideBox); Marshal.FreeHGlobal(offBox);
        }

        void UploadConstants(float r, float g, float b, float a, float useTex)
        {
            float[] cb = new float[8];
            cb[0] = r; cb[1] = g; cb[2] = b; cb[3] = a;
            cb[4] = useTex; cb[5] = _width; cb[6] = _height;
            MapWrite(_cb, cb, 32);
        }

        void Draw(int vertexCount)
        {
            var fn = (DrawFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 13), typeof(DrawFn));
            fn(_ctx11, (uint)vertexCount, 0);
        }

        void MapWrite(nint resource, float[] data, int byteCount)
        {
            nint mappedBox = Marshal.AllocHGlobal(nint.Size * 2);
            Marshal.WriteIntPtr(mappedBox, nint.Zero);
            var map = (MapFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 14), typeof(MapFn));
            int hr = map(_ctx11, resource, 0, D3D11_MAP_WRITE_DISCARD, 0, mappedBox);
            if (hr >= 0)
            {
                nint dest = Marshal.ReadIntPtr(mappedBox);
                if (dest != nint.Zero)
                    Marshal.Copy(data, 0, dest, Math.Min(data.Length, byteCount / 4));
            }
            var unmap = (UnmapFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 15), typeof(UnmapFn));
            unmap(_ctx11, resource, 0);
            Marshal.FreeHGlobal(mappedBox);
        }

        void SyncSwapchainSize()
        {
            if (_controlContext == null || _window == nint.Zero) return;
            _controlContext.GetWindowSize(_window, out int w, out int h);
            w = Math.Max(1, w);
            h = Math.Max(1, h);
            if (w == _width && h == _height) return;
            ResizeSwapchain(w, h);
        }

        void ResizeSwapchain(int w, int h)
        {
            try { WaitGpu(); } catch { }
            if (_ctx11 != nint.Zero)
            {
                UnbindRtv();
                Flush11();
            }
            for (int i = 0; i < FrameCount; i++)
            {
                if (_rtv11[i] != nint.Zero) { ComVtable.Release(_rtv11[i]); _rtv11[i] = nint.Zero; }
                if (_wrapped11[i] != nint.Zero) { ComVtable.Release(_wrapped11[i]); _wrapped11[i] = nint.Zero; }
                if (_bb12[i] != nint.Zero) { ComVtable.Release(_bb12[i]); _bb12[i] = nint.Zero; }
            }
            var resize = (ResizeBuffersFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_swap, 13), typeof(ResizeBuffersFn));
            int hr = resize(_swap, (uint)FrameCount, (uint)w, (uint)h, DXGI_FORMAT_R8G8B8A8_UNORM, 0);
            if (hr < 0)
                return;
            _width = w;
            _height = h;
            _frame = 0;
            for (uint i = 0; i < FrameCount; i++)
            {
                _bb12[i] = GetSwapBuffer(_swap, i);
                _wrapped11[i] = WrapResource(_bb12[i]);
                _rtv11[i] = CreateRtv11(_wrapped11[i]);
            }
            if (_backend != null)
            {
                _backend.Viewport(0, 0, (uint)_width, (uint)_height);
                _backend.Scissor(0, 0, (uint)_width, (uint)_height);
            }
        }

        void UnbindRtv()
        {
            var om = (OmSetFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 33), typeof(OmSetFn));
            om(_ctx11, 0, nint.Zero, nint.Zero);
        }

        void WaitGpu()
        {
            _fenceValue++;
            var sig = (SignalFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_queue, 14), typeof(SignalFn));
            sig(_queue, _fence, _fenceValue);
            var completed = (GetCompletedFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_fence, 8), typeof(GetCompletedFn));
            if (completed(_fence) < _fenceValue)
            {
                var setEv = (SetEventFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_fence, 9), typeof(SetEventFn));
                setEv(_fence, _fenceValue, _fenceEvent);
                WaitForSingleObject(_fenceEvent, 2000);
            }
        }

        public override void Terminate()
        {
            try { WaitGpu(); } catch { }
            foreach (var kv in _gpuSrv) ComVtable.Release(kv.Value);
            foreach (var kv in _gpuTex) ComVtable.Release(kv.Value);
            ComVtable.Release(_whiteSrv); ComVtable.Release(_whiteTex);
            ComVtable.Release(_sampler); ComVtable.Release(_cb); ComVtable.Release(_vb);
            ComVtable.Release(_layout); ComVtable.Release(_vs); ComVtable.Release(_ps);
            for (int i = 0; i < FrameCount; i++)
            {
                ComVtable.Release(_rtv11[i]);
                ComVtable.Release(_wrapped11[i]);
                ComVtable.Release(_bb12[i]);
            }
            ComVtable.Release(_on12);
            ComVtable.Release(_ctx11);
            ComVtable.Release(_device11);
            ComVtable.Release(_fence);
            ComVtable.Release(_swap);
            ComVtable.Release(_queue);
            ComVtable.Release(_factory);
            ComVtable.Release(_device12);
            if (_fenceEvent != nint.Zero) CloseHandle(_fenceEvent);
            _host?.Dispose();
        }

        static nint CreateCom<T>(nint device, int slot, ref T desc, Guid iid, string what) where T : struct
        {
            nint descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
            Marshal.StructureToPtr(desc, descPtr, false);
            nint iidPtr = GuidPtr(iid);
            nint outPtr = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(outPtr, nint.Zero);
            try
            {
                var fn = (CreateComFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(device, slot), typeof(CreateComFn));
                int hr = fn(device, descPtr, iidPtr, outPtr);
                ComVtable.ThrowIfFailed(hr, what);
                return Marshal.ReadIntPtr(outPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(descPtr);
                Marshal.FreeHGlobal(iidPtr);
                Marshal.FreeHGlobal(outPtr);
            }
        }

        static nint CreateFence(nint device)
        {
            nint iidPtr = GuidPtr(IID_ID3D12Fence);
            nint outPtr = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(outPtr, nint.Zero);
            try
            {
                var fn = (CreateFenceFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(device, 36), typeof(CreateFenceFn));
                int hr = fn(device, 0UL, 0, iidPtr, outPtr);
                ComVtable.ThrowIfFailed(hr, "CreateFence");
                return Marshal.ReadIntPtr(outPtr);
            }
            finally { Marshal.FreeHGlobal(iidPtr); Marshal.FreeHGlobal(outPtr); }
        }

        static nint CreateSwapChain(nint factory, nint queue, ref D3D12Native.DXGI_SWAP_CHAIN_DESC desc)
        {
            nint descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<D3D12Native.DXGI_SWAP_CHAIN_DESC>());
            Marshal.StructureToPtr(desc, descPtr, false);
            nint outPtr = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(outPtr, nint.Zero);
            try
            {
                var fn = (CreateScFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(factory, 10), typeof(CreateScFn));
                int hr = fn(factory, queue, descPtr, outPtr);
                ComVtable.ThrowIfFailed(hr, "IDXGIFactory.CreateSwapChain");
                return Marshal.ReadIntPtr(outPtr);
            }
            finally { Marshal.FreeHGlobal(descPtr); Marshal.FreeHGlobal(outPtr); }
        }

        static nint GetSwapBuffer(nint swap, uint index)
        {
            nint iidPtr = GuidPtr(IID_ID3D12Resource);
            nint outPtr = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(outPtr, nint.Zero);
            try
            {
                var fn = (GetBufFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(swap, 9), typeof(GetBufFn));
                int hr = fn(swap, index, iidPtr, outPtr);
                ComVtable.ThrowIfFailed(hr, "GetBuffer");
                return Marshal.ReadIntPtr(outPtr);
            }
            finally { Marshal.FreeHGlobal(iidPtr); Marshal.FreeHGlobal(outPtr); }
        }

        nint QueryOn12(nint device11)
        {
            nint iidPtr = GuidPtr(IID_ID3D11On12Device);
            nint outPtr = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(outPtr, nint.Zero);
            try
            {
                var fn = (QiFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(device11, 0), typeof(QiFn));
                int hr = fn(device11, iidPtr, outPtr);
                if (hr < 0) return nint.Zero;
                return Marshal.ReadIntPtr(outPtr);
            }
            finally { Marshal.FreeHGlobal(iidPtr); Marshal.FreeHGlobal(outPtr); }
        }

        nint WrapResource(nint res12)
        {
            var flags = new D3D11_RESOURCE_FLAGS { BindFlags = (uint)D3D11_BIND_RENDER_TARGET };
            nint flagsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<D3D11_RESOURCE_FLAGS>());
            Marshal.StructureToPtr(flags, flagsPtr, false);
            nint iidPtr = GuidPtr(IID_ID3D11Texture2D);
            nint outPtr = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(outPtr, nint.Zero);
            try
            {
                var fn = (WrapFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_on12, 3), typeof(WrapFn));
                int hr = fn(_on12, res12, flagsPtr, D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATE_PRESENT, iidPtr, outPtr);
                ComVtable.ThrowIfFailed(hr, "CreateWrappedResource");
                return Marshal.ReadIntPtr(outPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(flagsPtr);
                Marshal.FreeHGlobal(iidPtr);
                Marshal.FreeHGlobal(outPtr);
            }
        }

        void Acquire(nint wrapped)
        {
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, wrapped);
            var fn = (AcquireFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_on12, 5), typeof(AcquireFn));
            fn(_on12, box, 1);
            Marshal.FreeHGlobal(box);
        }

        void ReleaseWrap(nint wrapped)
        {
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, wrapped);
            var fn = (AcquireFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_on12, 4), typeof(AcquireFn));
            fn(_on12, box, 1);
            Marshal.FreeHGlobal(box);
        }

        void Flush11()
        {
            var fn = (FlushFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 111), typeof(FlushFn));
            fn(_ctx11);
        }

        nint CreateRtv11(nint resource)
        {
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateRtv11Fn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_device11, 9), typeof(CreateRtv11Fn));
                int hr = fn(_device11, resource, nint.Zero, box);
                ComVtable.ThrowIfFailed(hr, "CreateRenderTargetView");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(box); }
        }

        nint CreateVertexShader(nint bytecode, ulong len)
        {
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateShaderFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_device11, 12), typeof(CreateShaderFn));
                int hr = fn(_device11, bytecode, new UIntPtr(len), nint.Zero, box);
                ComVtable.ThrowIfFailed(hr, "CreateVertexShader");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(box); }
        }

        nint CreatePixelShader(nint bytecode, ulong len)
        {
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateShaderFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_device11, 15), typeof(CreateShaderFn));
                int hr = fn(_device11, bytecode, new UIntPtr(len), nint.Zero, box);
                ComVtable.ThrowIfFailed(hr, "CreatePixelShader");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(box); }
        }

        nint CreateInputLayout(D3D11_INPUT_ELEMENT_DESC[] elems, nint vs, ulong vsLen)
        {
            int sz = Marshal.SizeOf<D3D11_INPUT_ELEMENT_DESC>();
            nint arr = Marshal.AllocHGlobal(sz * elems.Length);
            for (int i = 0; i < elems.Length; i++)
                Marshal.StructureToPtr(elems[i], arr + i * sz, false);
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateLayoutFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_device11, 11), typeof(CreateLayoutFn));
                int hr = fn(_device11, arr, (uint)elems.Length, vs, new UIntPtr(vsLen), box);
                ComVtable.ThrowIfFailed(hr, "CreateInputLayout");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(arr); Marshal.FreeHGlobal(box); }
        }

        nint CreateBuffer11(int bytes, int bind, int usage, int cpuAccess)
        {
            var desc = new D3D11_BUFFER_DESC { ByteWidth = (uint)((bytes + 15) & ~15), Usage = usage, BindFlags = bind, CPUAccessFlags = cpuAccess };
            nint descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<D3D11_BUFFER_DESC>());
            Marshal.StructureToPtr(desc, descPtr, false);
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateBufFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_device11, 3), typeof(CreateBufFn));
                int hr = fn(_device11, descPtr, nint.Zero, box);
                ComVtable.ThrowIfFailed(hr, "CreateBuffer");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(descPtr); Marshal.FreeHGlobal(box); }
        }


        void SetRasterizer()
        {
            if (_rs == nint.Zero) return;
            var fn = (RsSetStateFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 43), typeof(RsSetStateFn));
            fn(_ctx11, _rs);
        }

        nint CreateRasterizer()
        {
            var desc = new D3D11_RASTERIZER_DESC
            {
                FillMode = D3D11_FILL_SOLID,
                CullMode = D3D11_CULL_NONE,
                FrontCounterClockwise = 0,
                DepthClipEnable = 1,
                ScissorEnable = 1
            };
            nint descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<D3D11_RASTERIZER_DESC>());
            Marshal.StructureToPtr(desc, descPtr, false);
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateRsFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_device11, 22), typeof(CreateRsFn));
                int hr = fn(_device11, descPtr, box);
                if (hr < 0)
                {
                    Console.WriteLine($"[DirectX12] CreateRasterizerState hr=0x{hr:X8}");
                    return nint.Zero;
                }
                Console.WriteLine("[DirectX12] Rasterizer Cull=NONE");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(descPtr); Marshal.FreeHGlobal(box); }
        }

        void SetBlend()
        {
            if (_blend == nint.Zero) return;
            float[] factor = { 1, 1, 1, 1 };
            nint fac = Marshal.AllocHGlobal(16);
            Marshal.Copy(factor, 0, fac, 4);
            var fn = (OmSetBlendFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_ctx11, 35), typeof(OmSetBlendFn));
            fn(_ctx11, _blend, fac, 0xFFFFFFFFu);
            Marshal.FreeHGlobal(fac);
        }

        nint CreateBlend()
        {
            var desc = new D3D11_BLEND_DESC
            {
                AlphaToCoverageEnable = 0,
                IndependentBlendEnable = 0
            };
            desc.RT0.BlendEnable = 1;
            desc.RT0.SrcBlend = 5;       // D3D11_BLEND_SRC_ALPHA
            desc.RT0.DestBlend = 6;      // D3D11_BLEND_INV_SRC_ALPHA
            desc.RT0.BlendOp = 1;        // ADD
            desc.RT0.SrcBlendAlpha = 2;  // ONE
            desc.RT0.DestBlendAlpha = 6; // INV_SRC_ALPHA
            desc.RT0.BlendOpAlpha = 1;
            desc.RT0.RenderTargetWriteMask = 0x0F;
            nint descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<D3D11_BLEND_DESC>());
            Marshal.StructureToPtr(desc, descPtr, false);
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateBlendFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_device11, 20), typeof(CreateBlendFn));
                int hr = fn(_device11, descPtr, box);
                if (hr < 0)
                {
                    Console.WriteLine($"[DirectX12] CreateBlendState hr=0x{hr:X8}");
                    return nint.Zero;
                }
                Console.WriteLine("[DirectX12] Blend SrcAlpha/InvSrcAlpha");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(descPtr); Marshal.FreeHGlobal(box); }
        }
        nint CreateSampler()
        {
            var desc = new D3D11_SAMPLER_DESC
            {
                Filter = D3D11_FILTER_MIN_MAG_LINEAR_MIP_POINT,
                AddressU = D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressV = D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressW = D3D11_TEXTURE_ADDRESS_CLAMP,
                MaxLOD = float.MaxValue
            };
            nint descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<D3D11_SAMPLER_DESC>());
            Marshal.StructureToPtr(desc, descPtr, false);
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateSampFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_device11, 23), typeof(CreateSampFn));
                int hr = fn(_device11, descPtr, box);
                ComVtable.ThrowIfFailed(hr, "CreateSamplerState");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(descPtr); Marshal.FreeHGlobal(box); }
        }

        nint CreateTexture2D(int w, int h, byte[] rgba)
        {
            var desc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)w, Height = (uint)h, MipLevels = 1, ArraySize = 1,
                Format = DXGI_FORMAT_R8G8B8A8_UNORM, SampleCount = 1, SampleQuality = 0,
                Usage = D3D11_USAGE_DEFAULT, BindFlags = D3D11_BIND_SHADER_RESOURCE
            };
            nint descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<D3D11_TEXTURE2D_DESC>());
            Marshal.StructureToPtr(desc, descPtr, false);
            nint initPtr = nint.Zero;
            if (rgba != null)
            {
                nint pix = Marshal.AllocHGlobal(rgba.Length);
                Marshal.Copy(rgba, 0, pix, rgba.Length);
                var data = new D3D11_SUBRESOURCE_DATA { pSysMem = pix, SysMemPitch = (uint)(w * 4) };
                initPtr = Marshal.AllocHGlobal(Marshal.SizeOf<D3D11_SUBRESOURCE_DATA>());
                Marshal.StructureToPtr(data, initPtr, false);
            }
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateTexFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_device11, 5), typeof(CreateTexFn));
                int hr = fn(_device11, descPtr, initPtr, box);
                ComVtable.ThrowIfFailed(hr, "CreateTexture2D");
                return Marshal.ReadIntPtr(box);
            }
            finally
            {
                Marshal.FreeHGlobal(descPtr);
                if (initPtr != nint.Zero)
                {
                    var data = Marshal.PtrToStructure<D3D11_SUBRESOURCE_DATA>(initPtr);
                    if (data.pSysMem != nint.Zero) Marshal.FreeHGlobal(data.pSysMem);
                    Marshal.FreeHGlobal(initPtr);
                }
                Marshal.FreeHGlobal(box);
            }
        }

        nint CreateSrv(nint resource)
        {
            var desc = new D3D11_SHADER_RESOURCE_VIEW_DESC
            {
                Format = DXGI_FORMAT_R8G8B8A8_UNORM,
                ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D,
                MipLevels = 1
            };
            nint descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<D3D11_SHADER_RESOURCE_VIEW_DESC>());
            Marshal.StructureToPtr(desc, descPtr, false);
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateSrvFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(_device11, 7), typeof(CreateSrvFn));
                int hr = fn(_device11, resource, descPtr, box);
                ComVtable.ThrowIfFailed(hr, "CreateShaderResourceView");
                return Marshal.ReadIntPtr(box);
            }
            finally { Marshal.FreeHGlobal(descPtr); Marshal.FreeHGlobal(box); }
        }

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
        static void ReleaseBlob(nint blob) { if (blob != nint.Zero) ComVtable.Release(blob); }

        static nint GuidPtr(Guid g)
        {
            nint p = Marshal.AllocHGlobal(16);
            Marshal.Copy(g.ToByteArray(), 0, p, 16);
            return p;
        }

        [StructLayout(LayoutKind.Sequential)] struct D3D11_RESOURCE_FLAGS { public uint BindFlags, MiscFlags, CPUAccessFlags, StructureByteStride; }
        [StructLayout(LayoutKind.Sequential)] struct D3D11_BUFFER_DESC { public uint ByteWidth; public int Usage; public int BindFlags; public int CPUAccessFlags; public int MiscFlags; public int StructureByteStride; }
        [StructLayout(LayoutKind.Sequential)] struct D3D11_TEXTURE2D_DESC { public uint Width, Height, MipLevels, ArraySize; public int Format; public uint SampleCount, SampleQuality; public int Usage, BindFlags, CPUAccessFlags, MiscFlags; }
        [StructLayout(LayoutKind.Sequential)] struct D3D11_SUBRESOURCE_DATA { public nint pSysMem; public uint SysMemPitch, SysMemSlicePitch; }
        [StructLayout(LayoutKind.Sequential)] struct D3D11_SHADER_RESOURCE_VIEW_DESC { public int Format; public int ViewDimension; public uint MostDetailedMip; public uint MipLevels; public uint pad0, pad1; }
        [StructLayout(LayoutKind.Sequential)] struct D3D11_SAMPLER_DESC { public int Filter, AddressU, AddressV, AddressW; public float MipLODBias; public uint MaxAnisotropy; public int ComparisonFunc; public float Border0, Border1, Border2, Border3; public float MinLOD, MaxLOD; }
        [StructLayout(LayoutKind.Sequential)] struct D3D11_RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct D3D11_VIEWPORT { public float TopLeftX, TopLeftY, Width, Height, MinDepth, MaxDepth; }
                [StructLayout(LayoutKind.Sequential)]
        struct D3D11_RENDER_TARGET_BLEND_DESC
        {
            public int BlendEnable;
            public int SrcBlend, DestBlend, BlendOp;
            public int SrcBlendAlpha, DestBlendAlpha, BlendOpAlpha;
            public byte RenderTargetWriteMask;
            public byte Pad0, Pad1, Pad2;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct D3D11_BLEND_DESC
        {
            public int AlphaToCoverageEnable;
            public int IndependentBlendEnable;
            public D3D11_RENDER_TARGET_BLEND_DESC RT0, RT1, RT2, RT3, RT4, RT5, RT6, RT7;
        }
        [StructLayout(LayoutKind.Sequential)] struct D3D11_RASTERIZER_DESC { public int FillMode, CullMode, FrontCounterClockwise, DepthBias; public float DepthBiasClamp, SlopeScaledDepthBias; public int DepthClipEnable, ScissorEnable, MultisampleEnable, AntialiasedLineEnable; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct D3D11_INPUT_ELEMENT_DESC
        {
            public string SemanticName;
            public uint SemanticIndex;
            public int Format;
            public uint InputSlot;
            public uint AlignedByteOffset;
            public int InputSlotClass;
            public uint InstanceDataStepRate;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateComFn(nint self, nint desc, nint riid, nint ppv);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateFenceFn(nint self, ulong initial, int flags, nint riid, nint ppv);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateScFn(nint self, nint device, nint desc, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetBufFn(nint self, uint index, nint riid, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int QiFn(nint self, nint riid, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int WrapFn(nint self, nint res12, nint flags, int inState, int outState, nint iid, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void AcquireFn(nint self, nint resources, uint count);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void FlushFn(nint self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateRtv11Fn(nint self, nint resource, nint desc, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateShaderFn(nint self, nint bytecode, UIntPtr len, nint linkage, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateLayoutFn(nint self, nint elems, uint count, nint vs, UIntPtr vsLen, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateBufFn(nint self, nint desc, nint initial, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateSampFn(nint self, nint desc, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateBlendFn(nint self, nint desc, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void OmSetBlendFn(nint self, nint blend, nint factor, uint mask);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateRsFn(nint self, nint desc, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void RsSetStateFn(nint self, nint rs);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateTexFn(nint self, nint desc, nint initial, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateSrvFn(nint self, nint resource, nint desc, nint pp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void OmSetFn(nint self, uint num, nint ppRTV, nint dsv);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void ClearRtvFn(nint self, nint rtv, nint color);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetVpFn(nint self, uint num, nint vp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetScissorFn(nint self, uint num, nint rects);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetTopoFn(nint self, int topo);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetVsFn(nint self, nint vs, nint inst, uint num);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetPsFn(nint self, nint ps, nint inst, uint num);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetIlFn(nint self, nint layout);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetCbFn(nint self, uint start, uint num, nint bufs);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetSampFn(nint self, uint start, uint num, nint ss);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetSrvFn(nint self, uint start, uint num, nint srvs);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetVbFn(nint self, uint start, uint num, nint bufs, nint strides, nint offsets);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void DrawFn(nint self, uint count, uint start);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ResizeBuffersFn(nint self, uint count, uint width, uint height, int format, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int MapFn(nint self, nint res, uint sub, int map, int flags, nint mapped);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void UnmapFn(nint self, nint res, uint sub);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SignalFn(nint self, nint fence, ulong value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate ulong GetCompletedFn(nint self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetEventFn(nint self, ulong value, nint ev);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate nint BlobPtrFn(nint self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate ulong BlobLenFn(nint self);

        [DllImport("kernel32.dll")] static extern nint CreateEventW(nint attr, bool manual, bool initial, string name);
        [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(nint handle, uint ms);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
    }
}
