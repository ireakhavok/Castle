// Folder: SiegeEngine/Core/GPU/PostProcess
// File: AntiAliasingPass.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Numerics;
using SiegeEngine.Core.GPU.Shaders.OpenGL;

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

        private ShaderProgram _copy;
        private ShaderProgram _fxaa;
        private ShaderProgram _smaaEdge;
        private ShaderProgram _smaaWeight;
        private ShaderProgram _smaaBlend;
        private ShaderProgram _taa;
        private uint _emptyVao;

        private int _width;
        private int _height;

        private uint _worldFbo;
        private uint _worldColor;
        private uint _worldDepthTex;
        private uint _worldDepthRb;
        private bool _worldDepthIsTexture;

        private uint _edgeFbo;
        private uint _edgeColor;
        private uint _weightFbo;
        private uint _weightColor;
        private uint _historyFbo;
        private uint _historyColor;
        private uint _resolveFbo;
        private uint _resolveColor;

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
            _copy = new ShaderProgram(_rc, AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.CopyFragment);
            _fxaa = new ShaderProgram(_rc, AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.FxaaFragment);
            _smaaEdge = new ShaderProgram(_rc, AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.SmaaEdgeFragment);
            _smaaWeight = new ShaderProgram(_rc, AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.SmaaWeightFragment);
            _smaaBlend = new ShaderProgram(_rc, AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.SmaaBlendFragment);
            _taa = new ShaderProgram(_rc, AntiAliasingShaders.FullscreenVertex, AntiAliasingShaders.TaaFragment);
            _emptyVao = ((OpenGLRenderContext)_rc).GenVertexArray();
        }

        public void DiscardHistory()
        {
            _hasHistory = false;
        }

        public uint WorldColor => _worldColor;
        public uint WorldDepth => _worldDepthTex;
        public bool WorldDepthIsTexture => _worldDepthIsTexture;
        public int TargetWidth => _width;
        public int TargetHeight => _height;
        public bool IsWrappingWorld => _insideWorld;

        public void ReplaceWorldColor(uint sourceColor)
        {
            if (_disposed || _worldFbo == 0 || sourceColor == 0 || _width <= 0 || _height <= 0)
                return;
            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, _worldFbo);
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

            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, _worldFbo);
            _rc.Viewport(0, 0, (uint)tw, (uint)th);
            _rc.Disable(_e.ScissorTest);
            ((OpenGLRenderContext)_rc).DrawBuffer(_e.ColorAttachment0);
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

            if (mode == AntiAliasingMode.TAA && !_worldDepthIsTexture)
                mode = AntiAliasingMode.FXAA;

            switch (mode)
            {
                case AntiAliasingMode.FXAA:
                    BindPresent();
                    DrawFxaa(_worldColor);
                    break;
                case AntiAliasingMode.SMAA:
                    DrawSmaa();
                    BindPresent();
                    DrawCopy(_resolveColor);
                    break;
                case AntiAliasingMode.TAA:
                    DrawTaa(view, projection);
                    ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, _historyFbo);
                    _rc.Viewport(0, 0, (uint)_width, (uint)_height);
                    DrawCopy(_resolveColor);
                    BindPresent();
                    DrawCopy(_resolveColor);
                    _prevView = view;
                    _prevProjection = projection;
                    _hasHistory = true;
                    break;
                default:
                    BindPresent();
                    DrawCopy(_worldColor);
                    break;
            }

            RestorePresentState();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DestroyTargets();
            _copy?.Dispose();
            _fxaa?.Dispose();
            _smaaEdge?.Dispose();
            _smaaWeight?.Dispose();
            _smaaBlend?.Dispose();
            _taa?.Dispose();
            _copy = null;
            _fxaa = null;
            _smaaEdge = null;
            _smaaWeight = null;
            _smaaBlend = null;
            _taa = null;
            if (_emptyVao != 0)
            {
                ((OpenGLRenderContext)_rc).DeleteVertexArray(_emptyVao);
                _emptyVao = 0;
            }
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
            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, (uint)Math.Max(_savedFbo, 0));
            _rc.Viewport(_savedVpX, _savedVpY, (uint)Math.Max(_savedVpW, 1), (uint)Math.Max(_savedVpH, 1));
        }

        private void RestorePresentState()
        {
            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, (uint)Math.Max(_savedFbo, 0));
            _rc.Viewport(_savedVpX, _savedVpY, (uint)Math.Max(_savedVpW, 1), (uint)Math.Max(_savedVpH, 1));
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
                        ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0);
        }

        private void EnsureTargets(int width, int height)
        {
            if (_worldFbo != 0 && _width == width && _height == height)
                return;

            DestroyTargets();
            _width = width;
            _height = height;
            _hasHistory = false;

            _worldColor = CreateColorTex(width, height, _e.Linear);
            _worldFbo = CreateFbo(_worldColor);
            _worldDepthIsTexture = TryAttachDepthTexture(_worldFbo, width, height, out _worldDepthTex);
            if (!_worldDepthIsTexture)
            {
                ((OpenGLRenderContext)_rc).GenRenderbuffers(1, out _worldDepthRb);
                ((OpenGLRenderContext)_rc).BindRenderbuffer(_e.Renderbuffer, _worldDepthRb);
                ((OpenGLRenderContext)_rc).RenderbufferStorage(_e.Renderbuffer, _e.DepthComponent24, (uint)width, (uint)height);
                ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, _worldFbo);
                ((OpenGLRenderContext)_rc).FramebufferRenderbuffer(_e.Framebuffer, _e.DepthAttachment, _e.Renderbuffer, _worldDepthRb);
            }
            CheckFbo("world");

            _edgeColor = CreateColorTex(width, height, _e.Nearest);
            _edgeFbo = CreateFbo(_edgeColor);
            CheckFbo("smaa-edge");

            _weightColor = CreateColorTex(width, height, _e.Nearest);
            _weightFbo = CreateFbo(_weightColor);
            CheckFbo("smaa-weight");

            _resolveColor = CreateColorTex(width, height, _e.Linear);
            _resolveFbo = CreateFbo(_resolveColor);
            CheckFbo("resolve");

            _historyColor = CreateColorTex(width, height, _e.Linear);
            _historyFbo = CreateFbo(_historyColor);
            CheckFbo("history");

            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, 0);
        }

        private uint CreateColorTex(int width, int height, int filter)
        {
            ((OpenGLRenderContext)_rc).GenTextures(1, out uint tex);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, tex);
            ((OpenGLRenderContext)_rc).TexImage2D(_e.Texture2D, 0, _e.InternalRgba, (uint)width, (uint)height, 0, _e.PixelRgba, _e.UnsignedByte, null);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureMinFilter, filter);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureMagFilter, filter);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureWrapS, _e.ClampToEdge);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureWrapT, _e.ClampToEdge);
            return tex;
        }

        private uint CreateFbo(uint color)
        {
            ((OpenGLRenderContext)_rc).GenFramebuffers(1, out uint fbo);
            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, fbo);
            ((OpenGLRenderContext)_rc).FramebufferTexture2D(_e.Framebuffer, _e.ColorAttachment0, _e.Texture2D, color, 0);
            ((OpenGLRenderContext)_rc).DrawBuffer(_e.ColorAttachment0);
            return fbo;
        }

        private bool TryAttachDepthTexture(uint fbo, int width, int height, out uint depthTex)
        {
            depthTex = 0;
            ((OpenGLRenderContext)_rc).GenTextures(1, out uint tex);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, tex);
            ((OpenGLRenderContext)_rc).TexImage2D(_e.Texture2D, 0, _e.DepthComponent24, (uint)width, (uint)height, 0, _e.DepthComponent, _e.UnsignedInt, null);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureMinFilter, _e.Nearest);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureMagFilter, _e.Nearest);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureWrapS, _e.ClampToEdge);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureWrapT, _e.ClampToEdge);
            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, fbo);
            ((OpenGLRenderContext)_rc).FramebufferTexture2D(_e.Framebuffer, _e.DepthAttachment, _e.Texture2D, tex, 0);
            int status = ((OpenGLRenderContext)_rc).CheckFramebufferStatus(_e.Framebuffer);
            if (status == _e.FramebufferComplete)
            {
                depthTex = tex;
                return true;
            }
            ((OpenGLRenderContext)_rc).DeleteTexture(tex);
            return false;
        }

        private void CheckFbo(string name)
        {
            int status = ((OpenGLRenderContext)_rc).CheckFramebufferStatus(_e.Framebuffer);
            if (status != _e.FramebufferComplete)
                Console.WriteLine($"[AntiAliasingPass] {name} FBO incomplete, status={status}");
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
            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, _edgeFbo);
            _rc.Viewport(0, 0, (uint)_width, (uint)_height);
            _rc.ClearColor(0f, 0f, 0f, 0f);
            _rc.Clear(_e.ColorBufferBit);
            _smaaEdge.Use();
            BindPost();
            BindColor0(_worldColor);
            _smaaEdge.SetUniform("uColor", 0);
            _smaaEdge.SetUniform("uInvResolution", 1f / _width, 1f / _height);
            DrawFullscreen();

            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, _weightFbo);
            _rc.ClearColor(0f, 0f, 0f, 0f);
            _rc.Clear(_e.ColorBufferBit);
            _smaaWeight.Use();
            BindPost();
            BindColor0(_edgeColor);
            _smaaWeight.SetUniform("uEdges", 0);
            _smaaWeight.SetUniform("uInvResolution", 1f / _width, 1f / _height);
            DrawFullscreen();

            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, _resolveFbo);
            _smaaBlend.Use();
            BindPost();
            BindColor0(_worldColor);
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0 + 1);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, _weightColor);
            _smaaBlend.SetUniform("uColor", 0);
            _smaaBlend.SetUniform("uWeights", 1);
            _smaaBlend.SetUniform("uInvResolution", 1f / _width, 1f / _height);
            DrawFullscreen();
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0 + 1);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, 0);
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0);
        }

        private void DrawTaa(Matrix4x4 view, Matrix4x4 projection)
        {
            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, _resolveFbo);
            _rc.Viewport(0, 0, (uint)_width, (uint)_height);
            _taa.Use();
            BindPost(_hasHistory ? 1 : 0, 1);
            BindColor0(_worldColor);
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0 + 1);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, _historyColor);
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0 + 2);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, _worldDepthIsTexture ? _worldDepthTex : 0);
            _taa.SetUniform("uColor", TextureSlot.Color);
            _taa.SetUniform("uHistory", TextureSlot.History);
            _taa.SetUniform("uDepth", TextureSlot.Depth);
            _rc.BindCamera(view, projection, Matrix4x4.Identity);
            if (!_rc.TryGetConstants(ConstantSlot.Post, out PostCB taaPost))
                taaPost = default;
            taaPost.PrevView = _prevView;
            taaPost.PrevProjection = _prevProjection;
            taaPost.InvResolution = new Vector4(1f / Math.Max(_width, 1), 1f / Math.Max(_height, 1), 0f, 0f);
            taaPost.HasHistory = _hasHistory ? 1 : 0;
            taaPost.HasDepth = 1;
            _rc.SetConstants(ConstantSlot.Post, taaPost);
            DrawFullscreen();
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0 + 2);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, 0);
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0 + 1);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, 0);
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0);
        }

        private void DrawFxaa(uint color)
        {
            _fxaa.Use();
            BindPost();
            BindColor0(color);
            _fxaa.SetUniform("uColor", 0);
            _fxaa.SetUniform("uInvResolution", 1f / Math.Max(_width, 1), 1f / Math.Max(_height, 1));
            DrawFullscreen();
        }

        private void DrawCopy(uint color)
        {
            _copy.Use();
            BindColor0(color);
            _copy.SetUniform("uColor", 0);
            DrawFullscreen();
        }

        private void BindColor0(uint tex)
        {
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, tex);
        }

        private void DrawFullscreen()
        {
            _rc.DrawFullscreen();
        }

        private void DestroyTargets()
        {
            DeleteFbo(ref _worldFbo);
            DeleteFbo(ref _edgeFbo);
            DeleteFbo(ref _weightFbo);
            DeleteFbo(ref _historyFbo);
            DeleteFbo(ref _resolveFbo);
            DeleteTex(ref _worldColor);
            DeleteTex(ref _worldDepthTex);
            DeleteTex(ref _edgeColor);
            DeleteTex(ref _weightColor);
            DeleteTex(ref _historyColor);
            DeleteTex(ref _resolveColor);
            if (_worldDepthRb != 0)
            {
                uint rb = _worldDepthRb;
                ((OpenGLRenderContext)_rc).DeleteRenderbuffers(1, &rb);
                _worldDepthRb = 0;
            }
            _worldDepthIsTexture = false;
            _width = 0;
            _height = 0;
        }

        private void DeleteFbo(ref uint fbo)
        {
            if (fbo == 0) return;
            uint id = fbo;
            ((OpenGLRenderContext)_rc).DeleteFramebuffers(1, &id);
            fbo = 0;
        }

        private void DeleteTex(ref uint tex)
        {
            if (tex == 0) return;
            ((OpenGLRenderContext)_rc).DeleteTexture(tex);
            tex = 0;
        }
    }
}
