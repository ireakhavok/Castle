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
        private const int GL_VIEWPORT = 0x0BA2;
        private const int GL_FRAMEBUFFER_BINDING = 0x8CA6;
        private const int GL_SCISSOR_BOX = 0x0C10;
        private const int GL_SCISSOR_TEST = 0x0C11;
        private const int GL_BLEND = 0x0BE2;

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
            _emptyVao = _rc.GenVertexArray();
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
            _rc.BindFramebuffer(_e.Framebuffer, _worldFbo);
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

            _rc.BindFramebuffer(_e.Framebuffer, _worldFbo);
            _rc.Viewport(0, 0, (uint)tw, (uint)th);
            _rc.Disable(_e.ScissorTest);
            _rc.DrawBuffer(_e.ColorAttachment0);
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
                    _rc.BindFramebuffer(_e.Framebuffer, _historyFbo);
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
                _rc.DeleteVertexArray(_emptyVao);
                _emptyVao = 0;
            }
        }

        private void CapturePresentTarget()
        {
            _rc.GetInteger(GL_FRAMEBUFFER_BINDING, out _savedFbo);
            int* vp = stackalloc int[4];
            _rc.GetInteger(GL_VIEWPORT, vp);
            _savedVpX = vp[0];
            _savedVpY = vp[1];
            _savedVpW = vp[2];
            _savedVpH = vp[3];
            int* sc = stackalloc int[4];
            _rc.GetInteger(GL_SCISSOR_BOX, sc);
            _savedScX = sc[0];
            _savedScY = sc[1];
            _savedScW = sc[2];
            _savedScH = sc[3];
            _rc.GetInteger(GL_SCISSOR_TEST, out int scissorOn);
            _savedScissor = scissorOn != 0;
            _rc.GetInteger(GL_BLEND, out int blendOn);
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
            _rc.BindFramebuffer(_e.Framebuffer, (uint)Math.Max(_savedFbo, 0));
            _rc.Viewport(_savedVpX, _savedVpY, (uint)Math.Max(_savedVpW, 1), (uint)Math.Max(_savedVpH, 1));
        }

        private void RestorePresentState()
        {
            _rc.BindFramebuffer(_e.Framebuffer, (uint)Math.Max(_savedFbo, 0));
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
            _rc.BindVertexArray(0);
            _rc.ActiveTexture(_e.Texture0);
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
                _rc.GenRenderbuffers(1, out _worldDepthRb);
                _rc.BindRenderbuffer(_e.Renderbuffer, _worldDepthRb);
                _rc.RenderbufferStorage(_e.Renderbuffer, _e.DepthComponent24, (uint)width, (uint)height);
                _rc.BindFramebuffer(_e.Framebuffer, _worldFbo);
                _rc.FramebufferRenderbuffer(_e.Framebuffer, _e.DepthAttachment, _e.Renderbuffer, _worldDepthRb);
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

            _rc.BindFramebuffer(_e.Framebuffer, 0);
        }

        private uint CreateColorTex(int width, int height, int filter)
        {
            _rc.GenTextures(1, out uint tex);
            _rc.BindTexture(_e.Texture2D, tex);
            _rc.TexImage2D(_e.Texture2D, 0, _e.InternalRgba, (uint)width, (uint)height, 0, _e.PixelRgba, _e.UnsignedByte, null);
            _rc.TexParameter(_e.Texture2D, _e.TextureMinFilter, filter);
            _rc.TexParameter(_e.Texture2D, _e.TextureMagFilter, filter);
            _rc.TexParameter(_e.Texture2D, _e.TextureWrapS, _e.ClampToEdge);
            _rc.TexParameter(_e.Texture2D, _e.TextureWrapT, _e.ClampToEdge);
            return tex;
        }

        private uint CreateFbo(uint color)
        {
            _rc.GenFramebuffers(1, out uint fbo);
            _rc.BindFramebuffer(_e.Framebuffer, fbo);
            _rc.FramebufferTexture2D(_e.Framebuffer, _e.ColorAttachment0, _e.Texture2D, color, 0);
            _rc.DrawBuffer(_e.ColorAttachment0);
            return fbo;
        }

        private bool TryAttachDepthTexture(uint fbo, int width, int height, out uint depthTex)
        {
            depthTex = 0;
            _rc.GenTextures(1, out uint tex);
            _rc.BindTexture(_e.Texture2D, tex);
            _rc.TexImage2D(_e.Texture2D, 0, _e.DepthComponent24, (uint)width, (uint)height, 0, _e.DepthComponent, _e.UnsignedInt, null);
            _rc.TexParameter(_e.Texture2D, _e.TextureMinFilter, _e.Nearest);
            _rc.TexParameter(_e.Texture2D, _e.TextureMagFilter, _e.Nearest);
            _rc.TexParameter(_e.Texture2D, _e.TextureWrapS, _e.ClampToEdge);
            _rc.TexParameter(_e.Texture2D, _e.TextureWrapT, _e.ClampToEdge);
            _rc.BindFramebuffer(_e.Framebuffer, fbo);
            _rc.FramebufferTexture2D(_e.Framebuffer, _e.DepthAttachment, _e.Texture2D, tex, 0);
            int status = _rc.CheckFramebufferStatus(_e.Framebuffer);
            if (status == _e.FramebufferComplete)
            {
                depthTex = tex;
                return true;
            }
            _rc.DeleteTexture(tex);
            return false;
        }

        private void CheckFbo(string name)
        {
            int status = _rc.CheckFramebufferStatus(_e.Framebuffer);
            if (status != _e.FramebufferComplete)
                Console.WriteLine($"[AntiAliasingPass] {name} FBO incomplete, status={status}");
        }

        private void DrawSmaa()
        {
            _rc.BindFramebuffer(_e.Framebuffer, _edgeFbo);
            _rc.Viewport(0, 0, (uint)_width, (uint)_height);
            _rc.ClearColor(0f, 0f, 0f, 0f);
            _rc.Clear(_e.ColorBufferBit);
            _smaaEdge.Use();
            BindColor0(_worldColor);
            _smaaEdge.SetUniform("uColor", 0);
            _smaaEdge.SetUniform("uInvResolution", 1f / _width, 1f / _height);
            DrawFullscreen();

            _rc.BindFramebuffer(_e.Framebuffer, _weightFbo);
            _rc.ClearColor(0f, 0f, 0f, 0f);
            _rc.Clear(_e.ColorBufferBit);
            _smaaWeight.Use();
            BindColor0(_edgeColor);
            _smaaWeight.SetUniform("uEdges", 0);
            _smaaWeight.SetUniform("uInvResolution", 1f / _width, 1f / _height);
            DrawFullscreen();

            _rc.BindFramebuffer(_e.Framebuffer, _resolveFbo);
            _smaaBlend.Use();
            BindColor0(_worldColor);
            _rc.ActiveTexture(_e.Texture0 + 1);
            _rc.BindTexture(_e.Texture2D, _weightColor);
            _smaaBlend.SetUniform("uColor", 0);
            _smaaBlend.SetUniform("uWeights", 1);
            _smaaBlend.SetUniform("uInvResolution", 1f / _width, 1f / _height);
            DrawFullscreen();
            _rc.ActiveTexture(_e.Texture0 + 1);
            _rc.BindTexture(_e.Texture2D, 0);
            _rc.ActiveTexture(_e.Texture0);
        }

        private void DrawTaa(Matrix4x4 view, Matrix4x4 projection)
        {
            _rc.BindFramebuffer(_e.Framebuffer, _resolveFbo);
            _rc.Viewport(0, 0, (uint)_width, (uint)_height);
            _taa.Use();
            BindColor0(_worldColor);
            _rc.ActiveTexture(_e.Texture0 + 1);
            _rc.BindTexture(_e.Texture2D, _historyColor);
            _rc.ActiveTexture(_e.Texture0 + 2);
            _rc.BindTexture(_e.Texture2D, _worldDepthIsTexture ? _worldDepthTex : 0);
            _taa.SetUniform("uColor", 0);
            _taa.SetUniform("uHistory", 1);
            _taa.SetUniform("uDepth", 2);
            _taa.SetMatrix4("uView", view);
            _taa.SetMatrix4("uProjection", projection);
            _taa.SetMatrix4("uPrevView", _prevView);
            _taa.SetMatrix4("uPrevProjection", _prevProjection);
            _taa.SetUniform("uInvResolution", 1f / _width, 1f / _height);
            _taa.SetUniform("uHasHistory", _hasHistory ? 1 : 0);
            DrawFullscreen();
            _rc.ActiveTexture(_e.Texture0 + 2);
            _rc.BindTexture(_e.Texture2D, 0);
            _rc.ActiveTexture(_e.Texture0 + 1);
            _rc.BindTexture(_e.Texture2D, 0);
            _rc.ActiveTexture(_e.Texture0);
        }

        private void DrawFxaa(uint color)
        {
            _fxaa.Use();
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
            _rc.ActiveTexture(_e.Texture0);
            _rc.BindTexture(_e.Texture2D, tex);
        }

        private void DrawFullscreen()
        {
            _rc.BindVertexArray(_emptyVao);
            _rc.DrawArrays(_e.Triangles, 0, 3);
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
                _rc.DeleteRenderbuffers(1, &rb);
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
            _rc.DeleteFramebuffers(1, &id);
            fbo = 0;
        }

        private void DeleteTex(ref uint tex)
        {
            if (tex == 0) return;
            _rc.DeleteTexture(tex);
            tex = 0;
        }
    }
}
