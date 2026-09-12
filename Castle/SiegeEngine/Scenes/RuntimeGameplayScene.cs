using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Renderers;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.Core.Terrain;
using SiegeEngine.Core.UI;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Numerics;
namespace SiegeEngine.Scenes
{
    public unsafe class RuntimeGameplayScene : GameScene
    {
        private Player _player;
        private readonly PlayerMovement _playerMovement;
        private readonly FlyCameraController _flyCamera;
        private ShaderProgram _terrainShader;
        private ModelRenderer _modelRenderer;
        private bool _contentLoaded = false;
        private bool _firstFrame = true;
        private ModelManager _modelManager;
        private bool _usePlayerCamera = false;
        private PlayerPresence _playerPresence = PlayerPresence.Avatar;
        private bool _panelHosted = false;
        private bool _inputLive = false;
        private float _frameScroll = 0f;
        private bool _paused = false;
        private readonly Dictionary<string, HostedContentPanel> _hostedHuds = new Dictionary<string, HostedContentPanel>(StringComparer.OrdinalIgnoreCase);
        public RuntimeGameplayScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus, SceneContext ctx = null)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _player = ctx?.Player;
            _playerMovement = ctx?.PlayerMovement;
            _flyCamera = new FlyCameraController(controlContext, window);
            DefaultDockingMode = DockingMode.Desktop;
            _modelRenderer = new ModelRenderer(renderContext);
            _terrainWidth = 205;
            _terrainHeight = 205;
            _hasColorTexture = true;
            _terrainWireframe = false;
            _heightmap = new float[_terrainWidth, _terrainHeight];
            for (int x = 0; x < _terrainWidth; x++) for (int y = 0; y < _terrainHeight; y++) _heightmap[x, y] = 5f + (float)Math.Sin(x * 0.1f + y * 0.1f) * 3f;
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
            _panelHosted = ctx != null && (ctx.IsPanelHosted || ctx.IsHostedPreview);
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
            // Never replace authored Environment / Skybox with a name-only SceneData.
            // Play Game was dropping Post Process sun + fog here and falling back
            // to LightingFrame.DefaultSunDirection.
            if (_sceneData == null)
                LoadSceneData(new SceneData { Name = levelName ?? "Main" });
            else if (string.IsNullOrWhiteSpace(_sceneData.Name))
                _sceneData.Name = levelName ?? "Main";
            _eventBus.Publish(new SceneActivatedEvent(levelName));
            _player?.InitializeCamera(_controlContext, _window);
            BindCameraGround();
        }
        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _terrainShader = new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
            _modelRenderer.Initialize();
            SetupPureRuntimeWorld();
            if (!_panelHosted)
            {
                // Play Host feeds wheel via PanelManager → SetScrollDelta.
                // Play Game has no PanelManager; an empty callback swallowed GLFW scroll.
                _controlContext.SetScrollCallback(_window, (w, xoffset, yoffset) =>
                {
                    _frameScroll += (float)yoffset;
                });
                _controlContext.SetWindowSizeCallback(_window, (w, newWidth, newHeight) =>
                {
                    if (newWidth > 0 && newHeight > 0)
                        Resize(newWidth, newHeight);
                });
            }
            _player?.InitializeCamera(_controlContext, _window);
            BindCameraGround();
            if (!_usePlayerCamera && _playerPresence != PlayerPresence.Spectator)
                ForceVisibleOverheadCamera();
            BuildTexturedMesh();
            EnsureHeightProvider();
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
        private void BindCameraGround()
        {
            if (_player?.Camera == null || _heightmap == null) return;
            _player.Camera.SetGroundQuery(new HeightmapAdapter(_heightmap, 1.0f, 1.0f));
        }
        private void EnsureHeightProvider()
        {
            if (_heightmap == null || _server == null) return;
            var ground = new HeightmapAdapter(_heightmap, 1.0f, 1.0f);
            _server.SetHeightProvider(ground);
            _player?.Camera?.SetGroundQuery(ground);
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
            if (ctx?.SceneData != null)
                LoadSceneData(ctx.SceneData);
            _playerPresence = ctx?.SceneData?.Settings != null
                ? ctx.SceneData.Settings.PlayerPresence
                : PlayerPresence.Avatar;
            ApplyAuthoredEnvironment(level.Environment ?? ctx?.SceneData?.Environment, level.Skybox ?? ctx?.SceneData?.Skybox);
            _skyboxData = _sceneData?.Skybox ?? level.Skybox;
            if (_skyboxData != null && _skyboxData.Enabled)
            {
                ResolveSkyboxPaths(_skyboxData, projectPath);
                EnsureSkyboxRenderer();
                _skyboxRenderer.LoadSkybox(_skyboxData);
            }
            LoadLevelData(levelName, projectPath);
            if (ctx?.HeightmapSnapshot != null
                && ctx.HeightmapSnapshot.GetLength(0) > 0
                && ctx.HeightmapSnapshot.GetLength(1) > 0)
            {
                _heightmap = ctx.HeightmapSnapshot;
                _terrainWidth = _heightmap.GetLength(0);
                _terrainHeight = _heightmap.GetLength(1);
                Console.WriteLine($"[RuntimeGameplayScene] ✅ Using HeightmapSnapshot from pure in-memory Play payload ({_terrainWidth}x{_terrainHeight})");
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
            EnsureHeightProvider();
            _modelManager = ctx?.ModelManager ?? ModelManager.Instance ?? new ModelManager(_renderContext);
            ModelManager.EnsurePacksLoaded(projectPath, level);
            Console.WriteLine($"[RuntimeGameplayScene] Payload-driven ctx: level.Entities = {level.Entities.Count} - rehydrate starting");
            foreach (var e in level.Entities)
            {
                var mc = e.GetComponent<ModelComponent>();
                var phys = e.GetComponent<PhysicsComponent>();
                if (mc != null && _modelManager.TryGetModel(mc.Key, out var m))
                {
                    mc.Model = m;
                    if (phys != null)
                    {
                        phys.Size = m.GetBoundingSize();
                        phys.LocalBoundsMinCm = m.LocalBoundsMinCm;
                        phys.LocalBoundsMaxCm = m.LocalBoundsMaxCm;
                        phys.RebuildShape(m, mc);
                    }
                }
                if (phys != null && e.Type != null && e.Type.Equals("Player", StringComparison.OrdinalIgnoreCase)
                    && _playerPresence == PlayerPresence.Avatar)
                {
                    phys.BodyType = BodyType.Dynamic;
                    phys.KeepUpright = true;
                    var pmc = e.GetComponent<ModelComponent>();
                    phys.RebuildShape(pmc?.Model ?? _player?.Model);
                }
                _server.AddEntity(e);
                var placedLight = e.GetComponent<LightComponent>();
                Console.WriteLine($"[RuntimeGameplayScene] Rehydrated + added saved entity {e.Id} Type='{e.Type}' Light={(placedLight != null ? placedLight.Type.ToString() : "none")} Position from Level (exact match, no spoof)");
            }
            if (_player != null)
                SetPlayer(_player);
            ModelManager.EnsurePacksLoaded(projectPath, level);
            Console.WriteLine($"[RuntimeGameplayScene] Server entities={_server.GetEntities()?.Count ?? 0} InstanceModels={(ModelManager.Instance != null)}");
            var settings = ctx?.SceneData?.Settings;
            _playerPresence = settings != null ? settings.PlayerPresence : PlayerPresence.Avatar;
            if (_playerPresence != PlayerPresence.Avatar)
            {
                _player = null;
                SetPlayer(null);
                _usePlayerCamera = false;
            }
            else
            {
                EnsurePlayer(level, settings);
                settings?.ApplyToPlayer(_player?.Physics);
                ApplyPreferredSpawn(level, settings);
            }

            if (settings != null && _playerPresence == PlayerPresence.Avatar)
            {
                string avatarKey = null;
                if (!string.IsNullOrWhiteSpace(settings.AvatarPackKey))
                {
                    avatarKey = settings.AvatarPackKey.Trim().ToLower();
                }
                if (!string.IsNullOrWhiteSpace(avatarKey))
                {
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
                AnimationPack attachedPack = null;
                if (!string.IsNullOrWhiteSpace(settings.AnimationPackKey) && avatarKey != null)
                {
                    string animKey = settings.AnimationPackKey.Trim();
                    if (_modelManager.TryLoadPackByKey(animKey, projectPath) &&
                        _modelManager.TryGetAnimationPack(animKey, out var animPack))
                    {
                        attachedPack = animPack;
                        string packsDir = Path.Combine(projectPath, "Assets", "Packs");
                        string jsonPath = Path.Combine(packsDir, animKey.ToLowerInvariant() + ".json");
                        if (!File.Exists(jsonPath))
                            jsonPath = Path.Combine(packsDir, animKey + ".json");
                        if (!File.Exists(jsonPath))
                        {
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
                if (avatarKey != null && _player != null)
                {
                    var playerEntity = _server.GetEntityById(_player.EntityId);
                    if (playerEntity == null)
                    {
                        playerEntity = new Entity { Id = _player.EntityId, Type = "Player" };
                        playerEntity.AddComponent(_player);
                        _player.Physics.BodyType = BodyType.Dynamic;
                        _player.Physics.KeepUpright = true;
                        _player.BindMeshCollider();
                        playerEntity.AddComponent(_player.Physics);
                        playerEntity.AddComponent(new ModelComponent { Key = avatarKey, Model = _player.Model });
                        _server.AddEntity(playerEntity);
                        Console.WriteLine("[RuntimeGameplayScene] Player entity registered id=" +
                            _player.EntityId + " pos=" + _player.Physics.Position);
                        _server.SnapToGround(_player.Physics);
                        Console.WriteLine("[RuntimeGameplayScene] After SnapToGround pos=" +
                            _player.Physics.Position + " render=" + _player.Physics.RenderPosition);
                    }
                    else
                    {
                        playerEntity.AddComponent(_player);
                        _player.Physics.BodyType = BodyType.Dynamic;
                        _player.Physics.KeepUpright = true;
                        _player.BindMeshCollider();
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
                    if (attachedPack != null)
                    {
                        var blendComp = new BlendedAnimationComponent
                        {
                            Pack = attachedPack,
                            Playing = true,
                            CurrentBlendParams = Vector3.Zero
                        };
                        playerEntity.AddComponent(blendComp);
                        _player.BlendComponent = blendComp;
                        Console.WriteLine($"[RuntimeGameplayScene] BlendedAnimationComponent attached to player entity {_player.EntityId}");
                    }
                }
                if (!string.IsNullOrWhiteSpace(settings.CameraMode) && _player?.Camera != null)
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
                    _usePlayerCamera = true;
                }
            }
            // Final single-instance guarantee: the entity that PhysicsWorld steps and ModelRenderer
            // draws MUST hold the exact same PhysicsComponent instance that PlayerMovement writes.
            // AddComponent overwrites by Type, so this replaces any earlier FromData instance.
            if (_player != null)
            {
                _player.Physics.BodyType = BodyType.Dynamic;
                _player.Physics.KeepUpright = true;
                _player.BindMeshCollider();
                var playerEntity = _server.GetEntityById(_player.EntityId);
                if (playerEntity != null)
                {
                    playerEntity.AddComponent(_player);
                    playerEntity.AddComponent(_player.Physics);
                    Console.WriteLine($"[RuntimeGameplayScene] Single PhysicsComponent instance enforced for player entity {_player.EntityId}");
                }
                else
                {
                    playerEntity = new Entity { Id = _player.EntityId, Type = "Player" };
                    playerEntity.AddComponent(_player);
                    playerEntity.AddComponent(_player.Physics);
                    _server.AddEntity(playerEntity);
                    Console.WriteLine($"[RuntimeGameplayScene] Player entity {_player.EntityId} created with shared PhysicsComponent");
                }
                _player.InitializeCamera(_controlContext, _window);
                BindCameraGround();
                ApplyPlayerCollision(settings);
            }
            if (_playerPresence == PlayerPresence.Spectator)
            {
                Vector3 spawn = ResolvePreferredSpawn(level, settings);
                if (spawn != Vector3.Zero)
                    _flyCamera.Position = spawn;
                _flyCamera.Update(0f, 0f, true);
                _flyCamera.RefreshViewMatrix();
            }
            else if (!_usePlayerCamera)
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
            terrainEntity.AddComponent(new PhysicsComponent { Position = Vector3.Zero, BodyType = BodyType.Static });
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
        public void HandleGameHud(OpenGameHudEvent request)
        {
            if (request == null) return;
            string key = request.Key;
            if (string.IsNullOrEmpty(key)) key = request.HtmlRelativePath ?? request.Title ?? "hud";
            if (!request.Open)
            {
                if (_hostedHuds.TryGetValue(key, out var existing))
                {
                    existing.Dispose();
                    _hostedHuds.Remove(key);
                }
                return;
            }
            if (_hostedHuds.ContainsKey(key)) return;
            if (request.Content == null && string.IsNullOrEmpty(request.HtmlContent) && string.IsNullOrEmpty(request.HtmlRelativePath)) return;
            var panel = new HostedContentPanel(_renderContext, _controlContext, _window, _eventBus, request);
            panel.Init();
            PlaceSceneHud(panel, request);
            _hostedHuds[key] = panel;
        }

        private void PlaceSceneHud(HostedContentPanel panel, OpenGameHudEvent request)
        {
            float w = request.Width > 1f ? request.Width : 248f;
            float h = request.Height > 1f ? request.Height : 520f;
            float viewW = _width > 0 ? _width : w;
            float viewH = _height > 0 ? _height : h;
            const float margin = 16f;
            const float top = 48f;
            float x = request.PosX;
            float y = request.PosY;
            switch (request.Anchor)
            {
                case HudAnchor.Right:
                    x = viewW - w - margin;
                    y = float.IsNaN(y) ? top : y;
                    break;
                case HudAnchor.Left:
                    x = margin;
                    y = float.IsNaN(y) ? top : y;
                    break;
                case HudAnchor.Top:
                    x = float.IsNaN(x) ? (viewW - w) * 0.5f : x;
                    y = top;
                    break;
                case HudAnchor.Bottom:
                    x = float.IsNaN(x) ? (viewW - w) * 0.5f : x;
                    y = viewH - h - margin;
                    break;
                case HudAnchor.Center:
                    x = (viewW - w) * 0.5f;
                    y = (viewH - h) * 0.5f;
                    break;
                default:
                    if (float.IsNaN(x)) x = margin;
                    if (float.IsNaN(y)) y = top;
                    break;
            }
            panel.Size = new Vector2(w, h);
            panel.Position = new Vector2(x, y);
            panel.OnPanelResize(w, h);
        }

        private void UpdateHostedHuds(float deltaTime)
        {
            foreach (var kv in _hostedHuds)
            {
                if (kv.Value != null && kv.Value.Visible)
                    kv.Value.Update(deltaTime, Vector2.Zero, false, false, false, 0f);
            }
        }

        public void SetInputLive(bool live)
        {
            _inputLive = live;
        }

        public void SetScrollDelta(float scroll)
        {
            _frameScroll = scroll;
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
        }

        public AudioSystem HostedAudio => _server?.GetSystem<AudioSystem>();

        public override void Update(float deltaTime)
        {
            if (_paused)
                return;
            // Movement first, then physics — contact corrections must be the last write to Position.
            bool driveCamera = !_panelHosted || _inputLive;
            if (driveCamera && _usePlayerCamera && _player?.Camera != null)
            {
                _player.Camera.Update(deltaTime, _frameScroll, true);
                if (_playerMovement != null)
                {
                    _playerMovement.Update(_player, deltaTime, (id, pos, rotation) => { }, _player.Camera);
                }
            }
            else if (driveCamera)
            {
                _flyCamera.Update(deltaTime, _frameScroll, true);
            }
            _frameScroll = 0f;
            base.Update(deltaTime);
            if (_usePlayerCamera && _player?.Camera != null)
                _player.Camera.RefreshFromPhysics();
            if (_firstFrame)
            {
                _firstFrame = false;
                if (!_usePlayerCamera && _playerPresence != PlayerPresence.Spectator)
                    ForceVisibleOverheadCamera();
            }
            UpdateHostedHuds(deltaTime);
        }
        protected override Vector3 GetViewPosition()
        {
            if (_usePlayerCamera && _player?.Camera != null)
                return _player.Camera.Position;
            return _flyCamera.Position;
        }

        protected override void GetViewProjection(out Matrix4x4 view, out Matrix4x4 projection)
        {
            view = _usePlayerCamera && _player?.Camera != null
                ? _player.Camera.ViewMatrix
                : _flyCamera.ViewMatrix;
            projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, AspectRatio, 0.1f, 20000f);
        }
        protected override void RenderEntities(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
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
        }

        protected override void RenderOverlay(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            if (_panelHosted)
            {
                foreach (var kv in _hostedHuds)
                {
                    if (kv.Value != null && kv.Value.Visible)
                        kv.Value.Render();
                }
                return;
            }
            PanelManager.Current?.Render();
        }
        public override void Dispose()
        {
            foreach (var kv in _hostedHuds)
            {
                try { kv.Value?.Dispose(); } catch { }
            }
            _hostedHuds.Clear();
            try { HostedAudio?.StopAll(); } catch { }
            try { HostedAudio?.Dispose(); } catch { }
            _terrainShader?.Dispose();
            _modelRenderer?.Dispose();
            base.Dispose();
        }

        static SceneSettings LoadSceneSettingsFromProject(string projectPath, string levelName)
        {
            try
            {
                if (string.IsNullOrEmpty(projectPath) || string.IsNullOrEmpty(levelName))
                    return null;
                string file = Path.Combine(projectPath, "project.json");
                if (!File.Exists(file))
                {
                    return null;
                }
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (!doc.RootElement.TryGetProperty("Scenes", out var scenes))
                    return null;
                JsonElement scene;
                if (scenes.ValueKind == JsonValueKind.Object && scenes.TryGetProperty(levelName, out scene))
                { }
                else
                    return null;
                if (!scene.TryGetProperty("settings", out var settingsEl))
                {
                    return null;
                }
                var loaded = JsonSerializer.Deserialize<SceneSettings>(settingsEl.GetRawText());
                return loaded;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[RuntimeGameplayScene] LoadSceneSettingsFromProject failed: " + ex.Message);
                return null;
            }
        }

        static Vector3 ResolvePreferredSpawn(Level level, SceneSettings settings)
        {
            if (settings?.PreferredSpawnPointIds == null || level?.Entities == null)
                return Vector3.Zero;
            for (int i = 0; i < settings.PreferredSpawnPointIds.Count; i++)
            {
                int id = settings.PreferredSpawnPointIds[i];
                Entity spawnEntity = level.Entities.FirstOrDefault(e => e.Id == id);
                var spawnPhysics = spawnEntity?.GetComponent<PhysicsComponent>();
                if (spawnPhysics == null) continue;
                float side = spawnPhysics.Size.X;
                if (side < 0.5f) side = 0.5f;
                return spawnPhysics.Position + new Vector3(side + 0.75f, 0f, 0f);
            }
            return Vector3.Zero;
        }

        static bool AvatarHasSkeleton(FBXModel model)
        {
            return model?.Skeleton?.Bones != null && model.Skeleton.Bones.Count > 0;
        }

        void ApplyPlayerCollision(SceneSettings settings)
        {
            if (_player?.Physics == null) return;
            var model = _player.Model;
            var collision = settings != null
                ? settings.ResolvePlayerCollisionType(AvatarHasSkeleton(model))
                : (AvatarHasSkeleton(model) ? PlayerCollisionType.Hitbox : PlayerCollisionType.Capsule);
            if (collision == PlayerCollisionType.Capsule)
            {
                _player.Physics.BindUprightCapsuleFromBounds(model);
                return;
            }
            _player.Physics.UseBoneHitboxes = true;
            _player.BindMeshCollider();
        }

        void EnsurePlayer(Level level, SceneSettings settings)
        {
            if (settings != null && settings.PlayerPresence != PlayerPresence.Avatar)
                return;
            if (_player != null)
            {
                SetPlayer(_player);
                return;
            }
            var existingPlayerEntity = level?.Entities?.FirstOrDefault(e =>
                e.Type != null && e.Type.Equals("Player", StringComparison.OrdinalIgnoreCase));
            if (existingPlayerEntity != null)
            {
                var existingPhys = existingPlayerEntity.GetComponent<PhysicsComponent>();
                Vector3 seed = existingPhys != null ? existingPhys.Position : ResolvePreferredSpawn(level, settings);
                _player = new Player(existingPlayerEntity.Id, seed, 0);
                SetPlayer(_player);
                if (existingPhys != null)
                {
                    _player.Physics.Position = existingPhys.Position;
                    _player.Physics.RenderPosition = existingPhys.Position;
                    _player.Physics.Rotation = existingPhys.Rotation;
                    _player.Physics.BodyType = existingPhys.BodyType;
                    _player.Physics.Mass = existingPhys.Mass;
                    _player.Physics.Friction = existingPhys.Friction;
                    _player.Physics.StaticFriction = existingPhys.StaticFriction;
                    _player.Physics.KineticFriction = existingPhys.KineticFriction;
                    _player.Physics.Restitution = existingPhys.Restitution;
                    _player.Physics.LinearDamping = existingPhys.LinearDamping;
                    _player.Physics.AngularDamping = existingPhys.AngularDamping;
                    _player.Physics.RollingResistance = existingPhys.RollingResistance;
                    _player.Physics.ReceiveFriction = existingPhys.ReceiveFriction;
                    _player.Physics.ReceiveVerticalContact = existingPhys.ReceiveVerticalContact;
                    _player.Physics.KeepUpright = existingPhys.KeepUpright;
                    _player.Physics.CollisionEnabled = existingPhys.CollisionEnabled;
                    _player.Physics.UseBoneHitboxes = existingPhys.UseBoneHitboxes;
                    _player.BindMeshCollider();
                }
                return;
            }
            int playerId = 1;
            var source = level?.Entities;
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    if (source[i] != null && source[i].Id >= playerId)
                        playerId = source[i].Id + 1;
                }
            }
            Vector3 spawnAt = ResolvePreferredSpawn(level, settings);
            _player = new Player(playerId, spawnAt, 0);
            SetPlayer(_player);
        }

        void ApplyPreferredSpawn(Level level, SceneSettings settings)
        {
            if (_player == null)
            {
                return;
            }
            if (settings?.PreferredSpawnPointIds == null || settings.PreferredSpawnPointIds.Count == 0)
            {
                return;
            }
            for (int i = 0; i < settings.PreferredSpawnPointIds.Count; i++)
            {
                int id = settings.PreferredSpawnPointIds[i];
                Entity spawnEntity = null;
                if (level != null)
                    spawnEntity = level.Entities.FirstOrDefault(e => e.Id == id);
                if (spawnEntity == null)
                    spawnEntity = _server.GetEntityById(id);
                var spawnPhysics = spawnEntity?.GetComponent<PhysicsComponent>();
                if (spawnPhysics == null)
                    continue;
                float side = spawnPhysics.Size.X;
                if (side < 0.5f) side = 0.5f;
                Vector3 nextTo = spawnPhysics.Position + new Vector3(side + 0.75f, 0f, 0f);
                _player.Physics.Position = nextTo;
                _player.Physics.RenderPosition = nextTo;
                _player.Physics.Rotation = spawnPhysics.Rotation;
                _server.SnapToGround(_player.Physics);
                _player.Physics.RenderPosition = _player.Physics.Position;
                return;
            }
        }

    }
}
