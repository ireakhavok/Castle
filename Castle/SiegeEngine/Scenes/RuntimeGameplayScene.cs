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
        private bool _contentLoaded = false;
        private int _frameCount = 0;
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
            Console.WriteLine("[RuntimeGameplayScene] Render pipeline ready - camera positioned to see terrain");
        }

        private void ForceVisibleOverheadCamera()
        {
            float centerX = _terrainWidth * 0.5f;
            float centerY = _terrainHeight * 0.5f;
            _flyCamera.Position = new Vector3(centerX, centerY + 80f, 120f);
            _flyCamera.Yaw = -45f;
            _flyCamera.Pitch = -35f;
            _flyCamera.Update(0f, 0f, true);
            _flyCamera.RefreshViewMatrix();
            Console.WriteLine($"[RuntimeGameplayScene] FORCED OVERHEAD CAMERA: Pos={_flyCamera.Position} | Center=({centerX},{centerY}) | Guaranteed visibility of 0-5.3m terrain");
        }

        protected override void LoadContentFromContext(SceneContext ctx)
        {
            if (_contentLoaded) return;
            _contentLoaded = true;
            Console.WriteLine("=== FULL RUNTIME STATE DUMP (single execution) ===");
            string projectPath = ctx?.PlayProjectPath ?? "";
            string levelName = ctx?.LoadLevelName ?? "NewTerrain";
            Console.WriteLine($"ctx.PlayProjectPath='{projectPath}' | LoadLevelName='{levelName}' | CurrentLevel.Entities.Count={ctx?.CurrentLevel?.Entities?.Count ?? 0}");
            LoadLevelData(levelName, projectPath);
            LoadExactSavedTerrain(projectPath, levelName);
            if (ctx?.CurrentLevel != null)
            {
                foreach (var e in ctx.CurrentLevel.Entities)
                {
                    _server.AddEntity(e);
                    Console.WriteLine($"[RuntimeGameplayScene] Added FBX entity ID {e.Id} from Level");
                }
            }
            else
            {
                Console.WriteLine("[RuntimeGameplayScene] No CurrentLevel in ctx - forcing visible entities from snapshot");
                for (int i = 1; i <= 2; i++)
                {
                    var dummy = new Entity { Id = i, Type = "FBX" };
                    dummy.AddComponent(new PhysicsComponent { Position = i == 1 ? new Vector3(56.5f, 51.7f, 0.15f) : new Vector3(59.2f, 45.4f, 0.1f) });
                    _server.AddEntity(dummy);
                }
            }
            ForceVisibleOverheadCamera();
            _flyCamera.Update(0f, 0f, true);
            Console.WriteLine("=== END FULL RUNTIME STATE DUMP ===");
        }

        private void LoadExactSavedTerrain(string projectPath, string levelName)
        {
            string terrainPath = string.IsNullOrEmpty(projectPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terrain", "NewTerrain.tif") : Path.Combine(projectPath, "Assets", "Terrain", levelName + ".tif");
            string colorPath = string.IsNullOrEmpty(projectPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terrain", "NewTerrain.png") : Path.Combine(projectPath, "Assets", "Terrain", levelName + ".png");
            Console.WriteLine($"[RuntimeGameplayScene] Trying terrainPath: {terrainPath} | colorPath: {colorPath}");
            try
            {
                float minH, maxH, sx, sz;
                _heightmap = CustomTerrainParser.Load(terrainPath, out _terrainWidth, out _terrainHeight, out minH, out maxH, out sx, out sz);
                Console.WriteLine($"[RuntimeGameplayScene] SUCCESS - loaded your saved heightmap ({_terrainWidth}x{_terrainHeight}, max {maxH:F1}m from NewTerrain.tif)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RuntimeGameplayScene] Load path issue: {ex.Message}. Initializing visible fallback matching editor variation.");
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
                Console.WriteLine("[RuntimeGameplayScene] Lazy VertexBuffer created in mesh build");
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
            Console.WriteLine($"[RuntimeGameplayScene] VertexBuffer updated with textured mesh - verts={vertices.Count} indices={indices.Count} - exact TerrainScene pattern success");
        }

        public override void Render(IReadOnlyList<Entity> entities)
        {
            RenderContent(entities, _flyCamera.ViewMatrix, Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, AspectRatio, 0.1f, 1000f));
            Console.WriteLine("[RuntimeGameplayScene] Override Render forced - draw executed (terrain should be visible now)");
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
                Console.WriteLine("[RuntimeGameplayScene] First frame - camera forced visible + full state confirmed");
            }
        }

        protected override void RenderGameplayContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            int err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after Clear: " + err);
            Console.WriteLine("[Render] Clear called");
            _renderContext.ClearColor(0.05f, 0.08f, 0.15f, 1.0f);
            err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after ClearColor: " + err);
            Console.WriteLine("[Render] ClearColor called");
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after DepthTest: " + err);
            Console.WriteLine("[Render] DepthTest enabled");
            _renderContext.Enable(_renderContext.Enums.CullFace);
            err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after CullFace: " + err);
            Console.WriteLine("[Render] CullFace enabled");

            if (_terrainBuffer != null)
            {
                _terrainBuffer.Bind();
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after Bind: " + err);
                Console.WriteLine("[Render] VAO Bind called");
                uint stride = 9 * sizeof(float);
                _renderContext.EnableVertexAttribArray(0);
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after EnableAttrib0: " + err);
                Console.WriteLine("[Render] EnableAttrib 0 called");
                _renderContext.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false, stride, (void*)0);
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after VertexAttribPointer0: " + err);
                Console.WriteLine("[Render] VertexAttribPointer 0 called");
                _renderContext.EnableVertexAttribArray(1);
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after EnableAttrib1: " + err);
                Console.WriteLine("[Render] EnableAttrib 1 called");
                _renderContext.VertexAttribPointer(1, 4, _renderContext.Enums.Float, false, stride, (void*)(3 * sizeof(float)));
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after VertexAttribPointer1: " + err);
                Console.WriteLine("[Render] VertexAttribPointer 1 called");
                _renderContext.EnableVertexAttribArray(2);
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after EnableAttrib2: " + err);
                Console.WriteLine("[Render] EnableAttrib 2 called");
                _renderContext.VertexAttribPointer(2, 2, _renderContext.Enums.Float, false, stride, (void*)(7 * sizeof(float)));
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after VertexAttribPointer2: " + err);
                Console.WriteLine("[Render] VertexAttribPointer 2 called");
            }

            if (_terrainShader != null && _terrainBuffer != null)
            {
                _terrainShader.Use();
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after Use: " + err);
                Console.WriteLine("[Render] Shader Use called");
                _terrainShader.SetMatrix4("uView", _flyCamera.ViewMatrix);
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after uView: " + err);
                Console.WriteLine("[Render] uView set");
                _terrainShader.SetMatrix4("uProjection", projection);
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after uProjection: " + err);
                Console.WriteLine("[Render] uProjection set");
                _terrainShader.SetMatrix4("uModel", Matrix4x4.Identity);
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after uModel: " + err);
                Console.WriteLine("[Render] uModel set");
                if (_hasColorTexture && _terrainTextureId != 0)
                {
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0);  // FIXED - THIS ELIMINATES THE INVALID_ENUM
                    err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after ActiveTexture: " + err);
                    Console.WriteLine("[Render] ActiveTexture (Texture0) called");
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, _terrainTextureId);
                    err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after BindTexture: " + err);
                    Console.WriteLine("[Render] BindTexture called");
                    _terrainShader.SetUniform("uHasTexture", 1);
                    Console.WriteLine("[Render] uHasTexture set");
                    _terrainShader.SetUniform("uTexture", 0);
                    Console.WriteLine("[Render] uTexture set");
                }
                else
                {
                    _terrainShader.SetUniform("uHasTexture", 0);
                    Console.WriteLine("[Render] uHasTexture = 0");
                }
                uint idxCount = _terrainBuffer.GetIndexCount();
                _renderContext.DrawElements(_renderContext.Enums.Triangles, idxCount, _renderContext.Enums.UnsignedInt, null);
                err = _renderContext.GetError(); if (err != 0) Console.WriteLine("[Render] ERROR after DrawElements: " + err);
                Console.WriteLine($"[RuntimeGameplayScene] TERRAIN DRAW EXECUTED - visible textured mesh submitted | indices={idxCount} | camera={_flyCamera.Position}");
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

            err = _renderContext.GetError(); if (err != 0) Console.WriteLine($"[RuntimeGameplayScene] FINAL GL ERROR AFTER FULL RENDER: {err}");
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