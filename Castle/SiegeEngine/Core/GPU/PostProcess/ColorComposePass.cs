// Folder: SiegeEngine/Core/GPU/PostProcess
// File: ColorComposePass.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Numerics;
using System.Diagnostics;

namespace SiegeEngine.Core.GPU.PostProcess
{
    /// <summary>
    /// HDR compose: extract brights, dual-filter pyramid, add bloom,
    /// expose, tonemap, grade. Writes an LDR color the AA pass can resolve.
    /// </summary>
    public unsafe class ColorComposePass : IDisposable
    {
        private const int MipCount = 4;

        private readonly IRenderContext _rc;
        private readonly AbstractRenderEnums _e;

        private GpuHandle _extractPipe;
        private GpuHandle _downPipe;
        private GpuHandle _upPipe;
        private GpuHandle _composePipe;
        private GpuHandle _lumaPipe;
        private GpuHandle _lumaDownPipe;
        private GpuHandle _adaptPipe;
        private long _lastAdaptStamp;
        private bool _hasAdapted;

        private int _width;
        private int _height;

        private GpuHandle _extract;
        private readonly GpuHandle[] _mip = new GpuHandle[MipCount];
        private readonly int[] _mipW = new int[MipCount];
        private readonly int[] _mipH = new int[MipCount];
        private GpuHandle _compose;
        private GpuHandle _luma;
        private GpuHandle _lumaDown;
        private GpuHandle _adaptA;
        private GpuHandle _adaptB;
        private bool _adaptPing;

        private bool _disposed;

