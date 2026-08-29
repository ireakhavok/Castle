// Folder: SiegeEngine/Core/GPU/PostProcess
// File: ColorComposePass.cs
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;

namespace SiegeEngine.Core.GPU.PostProcess
{
    /// <summary>
    /// HDR compose: extract brights, dual-filter pyramid, add bloom,
    /// expose, tonemap, grade. Writes an LDR color the AA pass can resolve.
    /// </summary>
    public unsafe class ColorComposePass : IDisposable
    {
        private const int MipCount = 4;
        private const int GL_RGBA16F = 0x881A;

        private readonly IRenderContext _rc;
        private readonly AbstractRenderEnums _e;

        private ShaderProgram _extract;
        private ShaderProgram _down;
        private ShaderProgram _up;
        private ShaderProgram _compose;
        private uint _emptyVao;

        private int _width;
        private int _height;

        private uint _extractFbo;
        private uint _extractColor;
        private readonly uint[] _mipFbo = new uint[MipCount];
        private readonly uint[] _mipColor = new uint[MipCount];
        private readonly int[] _mipW = new int[MipCount];
        private readonly int[] _mipH = new int[MipCount];
        private uint _composeFbo;
        private uint _composeColor;

        private bool _disposed;

        public ColorComposePass(IRenderContext renderContext)
        {
            _rc = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _e = _rc.Enums;
            _extract = new ShaderProgram(_rc, ColorComposeShaders.FullscreenVertex, ColorComposeShaders.ExtractFragment);
            _down = new ShaderProgram(_rc, ColorComposeShaders.FullscreenVertex, ColorComposeShaders.DownsampleFragment);
            _up = new ShaderProgram(_rc, ColorComposeShaders.FullscreenVertex, ColorComposeShaders.UpsampleFragment);
            _compose = new ShaderProgram(_rc, ColorComposeShaders.FullscreenVertex, ColorComposeShaders.ComposeFragment);
            _emptyVao = _rc.GenVertexArray();
        }

        public uint ResolveColor => _composeColor;

        public void Apply(uint sourceColor, int width, int height, ColorComposeState state)
        {
            if (_disposed || sourceColor == 0 || width <= 0 || height <= 0)
                return;
            if (!state.NeedsPass)
                return;

            EnsureTargets(width, height);

            _rc.Disable(_e.DepthTest);
            _rc.DepthMask(false);
            _rc.Disable(_e.Blend);
            _rc.Disable(_e.ScissorTest);
            _rc.ColorMask(true, true, true, true);

            uint bloomTex = 0;
            if (state.BloomEnabled && state.BloomIntensity > 0.001f)
            {
                _rc.BindFramebuffer(_e.Framebuffer, _extractFbo);
                _rc.Viewport(0, 0, (uint)_width, (uint)_height);
                _extract.Use();
                Bind0(sourceColor);
                _extract.SetUniform("uColor", 0);
                _extract.SetUniform("uThreshold", state.BloomThreshold);
                _extract.SetUniform("uKnee", MathF.Max(state.BloomThreshold * 0.5f, 0.05f));
                DrawFullscreen();

                uint src = _extractColor;
                int srcW = _width;
                int srcH = _height;
                for (int i = 0; i < MipCount; i++)
                {
                    _rc.BindFramebuffer(_e.Framebuffer, _mipFbo[i]);
                    _rc.Viewport(0, 0, (uint)_mipW[i], (uint)_mipH[i]);
                    _down.Use();
                    Bind0(src);
                    _down.SetUniform("uColor", 0);
                    _down.SetUniform("uInvResolution", 1f / Math.Max(srcW, 1), 1f / Math.Max(srcH, 1));
                    DrawFullscreen();
                    src = _mipColor[i];
                    srcW = _mipW[i];
                    srcH = _mipH[i];
                }

                for (int i = MipCount - 2; i >= 0; i--)
                {
                    _rc.BindFramebuffer(_e.Framebuffer, _mipFbo[i]);
                    _rc.Viewport(0, 0, (uint)_mipW[i], (uint)_mipH[i]);
                    _up.Use();
                    Bind0(_mipColor[i + 1]);
                    _rc.ActiveTexture(_e.Texture0 + 1);
                    _rc.BindTexture(_e.Texture2D, _mipColor[i]);
                    _up.SetUniform("uLow", 0);
                    _up.SetUniform("uHigh", 1);
                    _up.SetUniform("uInvResolution", 1f / Math.Max(_mipW[i + 1], 1), 1f / Math.Max(_mipH[i + 1], 1));
                    _up.SetUniform("uAddLow", 1f);
                    DrawFullscreen();
                    _rc.ActiveTexture(_e.Texture0 + 1);
                    _rc.BindTexture(_e.Texture2D, 0);
                }

                bloomTex = _mipColor[0];
            }

            _rc.BindFramebuffer(_e.Framebuffer, _composeFbo);
            _rc.Viewport(0, 0, (uint)_width, (uint)_height);
            _compose.Use();
            Bind0(sourceColor);
            _rc.ActiveTexture(_e.Texture0 + 1);
            _rc.BindTexture(_e.Texture2D, bloomTex);
            _compose.SetUniform("uColor", 0);
            _compose.SetUniform("uBloom", 1);
            _compose.SetUniform("uHasBloom", bloomTex != 0 ? 1 : 0);
            _compose.SetUniform("uBloomIntensity", state.BloomIntensity);
            _compose.SetUniform("uExposure", state.Exposure);
            _compose.SetUniform("uTonemap", (int)state.Tonemap);
            _compose.SetUniform("uContrast", state.Contrast);
            _compose.SetUniform("uSaturation", state.Saturation);
            _compose.SetUniform("uTemperature", state.Temperature);
            DrawFullscreen();
            _rc.ActiveTexture(_e.Texture0 + 1);
            _rc.BindTexture(_e.Texture2D, 0);
            _rc.ActiveTexture(_e.Texture0);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DestroyTargets();
            _extract?.Dispose();
            _down?.Dispose();
            _up?.Dispose();
            _compose?.Dispose();
            _extract = null;
            _down = null;
            _up = null;
            _compose = null;
            if (_emptyVao != 0)
            {
                _rc.DeleteVertexArray(_emptyVao);
                _emptyVao = 0;
            }
        }

