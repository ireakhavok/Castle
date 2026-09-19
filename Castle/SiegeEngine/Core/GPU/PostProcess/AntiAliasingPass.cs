// Folder: SiegeEngine/Core/GPU/PostProcess
// File: AntiAliasingPass.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Numerics;

namespace SiegeEngine.Core.GPU.PostProcess
{
    /// <summary>
    /// Per-viewport AA resolve. World is drawn into a 1x color+depth target,
    /// then FXAA / SMAA / TAA is resolved back into the captured present
    /// framebuffer at the captured viewport origin.
    /// </summary>
    public unsafe class AntiAliasingPass : IDisposable
    {
        private readonly IRenderContext _rc;
        private readonly AbstractRenderEnums _e;

        private GpuHandle _copyPipe;
        private GpuHandle _fxaaPipe;
        private GpuHandle _smaaEdgePipe;
        private GpuHandle _smaaWeightPipe;
        private GpuHandle _smaaBlendPipe;
        private GpuHandle _taaPipe;

        private int _width;
        private int _height;

        private GpuHandle _world;
        private GpuHandle _edge;
        private GpuHandle _weight;
        private GpuHandle _history;
        private GpuHandle _resolve;

        private int _savedFbo;
        private int _savedVpX;
        private int _savedVpY;
        private int _savedVpW;
        private int _savedVpH;
        private int _savedScX;
        private int _savedScY;
        private int _savedScW;
        private int _savedScH;
        private bool _savedScissor;
        private bool _savedBlend;
        private bool _insideWorld;
        private static bool _loggedOnce;

        private bool _hasHistory;
        private Matrix4x4 _prevView = Matrix4x4.Identity;
        private Matrix4x4 _prevProjection = Matrix4x4.Identity;
        private bool _disposed;

