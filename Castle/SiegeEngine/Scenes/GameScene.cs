// Folder: SiegeEngine/Scenes
// File: GameScene.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Renderers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Scenes
{
    public abstract class GameScene : Scene
    {
        protected SceneData _sceneData;
        protected SkyboxRenderer _skyboxRenderer;
        protected SkyboxData _skyboxData;
        protected TerrainRenderer _terrainRenderer;
        protected VertexBuffer _terrainBuffer;
        protected VertexBuffer _wireframeBuffer;
        protected float[,] _heightmap;
        protected int _terrainWidth = 200;
        protected int _terrainHeight = 200;
        protected uint _terrainTextureId;
        protected bool _hasColorTexture;
        protected bool _terrainWireframe;

        public string SceneName => _sceneData?.Name ?? GetType().Name;

        public GameScene(IRenderContext renderContext, IControlContext controlContext, IntPtr window, IGameServer server, EventBus eventBus, SceneData sceneData = null)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _sceneData = sceneData;
            _skyboxData = sceneData?.Skybox;
            _terrainRenderer = new TerrainRenderer(renderContext);
        }

        public virtual void LoadSceneData(SceneData data)
        {
            _sceneData = data;
            if (data?.Skybox != null)
                _skyboxData = data.Skybox;
        }

        protected override EnvironmentSettings GetEnvironmentSettings() => _sceneData?.Environment;

        protected virtual void LoadContentFromContext(SceneContext ctx)
        {
            if (ctx?.SceneData != null)
                LoadSceneData(ctx.SceneData);
        }

        protected virtual void SetupPureRuntimeWorld()
        {
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _terrainRenderer?.Initialize();
            if (_terrainBuffer == null)
                _terrainBuffer = new VertexBuffer(_renderContext);
            if (_wireframeBuffer == null)
                _wireframeBuffer = new VertexBuffer(_renderContext);
        }

        protected void EnsureSkyboxRenderer()
        {
            if (_skyboxRenderer != null) return;
            _skyboxRenderer = new SkyboxRenderer(_renderContext);
            _skyboxRenderer.Initialize();
        }

        protected virtual Vector3 GetViewPosition()
        {
            return _player?.Camera?.Position ?? Vector3.Zero;
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            RenderGameplayContent(entities, view, projection);
        }

        protected virtual void RenderGameplayContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            RenderSkybox(view, projection);
            RenderTerrain(view, projection);
            RenderEntities(entities, view, projection);
        }

        protected virtual void RenderSkybox(Matrix4x4 view, Matrix4x4 projection)
        {
            SkyboxData data = _skyboxData ?? _sceneData?.Skybox;
            if (_skyboxRenderer != null && data != null && data.Enabled)
                _skyboxRenderer.RenderSkybox(data, view, projection);
        }

        protected virtual void RenderTerrain(Matrix4x4 view, Matrix4x4 projection)
        {
            if (_terrainRenderer == null || _terrainBuffer == null || _heightmap == null)
                return;
            _terrainRenderer.RenderTerrain(view, projection, _hasColorTexture, _terrainTextureId, _terrainBuffer, _wireframeBuffer, _heightmap, _terrainWireframe);
        }

        protected virtual void RenderEntities(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            if (_modelRenderer == null) return;
            IReadOnlyList<Entity> list = entities;
            if (list == null || list.Count == 0)
                list = _server?.GetEntities();
            if (list == null || list.Count == 0) return;
            Vector3 viewPos = GetViewPosition();
            foreach (var entity in list)
            {
                var modelComp = entity.GetComponent<ModelComponent>();
                var physics = entity.GetComponent<PhysicsComponent>();
                if (modelComp != null && physics != null && !string.IsNullOrEmpty(modelComp.Key))
                    _modelRenderer.RenderEntityFully(modelComp, physics, view, projection, viewPos);
            }
        }

        protected override void RenderOverlay(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
        }

        public override void Dispose()
        {
            TextureLoader.DeleteTexture(_renderContext, ref _terrainTextureId);
            _skyboxRenderer?.Dispose();
            _skyboxRenderer = null;
            _terrainRenderer?.Dispose();
            _terrainRenderer = null;
            _terrainBuffer?.Dispose();
            _terrainBuffer = null;
            _wireframeBuffer?.Dispose();
            _wireframeBuffer = null;
            base.Dispose();
        }
    }
}