        private void EnsureTargets(int width, int height)
        {
            if (_composeFbo != 0 && _width == width && _height == height)
                return;

            DestroyTargets();
            _width = width;
            _height = height;

            _extractColor = CreateColor(width, height, preferHdr: true);
            _extractFbo = CreateFbo(_extractColor);

            int w = Math.Max(width / 2, 1);
            int h = Math.Max(height / 2, 1);
            for (int i = 0; i < MipCount; i++)
            {
                _mipW[i] = w;
                _mipH[i] = h;
                _mipColor[i] = CreateColor(w, h, preferHdr: true);
                _mipFbo[i] = CreateFbo(_mipColor[i]);
                w = Math.Max(w / 2, 1);
                h = Math.Max(h / 2, 1);
            }

            _composeColor = CreateColor(width, height, preferHdr: false);
            _composeFbo = CreateFbo(_composeColor);
        }

        private uint CreateColor(int width, int height, bool preferHdr)
        {
            _rc.GenTextures(1, out uint tex);
            _rc.BindTexture(_e.Texture2D, tex);
            if (preferHdr)
            {
                _rc.TexImage2D(_e.Texture2D, 0, GL_RGBA16F, (uint)width, (uint)height, 0, _e.PixelRgba, _e.Float, null);
            }
            else
            {
                _rc.TexImage2D(_e.Texture2D, 0, _e.InternalRgba, (uint)width, (uint)height, 0, _e.PixelRgba, _e.UnsignedByte, null);
            }
            _rc.TexParameter(_e.Texture2D, _e.TextureMinFilter, _e.Linear);
            _rc.TexParameter(_e.Texture2D, _e.TextureMagFilter, _e.Linear);
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

        private void Bind0(uint tex)
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
            DeleteFbo(ref _extractFbo);
            DeleteTex(ref _extractColor);
            for (int i = 0; i < MipCount; i++)
            {
                DeleteFbo(ref _mipFbo[i]);
                DeleteTex(ref _mipColor[i]);
                _mipW[i] = 0;
                _mipH[i] = 0;
            }
            DeleteFbo(ref _composeFbo);
            DeleteTex(ref _composeColor);
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
