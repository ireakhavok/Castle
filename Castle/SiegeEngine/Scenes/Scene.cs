// Folder: SiegeEngine/Scenes
// File: Scene.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.GPU.ContextManagement;
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

        protected virtual Vector4 FrameClearColor => new Vector4(0.1f, 0.1f, 0.1f, 1f);

        protected virtual EnvironmentSettings GetEnvironmentSettings() => null;

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
            // Drive systems registered on the IGameServer (AnimationSystem, ClientPredictionSystem, etc.)
            _server.Update(deltaTime);
        }

        public virtual void Render(IReadOnlyList<Entity> entities)
        {
            RenderPresentRoot(entities, presentRoot: true);
        }

        public void RenderWorldOnly(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
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
            AntiAliasingSettings.BindAuthored(GetEnvironmentSettings());

            bool wrapped = false;
            if (presentRoot)
            {
                AntiAliasingMode mode = AntiAliasingSettings.Resolve();
                if (mode != AntiAliasingMode.Off)
                {
                    if (_aaPass == null)
                        _aaPass = new AntiAliasingPass(_renderContext);
                    if (_aaLastMode != mode)
                        _aaPass.DiscardHistory();
                    _aaLastMode = mode;
                    wrapped = _aaPass.BeginWorld(mode, _width, _height, FrameClearColor);
                }
                else
                {
                    _aaLastMode = AntiAliasingMode.Off;
                }
            }

            if (!wrapped && OwnsFramebuffer)
            {
                Vector4 c = FrameClearColor;
                _renderContext.ClearColor(c.X, c.Y, c.Z, c.W);
                _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            }

            RenderContent(entities, view, projection);

            if (wrapped)
                _aaPass.Resolve(mode: _aaLastMode, view, projection);

            if (presentRoot)
                RenderOverlay(entities, view, projection);
        }

        protected virtual void GetViewProjection(out Matrix4x4 view, out Matrix4x4 projection)
        {
            view = _player?.Camera?.ViewMatrix ?? Matrix4x4.Identity;
            projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, AspectRatio, 0.1f, 1000f);
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
            _modelRenderer?.Dispose();
            _disposed = true;
        }
    }
}