using System;
using System.Runtime.InteropServices;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    public sealed class D3D11ContextManager : ContextManager
    {
        const int D3D_DRIVER_TYPE_HARDWARE = 1;
        const int D3D11_SDK_VERSION = 7;
        const int DXGI_FORMAT_R8G8B8A8_UNORM = 28;
        const uint DXGI_USAGE_RENDER_TARGET_OUTPUT = 32;
        const int DXGI_SWAP_EFFECT_FLIP_DISCARD = 4;
        static readonly Guid IID_ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int D3D11CreateDeviceAndSwapChain(
            nint pAdapter, int driverType, nint software, uint flags,
            int[] featureLevels, uint featureLevelCount, uint sdkVersion,
            ref D3D12Native.DXGI_SWAP_CHAIN_DESC swapChainDesc,
            out nint swapChain, out nint device, out int featureLevel, out nint context);

        GlfwNoApiHost _host;
        nint _device, _context, _swapChain, _rtv;
        BackendRenderContext _backend;
        D3D11UiBatch _ui;
        int _width, _height;

        public override string BackendName => "DirectX11";
        public override bool DrawsUi => true;

        public override void Initialize(int width, int height, string title)
        {
            _width = Math.Max(1, width);
            _height = Math.Max(1, height);
            _host = new GlfwNoApiHost(_width, _height, title);
            _window = _host.WindowPtr;

            var desc = new D3D12Native.DXGI_SWAP_CHAIN_DESC
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
                BufferCount = 2,
                OutputWindow = _host.Hwnd,
                Windowed = 1,
                SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD,
                Flags = 0
            };

            int[] levels = { 0xB100, 0xB000 };
            int hr = D3D11CreateDeviceAndSwapChain(
                nint.Zero, D3D_DRIVER_TYPE_HARDWARE, nint.Zero, 0,
                levels, (uint)levels.Length, D3D11_SDK_VERSION,
                ref desc, out _swapChain, out _device, out int fl, out _context);
            ComVtable.ThrowIfFailed(hr, "D3D11CreateDeviceAndSwapChain");
            Console.WriteLine($"[DirectX11] Device+swapchain FL=0x{fl:X} hwnd=0x{_host.Hwnd:X}");

            nint backBuffer = GetBuffer(_swapChain, 0, IID_ID3D11Texture2D);
            _rtv = CreateRtv(_device, backBuffer);
            ComVtable.Release(backBuffer);

            _ui = new D3D11UiBatch();
            _ui.Attach(_device, _context);
            _backend = new BackendRenderContext("DirectX11", _width, _height);
            _backend.AttachUi(_ui);
            _renderContext = _backend;
            var control = new GlfwControlContext(_host.Glfw);
            control.SetPresentOverride(Present);
            _controlContext = control;
        }

        public override void Present()
        {
            if (_context == nint.Zero || _swapChain == nint.Zero) return;
            nint backBuffer = GetBuffer(_swapChain, 0, IID_ID3D11Texture2D);
            if (_rtv != nint.Zero) { ComVtable.Release(_rtv); _rtv = nint.Zero; }
            _rtv = CreateRtv(_device, backBuffer);
            ComVtable.Release(backBuffer);

            _ui.SetRenderTarget(_rtv, _width, _height);
            _ui.ClearRtv(_rtv, _backend.ClearR, _backend.ClearG, _backend.ClearB, _backend.ClearA);
            _backend.FlushDraws();
            ComVtable.Call2i(_swapChain, 8, 1, 0);
        }

        public override void Terminate()
        {
            _ui?.Dispose();
            ComVtable.Release(_rtv); _rtv = nint.Zero;
            ComVtable.Release(_swapChain); _swapChain = nint.Zero;
            ComVtable.Release(_context); _context = nint.Zero;
            ComVtable.Release(_device); _device = nint.Zero;
            _host?.Dispose();
            _host = null;
        }

        static nint GetBuffer(nint swapChain, uint index, Guid iid)
        {
            nint iidBuf = Marshal.AllocHGlobal(16);
            Marshal.Copy(iid.ToByteArray(), 0, iidBuf, 16);
            nint resultBox = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(resultBox, nint.Zero);
            try
            {
                var fn = (GetBufferFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(swapChain, 9), typeof(GetBufferFn));
                int hr = fn(swapChain, index, iidBuf, resultBox);
                ComVtable.ThrowIfFailed(hr, "IDXGISwapChain.GetBuffer");
                return Marshal.ReadIntPtr(resultBox);
            }
            finally
            {
                Marshal.FreeHGlobal(iidBuf);
                Marshal.FreeHGlobal(resultBox);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetBufferFn(nint self, uint buffer, nint riid, nint ppSurface);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreateRtvFn(nint self, nint resource, nint desc, nint ppRTV);

        static nint CreateRtv(nint device, nint resource)
        {
            nint box = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(box, nint.Zero);
            try
            {
                var fn = (CreateRtvFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(device, 9), typeof(CreateRtvFn));
                int hr = fn(device, resource, nint.Zero, box);
                ComVtable.ThrowIfFailed(hr, "ID3D11Device.CreateRenderTargetView");
                return Marshal.ReadIntPtr(box);
            }
            finally
            {
                Marshal.FreeHGlobal(box);
            }
        }
    }
}
