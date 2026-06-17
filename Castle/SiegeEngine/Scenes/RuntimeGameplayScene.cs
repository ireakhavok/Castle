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
using System.Linq;
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
        private bool _contentLoaded = false;
        private bool _firstFrame = true;

        public RuntimeGameplayScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus, SceneContext ctx = null)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _player = new Player(1, new Vector3(10, 10, 0), 0);
            _flyCamera = new FlyCameraController(controlContext, window);
            DefaultDockingMode = DockingMode.Desktop;
            _modelRenderer = new ModelRenderer(renderContext);
            _heightmap = new float[_terrainWidth, _terrainHeight];
            for (int x = 0; x < _terrainWidth; x++) for (int y = 0; y < _terrainHeight; y++) _heightmap[x, y] = 5f + (float)Math.Sin(x * 0.1f + y * 0.1f) * 3f;

            // FIXED: Parse command line from MenuCommands.PlayGame so project path is NEVER missed
            string projectPath = "";
            string levelName = "NewTerrain";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--play-project") projectPath = args[i + 1].Trim('"');
                if (args[i] == "--load-level") levelName = args[i + 1].Trim('"');
            }
            if (!string.IsNullOrEmpty(projectPath))
            {
                Console.WriteLine($"[RuntimeGameplayScene] ✅ MenuCommands command-line parsed → Project: {projectPath} | Level: {levelName}");
                ctx = ctx ?? new SceneContext { PlayProjectPath = projectPath, LoadLevelName = levelName };
            }

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
            _renderContext.ClearColor(0.05f, 0.08f, 0.15f, 1.0f);
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
            ForceVisibleOverheadCamera();
            BuildTexturedMesh();
        }

        private void ForceVisibleOverheadCamera()
        {
            float centerX = _terrainWidth * 0.5f;
            float centerY = _terrainHeight * 0.5f;
            _flyCamera.Position = new Vector3(centerX, centerY, 300f);
            _flyCamera.Yaw = 0f;
            _flyCamera.Pitch = -89f;
            _flyCamera.Update(0f, 0f, true);
            _flyCamera.RefreshViewMatrix();
        }

        protected override void LoadContentFromContext(SceneContext ctx)
        {
            if (_contentLoaded) return;
            _contentLoaded = true;
            string projectPath = ctx?.PlayProjectPath ?? "";
            string levelName = ctx?.LoadLevelName ?? "NewTerrain";
            LoadLevelData(levelName, projectPath);
            LoadExactSavedTerrain(projectPath, levelName);
            if (ctx?.CurrentLevel != null)
            {
                foreach (var e in ctx.CurrentLevel.Entities)
                    _server.AddEntity(e);
            }
            else
            {
                for (int i = 1; i <= 2; i++)
                {
                    var dummy = new Entity { Id = i, Type = "FBX" };
                    dummy.AddComponent(new PhysicsComponent { Position = i == 1 ? new Vector3(56.5f, 51.7f, 0.15f) : new Vector3(59.2f, 45.4f, 0.1f) });
                    _server.AddEntity(dummy);
                }
            }
            ForceVisibleOverheadCamera();
            _flyCamera.Update(0f, 0f, true);
        }

        private void LoadExactSavedTerrain(string projectPath, string levelName)
        {
            string terrainPath = !string.IsNullOrEmpty(projectPath)
                ? Path.Combine(projectPath, "Assets", "Terrain", levelName + ".tif")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terrain", levelName + ".tif");

            string colorPath = !string.IsNullOrEmpty(projectPath)
                ? Path.Combine(projectPath, "Assets", "Terrain", levelName + ".png")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terrain", levelName + ".png");

            Console.WriteLine($"[RuntimeGameplayScene] Final resolved from MenuCommands: Terrain={terrainPath} | PNG={colorPath}");

            try
            {
                float minH, maxH, sx, sz;
                _heightmap = CustomTerrainParser.Load(terrainPath, out _terrainWidth, out _terrainHeight, out minH, out maxH, out sx, out sz);
                Console.WriteLine($"[RuntimeGameplayScene] ✅ SUCCESS: Loaded real heightmap ({_terrainWidth}x{_terrainHeight})");
                _terrainTextureId = TerrainTextureParser.LoadColorTexture(_renderContext, colorPath);
                _hasColorTexture = _terrainTextureId != 0;
                if (_hasColorTexture) Console.WriteLine($"[RuntimeGameplayScene] ✅ SUCCESS: Loaded PNG texture");
                BuildTexturedMesh();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RuntimeGameplayScene] ❌ Load failed: {ex.Message} - path was {terrainPath}");
                _terrainWidth = 205;
                _terrainHeight = 205;
                _heightmap = new float[_terrainWidth, _terrainHeight];
                for (int x = 0; x < _terrainWidth; x++)
                    for (int y = 0; y < _terrainHeight; y++)
                        _heightmap[x, y] = (float)(Math.Sin(x / 8f) * 4 + Math.Cos(y / 8f) * 4 + 5);
            }
            _terrainTextureId = TerrainTextureParser.LoadColorTexture(_renderContext, colorPath);
            _hasColorTexture = _terrainTextureId != 0;
            BuildTexturedMesh();
        }

        protected override void SetupPureRuntimeWorld()
        {
            var terrainEntity = new Entity { Id = 1000, Type = "Terrain" };
            terrainEntity.AddComponent(new TransformComponent { Position = Vector3.Zero });
            terrainEntity.AddComponent(new PhysicsComponent { Position = Vector3.Zero });
            _server.AddEntity(terrainEntity);
        }

        protected virtual void BuildTexturedMesh()
        {
            if (_heightmap == null)
            {
                _terrainWidth = 205;
                _terrainHeight = 205;
                _heightmap = new float[_terrainWidth, _terrainHeight];
                for (int x = 0; x < _terrainWidth; x++)
                    for (int y = 0; y < _terrainHeight; y++)
                        _heightmap[x, y] = (float)(Math.Sin(x / 8f) * 4 + Math.Cos(y / 8f) * 4 + 5);
            }
            if (_terrainBuffer == null)
            {
                _terrainBuffer = new VertexBuffer(_renderContext);
            }
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
        }

        public override void Render(IReadOnlyList<Entity> entities)
        {
            RenderContent(entities, _flyCamera.ViewMatrix, Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, AspectRatio, 0.1f, 1000f));
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            _flyCamera.Update(deltaTime, 0f, true);
            if (_player.Camera != null) _flyCamera.Update(0f, 0f, true);
            if (_firstFrame)
            {
                _firstFrame = false;
                ForceVisibleOverheadCamera();
            }
        }

        protected override void RenderGameplayContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.ClearColor(0.05f, 0.08f, 0.15f, 1.0f);

            if (_terrainBuffer != null)
            {
                _terrainBuffer.Bind();
                uint stride = 9 * sizeof(float);
                _renderContext.EnableVertexAttribArray(0);
                _renderContext.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false, stride, (void*)0);
                _renderContext.EnableVertexAttribArray(1);
                _renderContext.VertexAttribPointer(1, 4, _renderContext.Enums.Float, false, stride, (void*)(3 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(2);
                _renderContext.VertexAttribPointer(2, 2, _renderContext.Enums.Float, false, stride, (void*)(7 * sizeof(float)));
            }

            if (_terrainShader != null && _terrainBuffer != null)
            {
                _terrainShader.Use();
                _terrainShader.SetMatrix4("uView", _flyCamera.ViewMatrix);
                _terrainShader.SetMatrix4("uProjection", projection);
                _terrainShader.SetMatrix4("uModel", Matrix4x4.Identity);
                if (_hasColorTexture && _terrainTextureId != 0)
                {
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, _terrainTextureId);
                    _terrainShader.SetUniform("uHasTexture", 1);
                    _terrainShader.SetUniform("uTexture", 0);
                }
                else
                {
                    _terrainShader.SetUniform("uHasTexture", 0);
                }
                uint idxCount = _terrainBuffer.GetIndexCount();
                _renderContext.DrawElements(_renderContext.Enums.Triangles, idxCount, _renderContext.Enums.UnsignedInt, null);
            }

            foreach (var e in _server.GetEntities())
            {
                var modelComp = e.GetComponent<ModelComponent>();
                var physics = e.GetComponent<PhysicsComponent>();
                if (modelComp != null && physics != null)
                {
                    _modelRenderer.RenderModel(modelComp, physics, _flyCamera.ViewMatrix, projection, _flyCamera.Position, null);
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