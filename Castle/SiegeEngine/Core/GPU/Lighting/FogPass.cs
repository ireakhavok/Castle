// Folder: SiegeEngine/Core/GPU/Lighting
// File: FogPass.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Lighting
{
    /// <summary>
    /// Volumetric fog / light-shaft composite. Standard exponential and
    /// height fog are applied in the forward shaders. This pass only runs
    /// when FogMode.Volumetric is selected and a world color+depth pair is
    /// available (typically the AA world target).
    /// </summary>
    public unsafe class FogPass : IDisposable
    {
        private readonly IRenderContext _rc;
        private readonly AbstractRenderEnums _e;
        private ShaderProgram _volumetric;
        private uint _emptyVao;
        private uint _resolveFbo;
        private uint _resolveColor;
        private int _width;
        private int _height;
        private bool _disposed;

        public FogPass(IRenderContext renderContext)
        {
            _rc = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _e = _rc.Enums;
            _volumetric = ShaderProgram.FromId(_rc, ShaderId.Fog);
            _emptyVao = ((OpenGLRenderContext)_rc).GenVertexArray();
        }

        public void Apply(LightingFrame frame, Matrix4x4 view, Matrix4x4 projection, uint colorTex, uint depthTex, bool depthIsTexture, int width, int height)
        {
            if (_disposed || frame == null)
                return;
            if (frame.Fog.Mode != FogMode.Volumetric || frame.Fog.Quality == FogQuality.Off)
                return;
            if (colorTex == 0 || width <= 0 || height <= 0)
                return;

            EnsureTarget(width, height);
            if (!Matrix4x4.Invert(view, out Matrix4x4 invView))
                invView = Matrix4x4.Identity;
            if (!Matrix4x4.Invert(projection, out Matrix4x4 invProj))
                invProj = Matrix4x4.Identity;

            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, _resolveFbo);
            _rc.Viewport(0, 0, (uint)width, (uint)height);
            _rc.Disable(_e.DepthTest);
            _rc.DepthMask(false);
            _rc.Disable(_e.Blend);
            _volumetric.Use();
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, colorTex);
            _volumetric.SetUniform("uColor", 0);
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0 + 1);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, depthIsTexture ? depthTex : 0);
            _volumetric.SetUniform("uDepth", 1);
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0 + 2);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, frame.ShadowAtlas);
            _volumetric.SetUniform("uShadowAtlas", 2);

            FrameCB frameCb;
            if (!_rc.TryGetConstants(ConstantSlot.Frame, out frameCb))
                frameCb = new FrameCB { View = Matrix4x4.Identity, Projection = Matrix4x4.Identity };
            frameCb.View = view;
            frameCb.Projection = projection;
            _rc.SetConstants(ConstantSlot.Frame, frameCb);
            frame.ApplyConstants(_rc);

            PostCB post;
            if (!_rc.TryGetConstants(ConstantSlot.Post, out post))
                post = default;
            post.InvView = invView;
            post.InvProjection = invProj;
            post.InvResolution = new Vector4(1f / width, 1f / height, 0f, 0f);
            post.Intensity = frame.Fog.VolumetricIntensity;
            post.Steps = frame.Fog.RaySteps;
            post.HasDepth = depthIsTexture ? 1 : 0;
            _rc.SetConstants(ConstantSlot.Post, post);

            _rc.Disable(_e.DepthTest);
            _rc.DepthMask(false);
            _rc.Disable(_e.CullFace);
            _rc.ColorMask(true, true, true, true);
            _rc.DrawFullscreen();
            ((OpenGLRenderContext)_rc).ActiveTexture(_e.Texture0);
        }

        public uint ResolveColor => _resolveColor;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _volumetric?.Dispose();
            _volumetric = null;
            if (_emptyVao != 0)
            {
                ((OpenGLRenderContext)_rc).DeleteVertexArray(_emptyVao);
                _emptyVao = 0;
            }
            if (_resolveFbo != 0)
            {
                uint fbo = _resolveFbo;
                ((OpenGLRenderContext)_rc).DeleteFramebuffers(1, &fbo);
                _resolveFbo = 0;
            }
            if (_resolveColor != 0)
            {
                ((OpenGLRenderContext)_rc).DeleteTexture(_resolveColor);
                _resolveColor = 0;
            }
        }

        private void EnsureTarget(int width, int height)
        {
            if (_resolveFbo != 0 && _width == width && _height == height)
                return;
            if (_resolveFbo != 0)
            {
                uint fbo = _resolveFbo;
                ((OpenGLRenderContext)_rc).DeleteFramebuffers(1, &fbo);
                _resolveFbo = 0;
            }
            if (_resolveColor != 0)
            {
                ((OpenGLRenderContext)_rc).DeleteTexture(_resolveColor);
                _resolveColor = 0;
            }
            _width = width;
            _height = height;
            ((OpenGLRenderContext)_rc).GenTextures(1, out _resolveColor);
            ((OpenGLRenderContext)_rc).BindTexture(_e.Texture2D, _resolveColor);
            ((OpenGLRenderContext)_rc).TexImage2D(_e.Texture2D, 0, _e.InternalRgba, (uint)width, (uint)height, 0, _e.PixelRgba, _e.UnsignedByte, null);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureMinFilter, _e.Linear);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureMagFilter, _e.Linear);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureWrapS, _e.ClampToEdge);
            ((OpenGLRenderContext)_rc).TexParameter(_e.Texture2D, _e.TextureWrapT, _e.ClampToEdge);
            ((OpenGLRenderContext)_rc).GenFramebuffers(1, out _resolveFbo);
            ((OpenGLRenderContext)_rc).BindFramebuffer(_e.Framebuffer, _resolveFbo);
            ((OpenGLRenderContext)_rc).FramebufferTexture2D(_e.Framebuffer, _e.ColorAttachment0, _e.Texture2D, _resolveColor, 0);
            ((OpenGLRenderContext)_rc).DrawBuffer(_e.ColorAttachment0);
        }
    }
}
