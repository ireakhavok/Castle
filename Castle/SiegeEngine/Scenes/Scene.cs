// Folder: SiegeEngine/Scenes
// File: Scene.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.Rendering.Renderers;
using SiegeEngine.Core.Rendering.Shaders;
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

        public DockingMode DefaultDockingMode { get; protected set; } = DockingMode.Desktop;

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

        public virtual void Initialize(int width, int height)
        {
            _width = width;
            _height = height;
            _aspectRatio = width > 0 && height > 0 ? (float)width / height : 16f / 9f;
            _renderContext.Viewport(0, 0, (uint)width, (uint)height);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            _modelRenderer.Initialize();
        }

        public virtual void Resize(int width, int height)
        {
            _width = width;
            _height = height;
            _aspectRatio = width > 0 && height > 0 ? (float)width / height : 16f / 9f;
            _renderContext.Viewport(0, 0, (uint)width, (uint)height);
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
            if (_disposed) return;
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            Matrix4x4 view = _player?.Camera?.ViewMatrix ?? Matrix4x4.Identity;
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, AspectRatio, 0.1f, 1000f);
            RenderContent(entities, view, projection);
        }

        protected virtual void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
        }

        public virtual void AddSystem(GameSystem system)
        {
            _systems.Add(system);
        }

        public virtual void Dispose()
        {
            if (_disposed) return;
            _modelRenderer?.Dispose();
            _disposed = true;
        }
    }
}