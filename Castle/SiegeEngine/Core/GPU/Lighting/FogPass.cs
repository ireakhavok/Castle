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
        private GpuHandle _pipeline;
        private GpuHandle _resolve;
        private int _width;
        private int _height;
        private bool _disposed;

        public FogPass(IRenderContext renderContext)
        {
            _rc = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _e = _rc.Enums;
            _pipeline = _rc.CreatePipeline(ShaderCatalog.Describe(ShaderId.Fog, _rc));
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

            _rc.BindRenderTarget(_resolve);
            _rc.Viewport(0, 0, (uint)width, (uint)height);
            _rc.Disable(_e.DepthTest);
            _rc.DepthMask(false);
            _rc.Disable(_e.Blend);

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

            _rc.BindPipeline(_pipeline);
            _rc.BindTextureSlot(0, _rc.ImportTexture(colorTex, _e.Texture2D), "Color");
            _rc.BindTextureSlot(1, depthIsTexture ? _rc.ImportTexture(depthTex, _e.Texture2D) : default, "uDepth");
            _rc.BindTextureSlot(2, frame.ShadowAtlas != 0 ? _rc.ImportTexture(frame.ShadowAtlas, _e.Texture2D) : default, "uShadowAtlas");
            _rc.Disable(_e.DepthTest);
            _rc.DepthMask(false);
            _rc.Disable(_e.CullFace);
            _rc.ColorMask(true, true, true, true);
            _rc.DrawFullscreen();
        }

        public uint ResolveColor => _resolve.Id;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_resolve.IsValid)
            {
                _rc.Destroy(_resolve);
                _resolve = default;
            }
            if (_pipeline.IsValid)
            {
                _rc.Destroy(_pipeline);
                _pipeline = default;
            }
        }

        private void EnsureTarget(int width, int height)
        {
            if (_resolve.IsValid && _width == width && _height == height)
                return;
            if (_resolve.IsValid)
                _rc.Destroy(_resolve);
            _width = width;
            _height = height;
            _resolve = _rc.CreateRenderTarget(new RenderTargetDesc
            {
                Width = width,
                Height = height,
                ColorFormat = _e.InternalRgba
            });
            _rc.SetTextureParams(_resolve, _e.Linear, _e.Linear, _e.ClampToEdge, _e.ClampToEdge);
        }
    }
}
