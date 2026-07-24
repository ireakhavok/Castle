// Folder: SiegeEngine/Scenes
// File: RuntimeGameplayScene.cs
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.ContextManagement;
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
        private readonly PlayerMovement _playerMovement;
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
        private ModelManager _modelManager;
        private SkyboxRenderer _skyboxRenderer;
        private SkyboxData _skyboxData;
        private TerrainRenderer _terrainRenderer;
        // When true the player camera (with applied Perspective) owns the view matrix.
        private bool _usePlayerCamera = false;
        public RuntimeGameplayScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus, SceneContext ctx = null)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _player = ctx?.Player ?? new Player(1, new Vector3(10, 10, 0), 0);
            _playerMovement = ctx?.PlayerMovement;
            _flyCamera = new FlyCameraController(controlContext, window);
            DefaultDockingMode = DockingMode.Desktop;
            _modelRenderer = new ModelRenderer(renderContext);
            _heightmap = new float[_terrainWidth, _terrainHeight];
            for (int x = 0; x < _terrainWidth; x++) for (int y = 0; y < _terrainHeight; y++) _heightmap[x, y] = 5f + (float)Math.Sin(x * 0.1f + y * 0.1f) * 3f;
            _skyboxRenderer = null;
            _skyboxData = null;
            _terrainRenderer = new TerrainRenderer(renderContext);
            string projectPath = "";
            string levelName = "NewTerrain";
            string snapshotPath = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--play-project") projectPath = args[i + 1].Trim('"');
                if (args[i] == "--load-level") levelName = args[i + 1].Trim('"');
                if (args[i] == "--runtime-snapshot") snapshotPath = args[i + 1].Trim('"');
            }
            if (!string.IsNullOrEmpty(projectPath))
            {
                Console.WriteLine($"[RuntimeGameplayScene] ✅ MenuCommands command-line parsed → Project: {projectPath} | Level: {levelName}");
            }
            if (ctx != null && ctx.CurrentLevel != null && ctx.CurrentLevel.Entities.Count > 0)
            {
                Console.WriteLine($"[RuntimeGameplayScene] Rich ctx from registry with {ctx.CurrentLevel.Entities.Count} entities - preserving");
                LoadContentFromContext(ctx);
            }
            else
            {
                ctx = ctx ?? new SceneContext { PlayProjectPath = projectPath, LoadLevelName = levelName };
                LoadContentFromContext(ctx);
            }
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
            _terrainShader = new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
            _terrainBuffer = new VertexBuffer(_renderContext);
            _modelRenderer.Initialize();
            _terrainRenderer.Initialize();
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
            if (!_usePlayerCamera)
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
            Level level = ctx?.CurrentLevel;
            if (level == null || level.Entities.Count == 0)
            {
                level = new Level();
                Console.WriteLine("[RuntimeGameplayScene] Fallback empty Level detected - using ctx from registry");
            }
            _skyboxData = level.Skybox;
            if (_skyboxData != null && _skyboxData.Enabled)
            {
                ResolveSkyboxPaths(_skyboxData, projectPath);
                _skyboxRenderer = new SkyboxRenderer(_renderContext);
                _skyboxRenderer.Initialize();
                _skyboxRenderer.LoadSkybox(_skyboxData);
            }
            LoadLevelData(levelName, projectPath);
            // Prefer pure in-memory heightmap snapshot from the Play payload when present.
            // Fall back to disk path only when no live/unsaved snapshot was transferred.
            if (ctx?.HeightmapSnapshot != null
                && ctx.HeightmapSnapshot.GetLength(0) > 0
                && ctx.HeightmapSnapshot.GetLength(1) > 0)
            {
                _heightmap = ctx.HeightmapSnapshot;
                _terrainWidth = _heightmap.GetLength(0);
                _terrainHeight = _heightmap.GetLength(1);
                Console.WriteLine($"[RuntimeGameplayScene] ✅ Using HeightmapSnapshot from pure in-memory Play payload ({_terrainWidth}x{_terrainHeight})");
                // Still try to load color texture from disk when available; heightmap itself stays live.
                string colorPath = !string.IsNullOrEmpty(projectPath)
                    ? Path.Combine(projectPath, "Assets", "Terrain", levelName + ".png")
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terrain", levelName + ".png");
                _terrainTextureId = TerrainTextureParser.LoadColorTexture(_renderContext, colorPath);
                _hasColorTexture = _terrainTextureId != 0;
                BuildTexturedMesh();
            }
            else
            {
                LoadExactSavedTerrain(projectPath, levelName);
            }
            _modelManager = ctx?.ModelManager ?? ModelManager.Instance ?? new ModelManager(_renderContext);
            ModelManager.EnsurePacksLoaded(projectPath, level);
            Console.WriteLine($"[RuntimeGameplayScene] Payload-driven ctx: level.Entities = {level.Entities.Count} - rehydrate starting");
            foreach (var e in level.Entities)
            {
                var mc = e.GetComponent<ModelComponent>();
                if (mc != null && _modelManager.TryGetModel(mc.Key, out var m))
                {
                    mc.Model = m;
                }
                _server.AddEntity(e);
                Console.WriteLine($"[RuntimeGameplayScene] Rehydrated + added saved entity {e.Id} Type='{e.Type}' Position from Level (exact match, no spoof)");
            }
            // Restore authoring-time location from Level entity-1 when present
            var existingPlayerEntity = level.Entities.FirstOrDefault(e => e.Id == _player.EntityId);
            if (existingPlayerEntity != null)
            {
                var existingPhys = existingPlayerEntity.GetComponent<PhysicsComponent>();
                if (existingPhys != null)
                {
                    _player.Physics.Position = existingPhys.Position;
                    _player.Physics.Rotation = existingPhys.Rotation;
                }
            }
            // Apply SceneSettings when present (null-safe, no forced defaults)
            var settings = ctx?.SceneData?.Settings;
            if (settings != null)
            {
                // PreferredSpawnPointIds – first matching entity ID that has a PhysicsComponent
                if (settings.PreferredSpawnPointIds != null && settings.PreferredSpawnPointIds.Count > 0)
                {
                    foreach (int id in settings.PreferredSpawnPointIds)
                    {
                        var spawnEntity = level.Entities.FirstOrDefault(e => e.Id == id);
                        if (spawnEntity != null)
                        {
                            var spawnPhysics = spawnEntity.GetComponent<PhysicsComponent>();
                            if (spawnPhysics != null)
                            {
                                _player.Physics.Position = spawnPhysics.Position;
                                Console.WriteLine($"[RuntimeGameplayScene] Applied PreferredSpawnPointId {id} → player at {spawnPhysics.Position}");
                                break;
                            }
                        }
                    }
                }
                // AvatarPackKey – resolve model, bind to player, register player entity so it renders
                string avatarKey = null;
                if (!string.IsNullOrWhiteSpace(settings.AvatarPackKey))
                {
                    avatarKey = settings.AvatarPackKey.Trim().ToLower();
                    if (_modelManager.TryGetModel(avatarKey, out var avatarModel))
                    {
                        _player.SetModel(avatarModel);
                        Console.WriteLine($"[RuntimeGameplayScene] AvatarPackKey '{avatarKey}' resolved – model bound to player");
                    }
                    else
                    {
                        Console.WriteLine($"[RuntimeGameplayScene] AvatarPackKey '{avatarKey}' not found in ModelManager");
                        avatarKey = null;
                    }
                }
                // AnimationPackKey – optional; load pack, resolve relative clip paths, attach blend stack to avatar
                if (!string.IsNullOrWhiteSpace(settings.AnimationPackKey) && avatarKey != null)
                {
                    string animKey = settings.AnimationPackKey.Trim();
                    if (_modelManager.TryLoadPackByKey(animKey, projectPath) &&
                        _modelManager.TryGetAnimationPack(animKey, out var animPack))
                    {
                        // Locate the on-disk json so relative clip paths can be resolved
                        string packsDir = Path.Combine(projectPath, "Assets", "Packs");
                        string jsonPath = Path.Combine(packsDir, animKey.ToLowerInvariant() + ".json");
                        if (!File.Exists(jsonPath))
                            jsonPath = Path.Combine(packsDir, animKey + ".json");
                        if (!File.Exists(jsonPath))
                        {
                            // also try the folder style
                            string folderJson = Path.Combine(projectPath, "Assets", animKey.ToLowerInvariant(), "assetpack.json");
                            if (File.Exists(folderJson)) jsonPath = folderJson;
                            else
                            {
                                folderJson = Path.Combine(projectPath, "Assets", animKey, "assetpack.json");
                                if (File.Exists(folderJson)) jsonPath = folderJson;
                            }
                        }
                        if (File.Exists(jsonPath))
                        {
                            _modelManager.AttachResolvedBlendStack(avatarKey, animPack, jsonPath);
                            Console.WriteLine($"[RuntimeGameplayScene] AnimationPackKey '{animKey}' attached as blend stack to avatar '{avatarKey}'");
                        }
                        else
                        {
                            // paths already absolute or pack had no clips – still try a plain attach
                            var stack = animPack.CreateBlendStack();
                            _modelManager.AttachBlendStack(avatarKey, stack);
                            Console.WriteLine($"[RuntimeGameplayScene] AnimationPackKey '{animKey}' attached (no relative-path resolution needed)");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[RuntimeGameplayScene] AnimationPackKey '{animKey}' could not be loaded");
                    }
                }
                // Register / update the player entity so ModelRenderer draws the avatar
                if (avatarKey != null)
                {
                    var playerEntity = _server.GetEntityById(_player.EntityId);
                    if (playerEntity == null)
                    {
                        playerEntity = new Entity { Id = _player.EntityId, Type = "Player" };
                        playerEntity.AddComponent(_player);
                        playerEntity.AddComponent(_player.Physics);
                        playerEntity.AddComponent(new ModelComponent { Key = avatarKey, Model = _player.Model });
                        _server.AddEntity(playerEntity);
                        Console.WriteLine($"[RuntimeGameplayScene] Player entity {_player.EntityId} registered with ModelComponent Key='{avatarKey}'");
                    }
                    else
                    {
                        // Force the exact same component instances so movement writes are visible to both camera and ModelRenderer
                        playerEntity.AddComponent(_player);
                        playerEntity.AddComponent(_player.Physics);
                        var mc = playerEntity.GetComponent<ModelComponent>();
                        if (mc == null)
                        {
                            playerEntity.AddComponent(new ModelComponent { Key = avatarKey, Model = _player.Model });
                        }
                        else
                        {
                            mc.Key = avatarKey;
                            mc.Model = _player.Model;
                        }
                    }
                }
                // CameraMode
                if (!string.IsNullOrWhiteSpace(settings.CameraMode) && _player.Camera != null)
                {
                    if (Enum.TryParse<Perspective>(settings.CameraMode.Trim(), true, out var perspective))
                    {
                        _player.Camera.SetPerspective(perspective);
                        _usePlayerCamera = true;
                        Console.WriteLine($"[RuntimeGameplayScene] Applied CameraMode → {perspective}");
                    }
                }
                else if (avatarKey != null)
                {
                    // Avatar present but no explicit CameraMode → still prefer the player camera
                    _usePlayerCamera = true;
                }
                // ControllerTypeName is applied by SceneManager / ScriptLoader when a PlayerMovement instance is present
            }
            if (!_usePlayerCamera)
            {
                ForceVisibleOverheadCamera();
                _flyCamera.Update(0f, 0f, true);
            }
        }
        private void ResolveSkyboxPaths(SkyboxData skybox, string projectPath)
        {
            if (skybox == null || string.IsNullOrEmpty(projectPath)) return;
            if (!string.IsNullOrEmpty(skybox.CubemapPath) && !Path.IsPathRooted(skybox.CubemapPath))
            {
                skybox.CubemapPath = Path.GetFullPath(Path.Combine(projectPath, skybox.CubemapPath));
            }
            if (skybox.Faces != null)
            {
                for (int i = 0; i < skybox.Faces.Count; i++)
                {
                    string f = skybox.Faces[i];
                    if (!string.IsNullOrEmpty(f) && !Path.IsPathRooted(f))
                    {
                        skybox.Faces[i] = Path.GetFullPath(Path.Combine(projectPath, f));
                    }
                }
            }
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
            Matrix4x4 view = _usePlayerCamera && _player?.Camera != null
                ? _player.Camera.ViewMatrix
                : _flyCamera.ViewMatrix;
            RenderGameplayContent(entities, view, Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, AspectRatio, 0.1f, 1000f));
        }
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            if (_usePlayerCamera && _player?.Camera != null)
            {
                _player.Camera.Update(deltaTime, 0f, true);
                if (_playerMovement != null)
                {
                    _playerMovement.Update(_player, deltaTime, (id, pos, rotation) => { }, _player.Camera);
                }
            }
            else
            {
                _flyCamera.Update(deltaTime, 0f, true);
            }
            if (_firstFrame)
            {
                _firstFrame = false;
                if (!_usePlayerCamera)
                    ForceVisibleOverheadCamera();
            }
        }
        protected override void RenderGameplayContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            if (_skyboxRenderer != null && _skyboxData != null && _skyboxData.Enabled)
            {
                _skyboxRenderer.RenderSkybox(_skyboxData, view, projection);
            }
            _terrainRenderer.RenderTerrain(view, projection, _hasColorTexture, _terrainTextureId, _terrainBuffer, _heightmap, false);
            Vector3 camPos = _usePlayerCamera && _player?.Camera != null
                ? _player.Camera.Position
                : _flyCamera.Position;
            foreach (var e in _server.GetEntities())
            {
                var modelComp = e.GetComponent<ModelComponent>();
                var physics = e.GetComponent<PhysicsComponent>();
                if (modelComp != null && physics != null && !string.IsNullOrEmpty(modelComp.Key))
                {
                    _modelRenderer.RenderEntityFully(modelComp, physics, view, projection, camPos);
                }
            }
            PanelManager.Current?.Render();
        }
        public override void Dispose()
        {
            _skyboxRenderer?.Dispose();
            _terrainRenderer?.Dispose();
            _terrainShader?.Dispose();
            _terrainBuffer?.Dispose();
            _modelRenderer?.Dispose();
            base.Dispose();
        }
    }
}