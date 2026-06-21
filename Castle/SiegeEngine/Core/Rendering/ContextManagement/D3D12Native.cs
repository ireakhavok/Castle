using System;
using System.Runtime.InteropServices;

namespace SiegeEngine.Core.Rendering.ContextManagement
{
    public static class D3D12Native
    {
        private const string D3D12_DLL = "d3d12.dll";
        private const string DXGI_DLL = "dxgi.dll";
        private const string D3DCOMPILER_DLL = "d3dcompiler_47.dll";

        [DllImport(D3D12_DLL)] public static extern int D3D12CreateDevice(nint pAdapter, int minimumFeatureLevel, ref Guid riid, out nint ppDevice);
        [DllImport(DXGI_DLL)] public static extern int CreateDXGIFactory1(ref Guid riid, out nint ppFactory);
        [DllImport(DXGI_DLL)] public static extern int CreateSwapChain(nint pFactory, nint pDevice, ref DXGI_SWAP_CHAIN_DESC pDesc, out nint ppSwapChain);
        [DllImport(DXGI_DLL)] public static extern int IDXGISwapChain_Present(nint swapChain, int syncInterval, int flags);
        [DllImport(DXGI_DLL)] public static extern int GetBuffer(nint pSwapChain, uint buffer, ref Guid riid, out nint ppSurface);
        [DllImport(D3DCOMPILER_DLL)]
        public static extern int D3DCompile(
            string pSrcData, ulong srcDataSize, string pSourceName, nint pDefines,
            nint pInclude, string pEntryPoint, string pTarget,
            uint flags1, uint flags2, out nint ppCode, out nint ppErrorMsgs);

        public static readonly Guid IID_ID3D12Device = new Guid("189819f1-1db6-4b57-be54-1821339b85f7");
        public static readonly Guid IID_IDXGIFactory4 = new Guid("1bc6ea02-ef36-464f-bf0c-21ca39e5168a");
        public static readonly Guid IID_IDXGISwapChain = new Guid("310d36a0-d2e7-4c0a-aa04-6a9d23b8886a");
        public static readonly Guid IID_ID3D12Resource = new Guid("696442be-a72e-4059-bc79-5b5c98040fad");
        public static readonly Guid IID_ID3D12CommandQueue = new Guid("0ec870a6-5d7e-4c22-8cfc-5baae07616ed");
        public static readonly Guid IID_ID3D12DescriptorHeap = new Guid("8efb471d-616c-4f49-90f7-127bb763fa51");
        public static readonly Guid IID_ID3D12CommandAllocator = new Guid("6102dee4-af59-4b09-b999-b44d73f09b24");
        public static readonly Guid IID_ID3D12GraphicsCommandList = new Guid("5b160d0f-ac1b-4185-8ba8-b3ae42a5a455");
        public static readonly Guid IID_ID3D12Fence = new Guid("0a753dcf-c4d8-4b91-adf6-be5a60d95a76");
        public static readonly Guid IID_ID3D12RootSignature = new Guid("c54a6b66-72df-4ee8-8be5-a9466c944920");

        public enum D3D12_FEATURE_LEVEL : int
        {
            D3D_FEATURE_LEVEL_11_0 = 0xB000,
            D3D_FEATURE_LEVEL_11_1 = 0xB100,
            D3D_FEATURE_LEVEL_12_0 = 0xC000,
            D3D_FEATURE_LEVEL_12_1 = 0xC100
        }

