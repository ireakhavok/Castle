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
            _volumetric = new ShaderProgram(_rc, FogShaders.FullscreenVertex, FogShaders.VolumetricFragment);
            _emptyVao = _rc.GenVertexArray();
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

            _rc.BindFramebuffer(_e.Framebuffer, _resolveFbo);
            _rc.Viewport(0, 0, (uint)width, (uint)height);
            _rc.Disable(_e.DepthTest);
            _rc.DepthMask(false);
            _rc.Disable(_e.Blend);
            _volumetric.Use();
            _rc.ActiveTexture(_e.Texture0);
            _rc.BindTexture(_e.Texture2D, colorTex);
            _volumetric.SetUniform("uColor", 0);
            _rc.ActiveTexture(_e.Texture0 + 1);
            _rc.BindTexture(_e.Texture2D, depthIsTexture ? depthTex : 0);
            _volumetric.SetUniform("uDepth", 1);
            _rc.ActiveTexture(_e.Texture0 + 2);
            _rc.BindTexture(_e.Texture2D, frame.ShadowAtlas);
            _volumetric.SetUniform("uShadowAtlas", 2);
            _volumetric.SetMatrix4("uInvView", invView);
            _volumetric.SetMatrix4("uInvProjection", invProj);
            for (int i = 0; i < LightingFrame.MaxCascades; i++)
                _volumetric.SetMatrix4($"uCascadeVP[{i}]", frame.CascadeVP[i]);
            _volumetric.SetUniform("uCascadeSplits", frame.CascadeSplits.X, frame.CascadeSplits.Y, frame.CascadeSplits.Z, frame.CascadeSplits.W);
            _volumetric.SetUniform("uCascadeCount", frame.CascadeCount);
            _volumetric.SetUniform("uLightDir", frame.Sun.Direction.X, frame.Sun.Direction.Y, frame.Sun.Direction.Z);
            _volumetric.SetUniform("uLightColor", frame.Sun.Color.X, frame.Sun.Color.Y, frame.Sun.Color.Z);
            _volumetric.SetUniform("uLightIntensity", frame.Sun.Intensity);
            _volumetric.SetUniform("uFogColor", frame.Fog.Color.X, frame.Fog.Color.Y, frame.Fog.Color.Z);
            _volumetric.SetUniform("uFogDensity", frame.Fog.Density);
            _volumetric.SetUniform("uFogHeight", frame.Fog.Height);
            _volumetric.SetUniform("uFogHeightFalloff", frame.Fog.HeightFalloff);
            _volumetric.SetUniform("uIntensity", frame.Fog.VolumetricIntensity);
            _volumetric.SetUniform("uSteps", frame.Fog.RaySteps);
            _volumetric.SetUniform("uInvResolution", 1f / width, 1f / height);
            _volumetric.SetUniform("uHasDepth", depthIsTexture ? 1 : 0);
            _rc.BindVertexArray(_emptyVao);
            _rc.DrawArrays(_e.Triangles, 0, 3);
            _rc.ActiveTexture(_e.Texture0);
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
                _rc.DeleteVertexArray(_emptyVao);
                _emptyVao = 0;
            }
            if (_resolveFbo != 0)
            {
                uint fbo = _resolveFbo;
                _rc.DeleteFramebuffers(1, &fbo);
                _resolveFbo = 0;
            }
            if (_resolveColor != 0)
            {
                _rc.DeleteTexture(_resolveColor);
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
                _rc.DeleteFramebuffers(1, &fbo);
                _resolveFbo = 0;
            }
            if (_resolveColor != 0)
            {
                _rc.DeleteTexture(_resolveColor);
                _resolveColor = 0;
            }
            _width = width;
            _height = height;
            _rc.GenTextures(1, out _resolveColor);
            _rc.BindTexture(_e.Texture2D, _resolveColor);
            _rc.TexImage2D(_e.Texture2D, 0, _e.InternalRgba, (uint)width, (uint)height, 0, _e.PixelRgba, _e.UnsignedByte, null);
            _rc.TexParameter(_e.Texture2D, _e.TextureMinFilter, _e.Linear);
            _rc.TexParameter(_e.Texture2D, _e.TextureMagFilter, _e.Linear);
            _rc.TexParameter(_e.Texture2D, _e.TextureWrapS, _e.ClampToEdge);
            _rc.TexParameter(_e.Texture2D, _e.TextureWrapT, _e.ClampToEdge);
            _rc.GenFramebuffers(1, out _resolveFbo);
            _rc.BindFramebuffer(_e.Framebuffer, _resolveFbo);
            _rc.FramebufferTexture2D(_e.Framebuffer, _e.ColorAttachment0, _e.Texture2D, _resolveColor, 0);
            _rc.DrawBuffer(_e.ColorAttachment0);
        }
    }
}