        public ColorComposePass(IRenderContext renderContext)
        {
            _rc = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _e = _rc.Enums;
            _extractPipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.CcExtract, _rc));
            _downPipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.CcDownsample, _rc));
            _upPipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.CcUpsample, _rc));
            _composePipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.CcCompose, _rc));
            _lumaPipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.CcLuma, _rc));
            _lumaDownPipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.CcLumaDown, _rc));
            _adaptPipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.CcAdapt, _rc));
            _lastAdaptStamp = Stopwatch.GetTimestamp();
        }

        void BindPost(in ColorComposeState state, float invW = 1f, float invH = 1f, float intensity = 1f, int hasBloom = 0, int hasPrev = 0, float adapt = 0f)
        {
            _rc.SetConstants(ConstantSlot.Post, new PostCB
            {
                InvResolution = new Vector4(invW, invH, 0f, 0f),
                Threshold = state.BloomThreshold,
                Knee = MathF.Max(state.BloomThreshold * 0.5f, 0.05f),
                Exposure = state.Exposure,
                BloomIntensity = state.BloomIntensity,
                Contrast = state.Contrast,
                Saturation = state.Saturation,
                Temperature = state.Temperature,
                TargetLuma = state.TargetLuma,
                Adapt = adapt,
                HasBloom = hasBloom,
                HasPrev = hasPrev,
                AutoExposure = state.AutoExposure ? 1 : 0,
                Tonemap = (int)state.Tonemap,
                Intensity = intensity
            });
        }

        public GpuHandle ResolveColor => _rc.GetRenderTargetColor(_compose);

        public void Apply(GpuHandle sourceColor, int width, int height, ColorComposeState state)
        {
            if (_disposed || !sourceColor.IsValid || width <= 0 || height <= 0)
                return;
            if (!state.NeedsPass)
                return;

            EnsureTargets(width, height);

            _rc.Disable(_e.DepthTest);
            _rc.DepthMask(false);
            _rc.Disable(_e.Blend);
            _rc.Disable(_e.ScissorTest);
            _rc.ColorMask(true, true, true, true);

            GpuHandle bloomTex = default;
            if (state.BloomEnabled && state.BloomIntensity > 0.001f)
            {
                _rc.BindRenderTarget(_extract);
                _rc.Viewport(0, 0, (uint)_width, (uint)_height);
                _rc.BindPipeline(_extractPipe);
                BindPost(state, 1f / Math.Max(_width, 1), 1f / Math.Max(_height, 1));
                _rc.BindTextureSlot(0, sourceColor);
                DrawFullscreen();

                GpuHandle src = _rc.GetRenderTargetColor(_extract);
                int srcW = _width;
                int srcH = _height;
                for (int i = 0; i < MipCount; i++)
                {
                    _rc.BindRenderTarget(_mip[i]);
                    _rc.Viewport(0, 0, (uint)_mipW[i], (uint)_mipH[i]);
                    _rc.BindPipeline(_downPipe);
                    BindPost(state, 1f / Math.Max(srcW, 1), 1f / Math.Max(srcH, 1));
                    _rc.BindTextureSlot(0, src);
                    DrawFullscreen();
                    src = _rc.GetRenderTargetColor(_mip[i]);
                    srcW = _mipW[i];
                    srcH = _mipH[i];
                }

                for (int i = MipCount - 2; i >= 0; i--)
                {
                    _rc.BindRenderTarget(_mip[i]);
                    _rc.Viewport(0, 0, (uint)_mipW[i], (uint)_mipH[i]);
                    _rc.BindPipeline(_upPipe);
                    _rc.BindTextureSlot(0, _mip[i + 1]);
                    _rc.BindTextureSlot(1, _mip[i]);
                    BindPost(state, 1f / Math.Max(_mipW[i + 1], 1), 1f / Math.Max(_mipH[i + 1], 1), 1f);
                    DrawFullscreen();
                    _rc.BindTextureSlot(1, default);
                }

                bloomTex = _rc.GetRenderTargetColor(_mip[0]);
            }

            GpuHandle adaptedTex = default;
            if (state.AutoExposure)
                adaptedTex = MeterView(sourceColor, state);

            _rc.BindRenderTarget(_compose);
            _rc.Viewport(0, 0, (uint)_width, (uint)_height);
            _rc.BindPipeline(_composePipe);
            BindPost(state, 1f / Math.Max(_width, 1), 1f / Math.Max(_height, 1), hasBloom: bloomTex.IsValid ? 1 : 0);
            _rc.BindTextureSlot(0, sourceColor);
            _rc.BindTextureSlot(1, bloomTex);
            _rc.BindTextureSlot(2, adaptedTex);
            DrawFullscreen();
            _rc.BindTextureSlot(2, default);
            _rc.BindTextureSlot(1, default);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DestroyTargets();
            DestroyPipe(ref _extractPipe);
            DestroyPipe(ref _downPipe);
            DestroyPipe(ref _upPipe);
            DestroyPipe(ref _composePipe);
            DestroyPipe(ref _lumaPipe);
            DestroyPipe(ref _lumaDownPipe);
            DestroyPipe(ref _adaptPipe);
        }

        private void DestroyPipe(ref GpuHandle pipe)
        {
            if (!pipe.IsValid) return;
            _rc.Destroy(pipe);
            pipe = default;
        }

        private GpuHandle MeterView(GpuHandle sourceColor, ColorComposeState state)
        {
            int lumaW = Math.Max(_width / 8, 8);
            int lumaH = Math.Max(_height / 8, 8);

            _rc.BindRenderTarget(_luma);
            _rc.Viewport(0, 0, (uint)lumaW, (uint)lumaH);
            _rc.BindPipeline(_lumaPipe);
            _rc.BindTextureSlot(0, sourceColor);
            DrawFullscreen();

            _rc.BindRenderTarget(_lumaDown);
            _rc.Viewport(0, 0, 8, 8);
            _rc.BindPipeline(_lumaDownPipe);
            _rc.BindTextureSlot(0, _luma);
            DrawFullscreen();

            long now = Stopwatch.GetTimestamp();
            float dt = (now - _lastAdaptStamp) / (float)Stopwatch.Frequency;
            if (dt < 0f || dt > 0.25f) dt = 0.016f;
            _lastAdaptStamp = now;
            float tau = MathF.Max(state.AdaptSeconds, 0.05f);
            float k = 1f - MathF.Exp(-dt / tau);

            GpuHandle prev = _adaptPing ? _adaptB : _adaptA;
            GpuHandle dest = _adaptPing ? _adaptA : _adaptB;

            _rc.BindRenderTarget(dest);
            _rc.Viewport(0, 0, 1, 1);
            _rc.BindPipeline(_adaptPipe);
            _rc.SetConstants(ConstantSlot.Post, new PostCB
            {
                Adapt = k,
                HasPrev = _hasAdapted ? 1 : 0
            });
            _rc.BindTextureSlot(0, _lumaDown);
            _rc.BindTextureSlot(1, _hasAdapted ? prev : dest);
            DrawFullscreen();
            _rc.BindTextureSlot(1, default);

            _adaptPing = !_adaptPing;
            _hasAdapted = true;
            return dest;
        }

        private void EnsureTargets(int width, int height)
        {
            if (_compose.IsValid && _width == width && _height == height)
                return;

            DestroyTargets();
            _width = width;
            _height = height;

            _extract = ColorRt(width, height, true);

            int w = Math.Max(width / 2, 1);
            int h = Math.Max(height / 2, 1);
            for (int i = 0; i < MipCount; i++)
            {
                _mipW[i] = w;
                _mipH[i] = h;
                _mip[i] = ColorRt(w, h, true);
                w = Math.Max(w / 2, 1);
                h = Math.Max(h / 2, 1);
            }

            _compose = ColorRt(width, height, false);
            _luma = ColorRt(Math.Max(width / 8, 8), Math.Max(height / 8, 8), true);
            _lumaDown = ColorRt(8, 8, true);
            _adaptA = ColorRt(1, 1, true);
            _adaptB = ColorRt(1, 1, true);
            _hasAdapted = false;
        }

        private GpuHandle ColorRt(int width, int height, bool hdr)
        {
            GpuHandle rt = _rc.CreateRenderTarget(new RenderTargetDesc
            {
                Width = width,
                Height = height,
                ColorFormat = hdr ? _e.InternalRgba16f : _e.InternalRgba
            });
            _rc.SetTextureParams(rt, _e.Linear, _e.Linear, _e.ClampToEdge, _e.ClampToEdge);
            return rt;
        }

        private void DrawFullscreen()
        {
            _rc.Disable(_e.DepthTest);
            _rc.DepthMask(false);
            _rc.Disable(_e.CullFace);
            _rc.ColorMask(true, true, true, true);
            _rc.DrawFullscreen();
        }

        private void DestroyTargets()
        {
            DestroyRt(ref _extract);
            for (int i = 0; i < MipCount; i++)
            {
                DestroyRt(ref _mip[i]);
                _mipW[i] = 0;
                _mipH[i] = 0;
            }
            DestroyRt(ref _compose);
            DestroyRt(ref _luma);
            DestroyRt(ref _lumaDown);
            DestroyRt(ref _adaptA);
            DestroyRt(ref _adaptB);
            _width = 0;
            _height = 0;
            _hasAdapted = false;
        }

        private void DestroyRt(ref GpuHandle rt)
        {
            if (!rt.IsValid) return;
            _rc.Destroy(rt);
            rt = default;
        }
    }
}