        [ComImport, Guid("189819f1-1db6-4b57-be54-1821339b85f7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ID3D12Device
        {
            void QueryInterface(ref Guid riid, out nint ppvObject);
            uint AddRef();
            uint Release();
            nint GetPrivateData(ref Guid guid, ref uint pDataSize, nint pData);
            nint SetPrivateData(ref Guid guid, uint dataSize, nint pData);
            nint SetPrivateDataInterface(ref Guid guid, nint pData);
            nint SetName([MarshalAs(UnmanagedType.LPWStr)] string name);
            uint GetNodeCount();
            int CreateCommandQueue(ref D3D12_COMMAND_QUEUE_DESC pDesc, ref Guid riid, out nint ppCommandQueue);
            int CreateCommandAllocator(int type, ref Guid riid, out nint ppCommandAllocator);
            int CreateGraphicsCommandList(int nodeMask, int type, nint pCommandAllocator, nint pInitialState, ref Guid riid, out nint ppCommandList);
            int CheckFeatureSupport(int feature, nint pFeatureSupportData, uint featureSupportDataSize);
            int CreateDescriptorHeap(ref D3D12_DESCRIPTOR_HEAP_DESC pDesc, ref Guid riid, out nint ppvHeap);
            uint GetDescriptorHandleIncrementSize(int descriptorHeapType);
            int CreateRootSignature(uint nodeMask, nint pBlob, ulong blobLength, ref Guid riid, out nint ppvRootSignature);
            void CreateConstantBufferView(nint pDesc, nint destDescriptor);
            void CreateShaderResourceView(nint pResource, nint pDesc, nint destDescriptor);
            void CreateUnorderedAccessView(nint pResource, nint pCounterResource, nint pDesc, nint destDescriptor);
            void CreateRenderTargetView(nint pResource, nint pDesc, nint destDescriptor);
            void CreateDepthStencilView(nint pResource, nint pDesc, nint destDescriptor);
            void CreateSampler(nint pDesc, nint destDescriptor);
            void CopyDescriptors(uint numDestDescriptorRanges, nint pDestDescriptorRangeStarts, nint pDestDescriptorRangeSizes, uint numSrcDescriptorRanges, nint pSrcDescriptorRangeStarts, nint pSrcDescriptorRangeSizes, int descriptorHeapsType);
            void CopyDescriptorsSimple(uint numDescriptors, nint destDescriptorRangeStart, nint srcDescriptorRangeStart, int descriptorHeapsType);
            nint GetResourceAllocationInfo(uint visibleMask, uint numResourceDescs, nint pResourceDescs);
            nint GetCustomHeapProperties(uint nodeMask, int heapType);
            int CreateCommittedResource(ref D3D12_HEAP_PROPERTIES pHeapProperties, int heapFlags, ref D3D12_RESOURCE_DESC pDesc, int initialResourceState, nint pOptimizedClearValue, ref Guid riid, out nint ppvResource);
            int CreateHeap(ref D3D12_HEAP_DESC pDesc, ref Guid riid, out nint ppvHeap);
            int CreatePlacedResource(nint pHeap, ulong heapOffset, ref D3D12_RESOURCE_DESC pDesc, int initialState, nint pOptimizedClearValue, ref Guid riid, out nint ppvResource);
            int CreateReservedResource(ref D3D12_RESOURCE_DESC pDesc, int initialState, nint pOptimizedClearValue, ref Guid riid, out nint ppvResource);
            int CreateSharedHandle(nint pObject, nint pAttributes, uint access, [MarshalAs(UnmanagedType.LPWStr)] string name, out nint pHandle);
            int OpenSharedHandle(nint ntHandle, ref Guid riid, out nint ppvObj);
            int OpenSharedHandleByName([MarshalAs(UnmanagedType.LPWStr)] string name, uint access, out nint pNTHandle);
            int MakeResident(uint numObjects, nint ppObjects);
            int Evict(uint numObjects, nint ppObjects);
            int CreateFence(ulong initialValue, int flags, ref Guid riid, out nint ppFence);
            int GetDeviceRemovedReason();
            void GetCopyableFootprints(ref D3D12_RESOURCE_DESC pResourceDesc, uint firstSubresource, uint numSubresources, ulong baseOffset, nint pLayouts, nint pNumRows, nint pRowSizeInBytes, out ulong pTotalBytes);
            int CreateQueryHeap(ref D3D12_QUERY_HEAP_DESC pDesc, ref Guid riid, out nint ppvHeap);
            int SetStablePowerState(int enable);
            int CreateCommandSignature(ref D3D12_COMMAND_SIGNATURE_DESC pDesc, nint pRootSignature, ref Guid riid, out nint ppvCommandSignature);
            void GetResourceTiling(nint pTiledResource, out uint pNumTilesForEntireResource, out D3D12_PACKED_MIP_INFO pPackedMipDesc, out D3D12_TILE_SHAPE pStandardTileShapeForNonPackedMips, ref uint pNumSubresourceTilings, uint firstSubresourceTilingToGet, nint pSubresourceTilingsForNonPackedMips);
            nint GetAdapterLuid();
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_SWAP_CHAIN_DESC
        {
            public DXGI_MODE_DESC BufferDesc;
            public DXGI_SAMPLE_DESC SampleDesc;
            public uint BufferUsage;
            public uint BufferCount;
            public nint OutputWindow;
            public int Windowed;
            public int SwapEffect;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_MODE_DESC
        {
            public int Width;
            public int Height;
            public DXGI_RATIONAL RefreshRate;
            public int Format; // DXGI_FORMAT_R8G8B8A8_UNORM = 28
            public int ScanlineOrdering;
            public int Scaling;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_SAMPLE_DESC
        {
            public uint Count;
            public uint Quality;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_COMMAND_QUEUE_DESC
        {
            public int Type; // D3D12_COMMAND_LIST_TYPE_DIRECT = 0
            public int Priority;
            public uint Flags;
            public uint NodeMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_DESCRIPTOR_HEAP_DESC
        {
            public int Type; // D3D12_DESCRIPTOR_HEAP_TYPE_RTV = 0
            public uint NumDescriptors;
            public uint Flags;
            public uint NodeMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_HEAP_PROPERTIES
        {
            public int Type; // D3D12_HEAP_TYPE_DEFAULT = 1
            public uint CPUPageProperty;
            public uint MemoryPoolPreference;
            public uint CreationNodeMask;
            public uint VisibleNodeMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_RESOURCE_DESC
        {
            public int Dimension; // D3D12_RESOURCE_DIMENSION_BUFFER = 1
            public ulong Alignment;
            public ulong Width;
            public uint Height;
            public ushort DepthOrArraySize;
            public ushort MipLevels;
            public int Format;
            public DXGI_SAMPLE_DESC SampleDesc;
            public int Layout; // D3D12_TEXTURE_LAYOUT_ROW_MAJOR = 1
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_RESOURCE_BARRIER
        {
            public int Type; // D3D12_RESOURCE_BARRIER_TYPE_TRANSITION = 0
            public uint Flags;
            public nint pResource;
            public uint Subresource;
            public int StateBefore;
            public int StateAfter;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_VERTEX_BUFFER_VIEW
        {
            public nint BufferLocation;
            public uint SizeInBytes;
            public uint StrideInBytes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_GRAPHICS_PIPELINE_STATE_DESC
        {
            public nint pRootSignature;
            public D3D12_SHADER_BYTECODE VS;
            public D3D12_SHADER_BYTECODE PS;
            public D3D12_SHADER_BYTECODE DS;
            public D3D12_SHADER_BYTECODE HS;
            public D3D12_SHADER_BYTECODE GS;
            public D3D12_STREAM_OUTPUT_DESC StreamOutput;
            public D3D12_BLEND_DESC BlendState;
            public uint SampleMask;
            public D3D12_RASTERIZER_DESC RasterizerState;
            public D3D12_DEPTH_STENCIL_DESC DepthStencilState;
            public D3D12_INPUT_LAYOUT_DESC InputLayout;
            public int IBStripCutValue;
            public int PrimitiveTopologyType;
            public uint NumRenderTargets;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public int[] RTVFormats;
            public int DSVFormat;
            public DXGI_SAMPLE_DESC SampleDesc;
            public uint NodeMask;
            public D3D12_CACHED_PIPELINE_STATE CachedPSO;
            public int Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_SHADER_BYTECODE
        {
            public nint pShaderBytecode;
            public ulong BytecodeLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_STREAM_OUTPUT_DESC
        {
            public nint pSODeclaration;
            public uint NumEntries;
            public nint pBufferStrides;
            public uint NumStrides;
            public uint RasterizedStream;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_BLEND_DESC
        {
            public int AlphaToCoverageEnable;
            public int IndependentBlendEnable;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public D3D12_RENDER_TARGET_BLEND_DESC[] RenderTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_RENDER_TARGET_BLEND_DESC
        {
            public int BlendEnable;
            public int LogicOpEnable;
            public int SrcBlend;
            public int DestBlend;
            public int BlendOp;
            public int SrcBlendAlpha;
            public int DestBlendAlpha;
            public int BlendOpAlpha;
            public int LogicOp;
            public byte RenderTargetWriteMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_RASTERIZER_DESC
        {
            public int FillMode;
            public int CullMode;
            public int FrontCounterClockwise;
            public float DepthBias;
            public float DepthBiasClamp;
            public float SlopeScaledDepthBias;
            public int DepthClipEnable;
            public int MultisampleEnable;
            public int AntialiasedLineEnable;
            public uint ForcedSampleCount;
            public int ConservativeRaster;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_DEPTH_STENCIL_DESC
        {
            public int DepthEnable;
            public int DepthWriteMask;
            public int DepthFunc;
            public int StencilEnable;
            public byte StencilReadMask;
            public byte StencilWriteMask;
            public D3D12_DEPTH_STENCILOP_DESC FrontFace;
            public D3D12_DEPTH_STENCILOP_DESC BackFace;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_DEPTH_STENCILOP_DESC
        {
            public int StencilFailOp;
            public int StencilDepthFailOp;
            public int StencilPassOp;
            public int StencilFunc;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_INPUT_LAYOUT_DESC
        {
            public nint pInputElementDescs;
            public uint NumElements;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_CACHED_PIPELINE_STATE
        {
            public nint pCachedBlob;
            public ulong CachedBlobSizeInBytes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_PACKED_MIP_INFO
        {
            public uint NumStandardMips;
            public uint NumPackedMips;
            public uint NumTilesForPackedMips;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_TILE_SHAPE
        {
            public uint WidthInTexels;
            public uint HeightInTexels;
            public uint DepthInTexels;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_HEAP_DESC
        {
            public ulong SizeInBytes;
            public D3D12_HEAP_PROPERTIES Properties;
            public ulong Alignment;
            public int Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_QUERY_HEAP_DESC
        {
            public int Type; // D3D12_QUERY_HEAP_TYPE
            public uint Count;
            public uint NodeMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D12_COMMAND_SIGNATURE_DESC
        {
            public uint ByteStride;
            public uint NumArgumentDescs;
            public nint pArgumentDescs; // Pointer to D3D12_INDIRECT_ARGUMENT_DESC array
            public uint NodeMask;
        }
    }
}