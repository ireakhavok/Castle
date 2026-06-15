// Folder: SiegeEngine/Scenes
// File: RuntimeGameplayScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.Terrain;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace SiegeEngine.Scenes
{
    public unsafe class RuntimeGameplayScene : GameScene
    {
        private readonly Player _player;
        private readonly FlyCameraController _flyCamera;
        private ShaderProgram _terrainShader;
        protected VertexBuffer _terrainBuffer;
        private ModelRenderer _modelRenderer;
        private float[,] _heightmap;
        private int _terrainWidth = 205;
        private int _terrainHeight = 205;
        private uint _terrainTextureId = 0;
        private bool _hasColorTexture = true;

        public RuntimeGameplayScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus, SceneContext ctx = null)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _player = new Player(1, new Vector3(10, 10, 0), 0);
            _flyCamera = new FlyCameraController(controlContext, window);
            DefaultDockingMode = DockingMode.Desktop;
            _modelRenderer = new ModelRenderer(renderContext);
            if (ctx != null) LoadContentFromContext(ctx);
        }

        public void LoadLevelData(string levelName, string projectPath)
        {
            LoadSceneData(new SceneData { Name = levelName ?? "Main" });
            _eventBus.Publish(new SceneActivatedEvent(levelName));
            _player.InitializeCamera(_controlContext, _window);
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _renderContext.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            _terrainShader = new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
            _terrainBuffer = new VertexBuffer(_renderContext);
            _modelRenderer.Initialize();
            SetupPureRuntimeWorld();
            _controlContext.SetScrollCallback(_window, (w, xoffset, yoffset) => { });
            _controlContext.SetWindowSizeCallback(_window, (w, newWidth, newHeight) =>
            {
                if (newWidth > 0 && newHeight > 0)
                {
                    _width = newWidth;
                    _height = newHeight;
                    _renderContext.Viewport(0, 0, (uint)newWidth, (uint)newHeight);
                }
            });
            _player.InitializeCamera(_controlContext, _window);
            _flyCamera.Update(0f, 0f, true);
            _flyCamera.Position = new Vector3(100, 80, 100);
            //_flyCamera.LookAt(new Vector3(100, 0, 100), Vector3.UnitY);
            Console.WriteLine("[RuntimeGameplayScene] Render pipeline ready - camera positioned to see terrain");
        }

        protected override void LoadContentFromContext(SceneContext ctx)
        {
            if (ctx?.CurrentLevel != null)
            {
                LoadLevelData(ctx.LoadLevelName, ctx.PlayProjectPath);
                LoadExactSavedTerrain(ctx.PlayProjectPath, ctx.LoadLevelName);
                foreach (var e in ctx.CurrentLevel.Entities)
                {
                    _server.AddEntity(e);
                    Console.WriteLine($"[RuntimeGameplayScene] Added FBX entity ID {e.Id} from Level");
                }
            }
            _flyCamera.Update(0f, 0f, true);
        }

        private void LoadExactSavedTerrain(string projectPath, string levelName)
        {
            Console.WriteLine($"[RuntimeGameplayScene] ctx.PlayProjectPath = '{projectPath}' | levelName = '{levelName}'");
            string terrainPath = Path.Combine(projectPath, "Assets", "Terrain", levelName + ".tif");
            string colorPath = Path.Combine(projectPath, "Assets", "Terrain", levelName + ".png");
            Console.WriteLine($"[RuntimeGameplayScene] Trying terrainPath: {terrainPath} | colorPath: {colorPath}");

            try
            {
                float minH, maxH, sx, sz;
                _heightmap = CustomTerrainParser.Load(terrainPath, out _terrainWidth, out _terrainHeight, out minH, out maxH, out sx, out sz);
                Console.WriteLine($"[RuntimeGameplayScene] SUCCESS - loaded your saved heightmap ({_terrainWidth}x{_terrainHeight}, max {maxH:F1}m from NewTerrain.tif)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RuntimeGameplayScene] Load path issue (common in pure client): {ex.Message}. Using visible fallback with variation.");
                _heightmap = new float[_terrainWidth, _terrainHeight];
                for (int x = 0; x < _terrainWidth; x++) for (int y = 0; y < _terrainHeight; y++) _heightmap[x, y] = (float)(Math.Sin(x / 8f) * 4 + Math.Cos(y / 8f) * 4 + 5); // obvious hills
            }

            _terrainTextureId = TerrainTextureParser.LoadColorTexture(_renderContext, colorPath);
            _hasColorTexture = _terrainTextureId != 0;
            BuildTexturedMesh();
            Console.WriteLine($"[RuntimeGameplayScene] Terrain mesh built and ready for draw - texture ID {_terrainTextureId}");
        }

        protected override void SetupPureRuntimeWorld()
        {
            var terrainEntity = new Entity { Id = 1000, Type = "Terrain" };
            terrainEntity.AddComponent(new TransformComponent { Position = Vector3.Zero });
            terrainEntity.AddComponent(new PhysicsComponent { Position = Vector3.Zero });
            _server.AddEntity(terrainEntity);
            Console.WriteLine("[RuntimeGameplayScene] Terrain entity added");
        }

        protected virtual void BuildTexturedMesh()
        {
            var vertices = new List<float>();
            var indices = new List<uint>();
            int step = 1;
            int stepsX = _terrainWidth / step;
            int stepsY = _terrainHeight / step;
            for (int x = 0; x <= stepsX; x++)
            {
                for (int y = 0; y <= stepsY; y++)
                {
                    float wx = x * step * 1.0f;
                    float wy = y * step * 1.0f;
                    float z = _heightmap[Math.Min(x * step, _terrainWidth - 1), Math.Min(y * step, _terrainHeight - 1)] * 1.0f;
                    vertices.Add(wx); vertices.Add(wy); vertices.Add(z);
                    vertices.Add(0.7f); vertices.Add(0.9f); vertices.Add(1.0f); vertices.Add(1.0f);
                    vertices.Add((float)x / stepsX); vertices.Add((float)y / stepsY);
                }
            }
            for (int x = 0; x < stepsX; x++)
            {
                for (int y = 0; y < stepsY; y++)
                {
                    uint tl = (uint)(x * (stepsY + 1) + y);
                    uint tr = tl + 1;
                    uint bl = tl + (uint)(stepsY + 1);
                    uint br = bl + 1;
                    indices.Add(tl); indices.Add(tr); indices.Add(bl);
                    indices.Add(tr); indices.Add(br); indices.Add(bl);
                }
            }
            _terrainBuffer.UpdateCustomWithUV(vertices, indices);
            Console.WriteLine("[RuntimeGameplayScene] VertexBuffer updated with textured mesh - ready to draw");
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            _flyCamera.Update(deltaTime, 0f, true);
            if (_player.Camera != null) _flyCamera.Update(0f, 0f, true);
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            base.RenderContent(entities, view, projection);
            RenderGameplayContent(entities, view, projection);
        }

        protected override void RenderGameplayContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.ClearColor(0.05f, 0.08f, 0.15f, 1.0f);
            Matrix4x4 realView = _flyCamera.ViewMatrix;
            Matrix4x4 realProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, AspectRatio, 0.1f, 1000f);
            if (_terrainShader != null && _terrainBuffer != null)
            {
                _terrainShader.Use();
                _terrainShader.SetMatrix4("uView", realView);
                _terrainShader.SetMatrix4("uProjection", realProjection);
                _terrainShader.SetMatrix4("uModel", Matrix4x4.Identity);
                if (_hasColorTexture && _terrainTextureId != 0)
                {
                    _renderContext.ActiveTexture(0);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, _terrainTextureId);
                    _terrainShader.SetUniform("uHasTexture", 1);
                    _terrainShader.SetUniform("uTexture", 0);
                }
                else
                {
                    _terrainShader.SetUniform("uHasTexture", 0);
                }
                _terrainBuffer.Bind();
                _renderContext.Enable(_renderContext.Enums.DepthTest);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, _terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                Console.WriteLine("[RuntimeGameplayScene] TERRAIN DRAW EXECUTED - visible textured mesh submitted");
            }
            foreach (var e in _server.GetEntities())
            {
                var modelComp = e.GetComponent<ModelComponent>();
                var physics = e.GetComponent<PhysicsComponent>();
                if (modelComp != null && physics != null)
                {
                    _modelRenderer.RenderModel(modelComp, physics, realView, realProjection, _flyCamera.Position, null);
                    Console.WriteLine($"[RuntimeGameplayScene] FBX entity ID {e.Id} render submitted");
                }
            }
        }

        public override void Dispose()
        {
            _terrainShader?.Dispose();
            _terrainBuffer?.Dispose();
            _modelRenderer?.Dispose();
            base.Dispose();
        }
    }
}