        public AntiAliasingPass(IRenderContext renderContext)
        {
            _rc = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _e = _rc.Enums;
            _copyPipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.AaCopy, _rc));
            _fxaaPipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.AaFxaa, _rc));
            _smaaEdgePipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.AaSmaa, _rc));
            _smaaWeightPipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.AaSmaaWeight, _rc));
            _smaaBlendPipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.AaSmaaBlend, _rc));
            _taaPipe = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.AaTaa, _rc));
        }

        public void DiscardHistory()
        {
            _hasHistory = false;
        }

        public GpuHandle WorldColor => _rc.GetRenderTargetColor(_world);
        public GpuHandle WorldDepth => _rc.GetRenderTargetDepth(_world);
        public bool WorldDepthIsTexture => _rc.GetRenderTargetDepth(_world).IsValid;
        public int TargetWidth => _width;
        public int TargetHeight => _height;
        public bool IsWrappingWorld => _insideWorld;

        public void ReplaceWorldColor(GpuHandle sourceColor)
        {
            if (_disposed || !_world.IsValid || !sourceColor.IsValid || _width <= 0 || _height <= 0)
                return;
            _rc.BindRenderTarget(_world);
            _rc.Viewport(0, 0, (uint)_width, (uint)_height);
            _rc.Disable(_e.DepthTest);
            _rc.DepthMask(false);
            _rc.Disable(_e.Blend);
            _rc.ColorMask(true, true, true, true);
            DrawCopy(sourceColor);
        }

        public bool BeginWorld(AntiAliasingMode mode, int width, int height, Vector4 clearColor)
        {
            if (_disposed || mode == AntiAliasingMode.Off)
                return false;

            CapturePresentTarget();
            int tw = _savedVpW > 0 ? _savedVpW : width;
            int th = _savedVpH > 0 ? _savedVpH : height;
            if (tw <= 0 || th <= 0)
                return false;

            EnsureTargets(tw, th);

            if (!_loggedOnce)
            {
                _loggedOnce = true;
                Console.WriteLine($"[AntiAliasing] {mode} world={tw}x{th} present=({_savedVpX},{_savedVpY},{_savedVpW}x{_savedVpH}) fbo={_savedFbo}");
            }

            _rc.BindRenderTarget(_world);
            _rc.Viewport(0, 0, (uint)tw, (uint)th);
            _rc.Disable(_e.ScissorTest);
            _rc.Enable(_e.DepthTest);
            _rc.DepthMask(true);
            _rc.DepthFunc(_e.Less);
            _rc.ColorMask(true, true, true, true);
            _rc.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
            _rc.Clear(_e.ColorBufferBit | _e.DepthBufferBit);
            _insideWorld = true;
            return true;
        }

        public void Resolve(Matrix4x4 view, Matrix4x4 projection)
        {
            Resolve(AntiAliasingSettings.Resolve(), view, projection);
        }

        public void Resolve(AntiAliasingMode mode, Matrix4x4 view, Matrix4x4 projection)
        {
            if (_disposed || !_insideWorld)
                return;
            _insideWorld = false;

            _rc.Disable(_e.DepthTest);
            _rc.DepthMask(false);
            _rc.Disable(_e.Blend);
            _rc.Disable(_e.ScissorTest);
            _rc.ColorMask(true, true, true, true);
            _rc.Viewport(0, 0, (uint)_width, (uint)_height);

            if (mode == AntiAliasingMode.TAA && !WorldDepthIsTexture)
                mode = AntiAliasingMode.FXAA;

            switch (mode)
            {
                case AntiAliasingMode.FXAA:
                    BindPresent();
                    DrawFxaa(_rc.GetRenderTargetColor(_world));
                    break;
                case AntiAliasingMode.SMAA:
                    DrawSmaa();
                    BindPresent();
                    DrawCopy(_rc.GetRenderTargetColor(_resolve));
                    break;
                case AntiAliasingMode.TAA:
                    DrawTaa(view, projection);
                    _rc.BindRenderTarget(_history);
                    _rc.Viewport(0, 0, (uint)_width, (uint)_height);
                    DrawCopy(_rc.GetRenderTargetColor(_resolve));
                    BindPresent();
                    DrawCopy(_rc.GetRenderTargetColor(_resolve));
                    _prevView = view;
                    _prevProjection = projection;
                    _hasHistory = true;
                    break;
                default:
                    BindPresent();
                    DrawCopy(_rc.GetRenderTargetColor(_world));
                    break;
            }

            RestorePresentState();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DestroyTargets();
            DestroyPipe(ref _copyPipe);
            DestroyPipe(ref _fxaaPipe);
            DestroyPipe(ref _smaaEdgePipe);
            DestroyPipe(ref _smaaWeightPipe);
            DestroyPipe(ref _smaaBlendPipe);
            DestroyPipe(ref _taaPipe);
        }

        private void DestroyPipe(ref GpuHandle pipe)
        {
            if (!pipe.IsValid) return;
            _rc.Destroy(pipe);
            pipe = default;
        }

        private void CapturePresentTarget()
        {
            _rc.GetInteger(_e.FramebufferBinding, out _savedFbo);
            int* vp = stackalloc int[4];
            _rc.GetInteger(_e.Viewport, vp);
            _savedVpX = vp[0];
            _savedVpY = vp[1];
            _savedVpW = vp[2];
            _savedVpH = vp[3];
            int* sc = stackalloc int[4];
            _rc.GetInteger(_e.ScissorBox, sc);
            _savedScX = sc[0];
            _savedScY = sc[1];
            _savedScW = sc[2];
            _savedScH = sc[3];
            _rc.GetInteger(_e.ScissorTest, out int scissorOn);
            _savedScissor = scissorOn != 0;
            _rc.GetInteger(_e.Blend, out int blendOn);
            _savedBlend = blendOn != 0;

            if (_savedVpW <= 0 || _savedVpH <= 0)
            {
                _savedVpX = 0;
                _savedVpY = 0;
                _savedVpW = _rc.ViewportWidth;
                _savedVpH = _rc.ViewportHeight;
            }
        }

        private void BindPresent()
        {
            if (_savedFbo <= 0)
                _rc.BindDefaultRenderTarget();
            else
                _rc.BindRenderTarget(new GpuHandle((uint)_savedFbo, 1, GpuResourceKind.Texture));
            _rc.Viewport(_savedVpX, _savedVpY, (uint)Math.Max(_savedVpW, 1), (uint)Math.Max(_savedVpH, 1));
        }

        private void RestorePresentState()
        {
            BindPresent();
            _rc.Scissor(_savedScX, _savedScY, (uint)Math.Max(_savedScW, 1), (uint)Math.Max(_savedScH, 1));
            if (_savedScissor) _rc.Enable(_e.ScissorTest);
            else _rc.Disable(_e.ScissorTest);
            _rc.Enable(_e.DepthTest);
            _rc.DepthMask(true);
            _rc.DepthFunc(_e.Less);
            if (_savedBlend)
            {
                _rc.Enable(_e.Blend);
                _rc.BlendFunc(_e.SrcAlpha, _e.OneMinusSrcAlpha);
            }
            else
            {
                _rc.Disable(_e.Blend);
            }
            _rc.ColorMask(true, true, true, true);
            _rc.BindTextureSlot(0, default);
        }

        private void EnsureTargets(int width, int height)
        {
            if (_world.IsValid && _width == width && _height == height)
                return;

            DestroyTargets();
            _width = width;
            _height = height;
            _hasHistory = false;

            _world = ColorDepth(width, height, _e.Linear);
            _edge = ColorOnly(width, height, _e.Nearest);
            _weight = ColorOnly(width, height, _e.Nearest);
            _resolve = ColorOnly(width, height, _e.Linear);
            _history = ColorOnly(width, height, _e.Linear);
            _rc.BindDefaultRenderTarget();
        }

        private GpuHandle ColorDepth(int width, int height, int filter)
        {
            GpuHandle rt = _rc.CreateRenderTarget(new RenderTargetDesc
            {
                Width = width,
                Height = height,
                ColorFormat = _e.InternalRgba,
                DepthTexture = true,
                DepthFormat = _e.DepthComponent24
            });
            _rc.SetTextureParams(rt, filter, filter, _e.ClampToEdge, _e.ClampToEdge);
            GpuHandle depth = _rc.GetRenderTargetDepth(rt);
            if (depth.IsValid)
                _rc.SetTextureParams(depth, _e.Nearest, _e.Nearest, _e.ClampToEdge, _e.ClampToEdge);
            return rt;
        }

        private GpuHandle ColorOnly(int width, int height, int filter)
        {
            GpuHandle rt = _rc.CreateRenderTarget(new RenderTargetDesc
            {
                Width = width,
                Height = height,
                ColorFormat = _e.InternalRgba
            });
            _rc.SetTextureParams(rt, filter, filter, _e.ClampToEdge, _e.ClampToEdge);
            return rt;
        }

        void BindPost(int hasHistory = 0, int hasDepth = 0)
        {
            if (!_rc.TryGetConstants(ConstantSlot.Post, out PostCB post))
                post = default;
            post.InvResolution = new Vector4(1f / Math.Max(_width, 1), 1f / Math.Max(_height, 1), 0f, 0f);
            post.HasHistory = hasHistory;
            post.HasDepth = hasDepth;
            _rc.SetConstants(ConstantSlot.Post, post);
        }

        private void DrawSmaa()
        {
            _rc.BindRenderTarget(_edge);
            _rc.Viewport(0, 0, (uint)_width, (uint)_height);
            _rc.ClearColor(0f, 0f, 0f, 0f);
            _rc.Clear(_e.ColorBufferBit);
            _rc.BindPipeline(_smaaEdgePipe);
            BindPost();
            _rc.BindTextureSlot(0, _world);
            _rc.DrawFullscreen();

            _rc.BindRenderTarget(_weight);
            _rc.ClearColor(0f, 0f, 0f, 0f);
            _rc.Clear(_e.ColorBufferBit);
            _rc.BindPipeline(_smaaWeightPipe);
            BindPost();
            _rc.BindTextureSlot(0, _edge);
            _rc.DrawFullscreen();

            _rc.BindRenderTarget(_resolve);
            _rc.BindPipeline(_smaaBlendPipe);
            BindPost();
            _rc.BindTextureSlot(0, _world);
            _rc.BindTextureSlot(1, _weight);
            _rc.DrawFullscreen();
            _rc.BindTextureSlot(1, default);
        }

        private void DrawTaa(Matrix4x4 view, Matrix4x4 projection)
        {
            _rc.BindRenderTarget(_resolve);
            _rc.Viewport(0, 0, (uint)_width, (uint)_height);
            _rc.BindPipeline(_taaPipe);
            BindPost(_hasHistory ? 1 : 0, 1);
            _rc.BindTextureSlot(0, _world);
            _rc.BindTextureSlot(1, _history);
            GpuHandle depth = _rc.GetRenderTargetDepth(_world);
            _rc.BindTextureSlot(2, depth);
            _rc.BindCamera(view, projection, Matrix4x4.Identity);
            if (!_rc.TryGetConstants(ConstantSlot.Post, out PostCB taaPost))
                taaPost = default;
            taaPost.PrevView = _prevView;
            taaPost.PrevProjection = _prevProjection;
            taaPost.InvResolution = new Vector4(1f / Math.Max(_width, 1), 1f / Math.Max(_height, 1), 0f, 0f);
            taaPost.HasHistory = _hasHistory ? 1 : 0;
            taaPost.HasDepth = 1;
            _rc.SetConstants(ConstantSlot.Post, taaPost);
            _rc.DrawFullscreen();
            _rc.BindTextureSlot(2, default);
            _rc.BindTextureSlot(1, default);
        }

        private void DrawFxaa(GpuHandle color)
        {
            _rc.BindPipeline(_fxaaPipe);
            BindPost();
            _rc.BindTextureSlot(0, color);
            _rc.DrawFullscreen();
        }

        private void DrawCopy(GpuHandle color)
        {
            _rc.BindPipeline(_copyPipe);
            _rc.BindTextureSlot(0, color);
            _rc.DrawFullscreen();
        }

        private void DestroyTargets()
        {
            DestroyRt(ref _world);
            DestroyRt(ref _edge);
            DestroyRt(ref _weight);
            DestroyRt(ref _history);
            DestroyRt(ref _resolve);
            _width = 0;
            _height = 0;
        }

        private void DestroyRt(ref GpuHandle rt)
        {
            if (!rt.IsValid) return;
            _rc.Destroy(rt);
            rt = default;
        }
    }
}
