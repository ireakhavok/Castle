// Folder: SiegeEngine/Scenes
// File: Scene.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Lighting;
using SiegeEngine.Core.GPU.PostProcess;
using SiegeEngine.Core.GPU.Renderers;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Scenes
{
    public abstract class Scene : IScene, IDisposable
    {
        protected readonly IRenderContext _renderContext;
        protected readonly IControlContext _controlContext;
        protected readonly IntPtr _window;
        protected readonly IGameServer _server;
        protected readonly EventBus _eventBus;
        protected int _width;
        protected int _height;
        protected float _aspectRatio = 16f / 9f;
        protected bool _disposed;
        protected readonly List<GameSystem> _systems = new List<GameSystem>();
        protected Player _player;
        protected ModelRenderer _modelRenderer;
        private AntiAliasingPass _aaPass;
        private AntiAliasingMode _aaLastMode = AntiAliasingMode.Off;
        private ShadowMapRenderer _shadowMapRenderer;
        private FogPass _fogPass;

        public DockingMode DefaultDockingMode { get; protected set; } = DockingMode.Desktop;
        public bool OwnsFramebuffer { get; protected set; } = true;

        public void SetOwnsFramebuffer(bool owns)
        {
            OwnsFramebuffer = owns;
        }

        public Scene(IRenderContext renderContext, IControlContext controlContext, IntPtr window, IGameServer server, EventBus eventBus)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _controlContext = controlContext ?? throw new ArgumentNullException(nameof(controlContext));
            _window = window;
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _eventBus = eventBus;
            _modelRenderer = new ModelRenderer(_renderContext);
        }

        public IReadOnlyList<Entity> GetEntities() => _server.GetEntities();
        public void SetPlayer(Player player) => _player = player;

        protected float AspectRatio => _aspectRatio;

        protected virtual Vector4 FrameClearColor
        {
            get
            {
                // Past-the-map pixels used to show the dark clear color as a
                // hard black fog wall. When fog is on, the backdrop IS the fog.
                // GpuFogState is a struct — do not use Current?.Fog (GpuFogState?).
                LightingFrame frame = LightingFrame.Current;
                if (frame != null)
                {
                    GpuFogState fog = frame.Fog;
                    if (fog.Mode != FogMode.Off && fog.Quality != FogQuality.Off)
                        return new Vector4(fog.Color, 1f);
                }
                return new Vector4(0.35f, 0.35f, 0.35f, 1f);
            }
        }

        protected virtual EnvironmentSettings GetEnvironmentSettings() => null;

        /// <summary>
        /// Play / runtime only. Editor scenes override this to false so a
        /// placed Light entity or the Post Process sun toggle is the only
        /// source of directional lighting.
        /// </summary>
        protected virtual bool AllowRuntimeDefaultSun => true;

        public virtual void Initialize(int width, int height)
        {
            _width = width;
            _height = height;
            _aspectRatio = width > 0 && height > 0 ? (float)width / height : 16f / 9f;
            if (OwnsFramebuffer)
            {
                _renderContext.Viewport(0, 0, (uint)width, (uint)height);
                Vector4 c = FrameClearColor;
                _renderContext.ClearColor(c.X, c.Y, c.Z, c.W);
            }
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _modelRenderer.Initialize();
        }

        public virtual void Resize(int width, int height)
        {
            _width = width;
            _height = height;
            _aspectRatio = width > 0 && height > 0 ? (float)width / height : 16f / 9f;
            if (OwnsFramebuffer)
                _renderContext.Viewport(0, 0, (uint)width, (uint)height);
            _aaPass?.DiscardHistory();
        }

        public virtual void Update(float deltaTime)
        {
            foreach (var system in _systems)
            {
                if (system is AudioSystem audioSystem && _player?.Camera != null)
                {
                    audioSystem.SetListenerPosition(_player.Camera.Position);
                }
                system.Update(deltaTime);
            }
            _server.Update(deltaTime);
        }

        public virtual void Render(IReadOnlyList<Entity> entities)
        {
            RenderPresentRoot(entities, presentRoot: true);
        }

        public void RenderWorldOnly(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            // Parent RenderPresentRoot already packed LightingFrame.
            // Rebuild only when nothing is packed yet. ShadowsReady stays
            // on that frame so we do not double-draw the atlas.
            if (LightingFrame.Current == null)
                PrepareLightingFrame(entities, view, projection, runShadows: true);
            RenderContent(entities, view, projection);
        }

        public void RenderOverlaysOnly(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            RenderOverlay(entities, view, projection);
        }

        public void GetCameraViewProjection(out Matrix4x4 view, out Matrix4x4 projection)
        {
            GetViewProjection(out view, out projection);
        }

        protected void RenderPresentRoot(IReadOnlyList<Entity> entities, bool presentRoot)
        {
            if (_disposed) return;

            GetViewProjection(out Matrix4x4 view, out Matrix4x4 projection);
            LightingFrame frame = PrepareLightingFrame(entities, view, projection, runShadows: presentRoot);

            bool wrapped = false;
            AntiAliasingMode aaMode = AntiAliasingSettings.Resolve();
            bool volumetric = frame.Fog.Mode == FogMode.Volumetric && frame.Fog.Quality != FogQuality.Off;
            if (presentRoot && (aaMode != AntiAliasingMode.Off || volumetric))
            {
                if (_aaPass == null)
                    _aaPass = new AntiAliasingPass(_renderContext);
                if (_aaLastMode != aaMode)
                    _aaPass.DiscardHistory();
                _aaLastMode = aaMode;
                AntiAliasingMode wrapMode = aaMode != AntiAliasingMode.Off ? aaMode : AntiAliasingMode.FXAA;
                wrapped = _aaPass.BeginWorld(wrapMode, _width, _height, FrameClearColor);
            }
            else if (presentRoot)
            {
                _aaLastMode = AntiAliasingMode.Off;
            }

            if (!wrapped && OwnsFramebuffer)
            {
                Vector4 c = FrameClearColor;
                _renderContext.ClearColor(c.X, c.Y, c.Z, c.W);
                _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            }

            RenderContent(entities, view, projection);

            if (wrapped && frame.Fog.Mode == FogMode.Volumetric && frame.Fog.Quality != FogQuality.Off && _aaPass.WorldColor != 0)
            {
                if (_fogPass == null)
                    _fogPass = new FogPass(_renderContext);
                _fogPass.Apply(frame, view, projection, _aaPass.WorldColor, _aaPass.WorldDepth, _aaPass.WorldDepthIsTexture, _aaPass.TargetWidth, _aaPass.TargetHeight);
                if (_fogPass.ResolveColor != 0)
                    _aaPass.ReplaceWorldColor(_fogPass.ResolveColor);
            }

            if (wrapped)
                _aaPass.Resolve(mode: _aaLastMode, view, projection);

            if (presentRoot)
                RenderOverlay(entities, view, projection);
        }

        protected LightingFrame PrepareLightingFrame(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection, bool runShadows)
        {
            EnvironmentSettings environment = GetEnvironmentSettings();
            AntiAliasingSettings.BindAuthored(environment);
            LightingSettings.BindAuthored(environment);

            // Always prefer the scene server. Play Game and custom scenes
            // sometimes pass Array.Empty even when _server already has the
            // rehydrated models and lights.
            IReadOnlyList<Entity> list = _server?.GetEntities();
            if (list == null || list.Count == 0)
                list = entities;
            LightingFrame frame = LightingFrame.Build(list, environment, LightingFrame.DefaultSunDirection, AllowRuntimeDefaultSun);
            LightingFrame.Current = frame;

            bool wantSunShadows = runShadows && frame.ShadowQuality != ShadowQuality.Off && frame.Sun.CastShadows && frame.Sun.Intensity > 0.001f;
            bool wantLocalShadows = runShadows && frame.ShadowQuality != ShadowQuality.Off &&
                ((frame.PointCount > 0 && frame.Points[0].CastShadows) || (frame.SpotCount > 0 && frame.Spots[0].CastShadows));
            if (wantSunShadows || wantLocalShadows)
            {
                if (_shadowMapRenderer == null)
                    _shadowMapRenderer = new ShadowMapRenderer(_renderContext);
                Vector3 cameraPos = ExtractCameraPosition(view);
                if (_player?.Camera != null)
                    cameraPos = _player.Camera.Position;
                var casters = CollectShadowCasters(list);
                _shadowMapRenderer.Render(frame, casters, view, projection, cameraPos);
            }

            return frame;
        }

        protected virtual List<ShadowCaster> CollectShadowCasters(IReadOnlyList<Entity> entities)
        {
            return ShadowMapRenderer.CollectCasters(entities);
        }

        private static Vector3 ExtractCameraPosition(Matrix4x4 view)
        {
            if (Matrix4x4.Invert(view, out Matrix4x4 inv))
                return inv.Translation;
            return Vector3.Zero;
        }

        protected virtual void GetViewProjection(out Matrix4x4 view, out Matrix4x4 projection)
        {
            view = _player?.Camera?.ViewMatrix ?? Matrix4x4.Identity;
            projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, AspectRatio, 0.1f, 20000f);
        }

        protected virtual void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
        }

        protected virtual void RenderOverlay(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
        }

        public virtual void AddSystem(GameSystem system)
        {
            _systems.Add(system);
        }

        public virtual void Dispose()
        {
            if (_disposed) return;
            _aaPass?.Dispose();
            _aaPass = null;
            _shadowMapRenderer?.Dispose();
            _shadowMapRenderer = null;
            _fogPass?.Dispose();
            _fogPass = null;
            LightingFrame.Current = null;
            _modelRenderer?.Dispose();
            _disposed = true;
        }
    }
}
