// Folder: MapRoom
// File: TerrainCreatorScene.cs
using Keystone;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Terrain;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using ToolChest;

namespace MapRoom
{
    public unsafe class TerrainCreatorScene : TerrainScene
    {
        private ToolChest.Brush _activeBrush = null;
        private Vector3 _ghostPosition;
        private bool _ghostVisible = false;
        private VertexBuffer _ghostBuffer;
        private HashSet<Guid> _processedModifications = new HashSet<Guid>();
        private bool _isBrushing = false;
        private float _lastBrushUpdateTime = 0f;
        private Vector3 _lastGhostPosition = Vector3.Zero;
        private const float BrushUpdateInterval = 0.0f;
        private const float BrushMoveThreshold = 0.3f;
        private SceneData _sceneData;
        private TerrainPaintData _paintData;
        private uint _splatTextureId = 0;
        private bool _hasSplatMap = false;

        public TerrainCreatorScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus, SceneData sceneData = null)
            : base(renderContext, controlContext, window, server, eventBus, sceneData)
        {
            _sceneData = sceneData;
            _isEditorContext = true;
            _eventBus.Subscribe<TerrainModifiedEvent>(OnTerrainModified);
        }

        public bool TryPerformPlacementRaycast(out Vector3 hitPoint)
        {
            hitPoint = Vector3.Zero;
            Vector3 rayOrigin = _flyCamera.Position;
            Vector3 rayDir = GetLookDirection();
            return RayTerrainIntersect(rayOrigin, rayDir, out hitPoint);
        }

        public Vector3 GetCameraPosition() => _flyCamera.Position;

        public Vector3 GetLookDirection()
        {
            float yawRad = _flyCamera.Yaw * (MathF.PI / 180f);
            float pitchRad = _flyCamera.Pitch * (MathF.PI / 180f);
            return Vector3.Normalize(new Vector3(
                MathF.Cos(pitchRad) * MathF.Sin(yawRad),
                MathF.Cos(pitchRad) * MathF.Cos(yawRad),
                MathF.Sin(pitchRad)
            ));
        }

        public Matrix4x4 GetViewMatrix() => _flyCamera.ViewMatrix;

        public bool TryTerrainRaycast(Vector3 origin, Vector3 dir, out Vector3 hitPoint)
        {
            return RayTerrainIntersect(origin, dir, out hitPoint);
        }

        public bool GetMouseRay(Vector2 normalizedMouse, out Vector3 rayOrigin, out Vector3 rayDir)
        {
            rayOrigin = Vector3.Zero;
            rayDir = Vector3.Zero;
            if (_flyCamera == null) return false;
            float ndcX = normalizedMouse.X * 2f - 1f;
            float ndcY = 1f - normalizedMouse.Y * 2f;
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 180f * 65f, AspectRatio, 0.1f, 50000f);
            Matrix4x4 view = _flyCamera.ViewMatrix;
            if (!Matrix4x4.Invert(proj, out Matrix4x4 invProj)) return false;
            if (!Matrix4x4.Invert(view, out Matrix4x4 invView)) return false;
            Vector4 ndcNear = new Vector4(ndcX, ndcY, -1f, 1f);
            Vector4 ndcFar = new Vector4(ndcX, ndcY, 1f, 1f);
            Vector4 eyeNearH = Vector4.Transform(ndcNear, invProj);
            Vector4 eyeFarH = Vector4.Transform(ndcFar, invProj);
            Vector3 eyeNear = new Vector3(eyeNearH.X / eyeNearH.W, eyeNearH.Y / eyeNearH.W, eyeNearH.Z / eyeNearH.W);
            Vector3 eyeFar = new Vector3(eyeFarH.X / eyeFarH.W, eyeFarH.Y / eyeFarH.W, eyeFarH.Z / eyeFarH.W);
            rayOrigin = Vector3.Transform(eyeNear, invView);
            rayDir = Vector3.Normalize(Vector3.Transform(eyeFar, invView) - rayOrigin);
            return true;
        }

        public bool GetMouseRay(Vector2 normalizedMouse, float viewportWidth, float viewportHeight, out Vector3 rayOrigin, out Vector3 rayDir)
        {
            return base.GetMouseRay(normalizedMouse, viewportWidth, viewportHeight, out rayOrigin, out rayDir);
        }

        private string ResolveFullPath(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath)) return inputPath;
            if (Path.IsPathRooted(inputPath)) return Path.GetFullPath(inputPath);
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath)) return inputPath;
            string fullPath = Path.Combine(projectPath, inputPath);
            return Path.GetFullPath(fullPath);
        }

        public void CreateBlank()
        {
            _terrainWidth = 200;
            _terrainHeight = 200;
            _heightmap = new float[_terrainWidth, _terrainHeight];
            _paintData = ProjectSettings.Current.GetOrCreatePaintData("Untitled", _terrainWidth, _terrainHeight);
            _minHeight = float.MaxValue;
            _maxHeight = float.MinValue;
            for (int x = 0; x < _terrainWidth; x++)
            {
                for (int y = 0; y < _terrainHeight; y++)
                {
                    float h = 0f;
                    _heightmap[x, y] = h;
                    if (h < _minHeight) _minHeight = h;
                    if (h > _maxHeight) _maxHeight = h;
                }
            }
            Console.WriteLine($"[TerrainCreatorScene] Created blank 200×200 terrain with height range {_minHeight:F1} to {_maxHeight:F1}");
            _useCustomScale = true;
            RebuildTerrainMesh();
            RebuildSplatTexture();
            float centerX = (_terrainWidth * _worldScaleX) / 2f;
            float centerY = (_terrainHeight * _worldScaleZ) / 2f;
            _flyCamera.Position = new Vector3(centerX, centerY + 50f, _maxHeight * 1.5f + 10f);
            _flyCamera.Yaw = 0f;
            _flyCamera.Pitch = -MathF.PI / 6f;
        }

        public void CreateTerrain(TerrainCreationParams parameters)
        {
            if (parameters == null)
            {
                CreateBlank();
                return;
            }
            float cellSize = parameters.Resolution > 0 ? parameters.Resolution : 1.0f;
            int numCellsX = (int)Math.Ceiling(parameters.Width / cellSize);
            int numCellsY = (int)Math.Ceiling(parameters.Depth / cellSize);
            _terrainWidth = numCellsX + 1;
            _terrainHeight = numCellsY + 1;
            _worldScaleX = cellSize;
            _worldScaleZ = cellSize;

            if (!string.IsNullOrEmpty(parameters.ImportPath))
            {
                Console.WriteLine($"[TerrainCreatorScene] Loading GeoTIFF: {parameters.ImportPath}");
                LoadTerrain(parameters.ImportPath);
                if (_sceneData?.Terrain != null)
                {
                    string projectPath = ProjectSettings.Current.ActiveProject;
                    if (!string.IsNullOrEmpty(projectPath) && Path.IsPathRooted(parameters.ImportPath))
                    {
                        try
                        {
                            string relative = Path.GetRelativePath(projectPath, parameters.ImportPath);
                            _sceneData.Terrain.HeightmapPath = relative;
                            Console.WriteLine($"[TerrainCreatorScene] GeoTIFF path stored in SceneData: {relative}");
                        }
                        catch { _sceneData.Terrain.HeightmapPath = parameters.ImportPath; }
                    }
                    else
                    {
                        _sceneData.Terrain.HeightmapPath = parameters.ImportPath;
                    }
                }
            }
            else
            {
                _heightmap = new float[_terrainWidth, _terrainHeight];
                _paintData = ProjectSettings.Current.GetOrCreatePaintData(parameters.Name ?? "Untitled", _terrainWidth, _terrainHeight);
                _minHeight = parameters.InitialHeight;
                _maxHeight = parameters.InitialHeight;
                for (int x = 0; x < _terrainWidth; x++)
                {
                    for (int y = 0; y < _terrainHeight; y++)
                    {
                        _heightmap[x, y] = parameters.InitialHeight * parameters.VerticalExaggeration;
                    }
                }
                Console.WriteLine($"[TerrainCreatorScene] SUCCESS: Created {parameters.Width}m × {parameters.Depth}m terrain");
                _useCustomScale = true;
                RebuildTerrainMesh();
                RebuildSplatTexture();
                float centerX = (_terrainWidth * _worldScaleX) / 2f;
                float centerY = (_terrainHeight * _worldScaleZ) / 2f;
                _flyCamera.Position = new Vector3(centerX, centerY + 50f, _maxHeight * 1.5f + 10f);
                _flyCamera.Yaw = 0f;
                _flyCamera.Pitch = -MathF.PI / 6f;
            }
        }

        public override void LoadSceneData(SceneData data)
        {
            _sceneData = data;
            string sceneName = data?.Name ?? ProjectSettings.Current.CurrentSceneName;
            _paintData = ProjectSettings.Current.GetOrCreatePaintData(sceneName, _terrainWidth > 0 ? _terrainWidth : 200, _terrainHeight > 0 ? _terrainHeight : 200);

            if (data?.Terrain != null && !string.IsNullOrEmpty(data.Terrain.HeightmapPath))
            {
                LoadTerrain(data.Terrain.HeightmapPath);
                return;
            }

            float[,] cachedMap = ProjectSettings.Current.GetUnsavedHeightmap(sceneName);
            if (cachedMap != null)
            {
                _heightmap = cachedMap;
                _terrainWidth = _heightmap.GetLength(0);
                _terrainHeight = _heightmap.GetLength(1);
                _minHeight = 0f;
                _maxHeight = 0f;
                for (int x = 0; x < _terrainWidth; x++)
                {
                    for (int y = 0; y < _terrainHeight; y++)
                    {
                        float h = _heightmap[x, y];
                        if (h < _minHeight) _minHeight = h;
                        if (h > _maxHeight) _maxHeight = h;
                    }
                }
                _worldScaleX = data?.Terrain?.WorldScaleX ?? 1.0f;
                _worldScaleZ = data?.Terrain?.WorldScaleZ ?? 1.0f;
                _useCustomScale = true;
                Console.WriteLine($"[TerrainCreatorScene.LoadSceneData] Using PER-SCENE unsaved heightmap for '{sceneName}' ({_terrainWidth}×{_terrainHeight})");
                RebuildTerrainMesh();
                RebuildSplatTexture();
                return;
            }

            if (ProjectSettings.Current.CurrentHeightmap != null &&
                (data?.Terrain == null || string.IsNullOrEmpty(data.Terrain.HeightmapPath)))
            {
                _heightmap = ProjectSettings.Current.CurrentHeightmap;
                _terrainWidth = _heightmap.GetLength(0);
                _terrainHeight = _heightmap.GetLength(1);
                _minHeight = 0f;
                _maxHeight = 0f;
                for (int x = 0; x < _terrainWidth; x++)
                {
                    for (int y = 0; y < _terrainHeight; y++)
                    {
                        float h = _heightmap[x, y];
                        if (h < _minHeight) _minHeight = h;
                        if (h > _maxHeight) _maxHeight = h;
                    }
                }
                _worldScaleX = data?.Terrain?.WorldScaleX ?? 1.0f;
                _worldScaleZ = data?.Terrain?.WorldScaleZ ?? 1.0f;
                _useCustomScale = true;
                Console.WriteLine($"[TerrainCreatorScene.LoadSceneData] Using CENTRAL in-memory heightmap ({_terrainWidth}×{_terrainHeight})");
                RebuildTerrainMesh();
                RebuildSplatTexture();
                return;
            }

            base.LoadSceneData(data);
            RebuildSplatTexture();
        }

        public override void LoadTerrain(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                base.LoadTerrain(path);
                return;
            }
            string fullPath = ResolveFullPath(path);
            Console.WriteLine($"[TerrainCreatorScene] Loading terrain - relative '{path}' → full '{fullPath}'");
            base.LoadTerrain(fullPath);
            if (_sceneData?.Terrain != null)
            {
                _sceneData.Terrain.HeightmapPath = path;
            }
            RebuildTerrainMesh();
            RebuildSplatTexture();
        }

        public void SetColorTexture(string path)
        {
            base.SetColorTexture(path);
        }

        public void SaveTerrain(string terrainName)
        {
            if (string.IsNullOrEmpty(terrainName))
                terrainName = "UntitledTerrain";
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath))
                projectPath = AppDomain.CurrentDomain.BaseDirectory;
            string saveDir = Path.Combine(projectPath, "Assets", "Terrain");
            Directory.CreateDirectory(saveDir);

            if (_sceneData?.Terrain != null && !string.IsNullOrEmpty(_sceneData.Terrain.HeightmapPath))
            {
                string fullOriginal = ResolveFullPath(_sceneData.Terrain.HeightmapPath);
                if (File.Exists(fullOriginal))
                {
                    if (!CustomTerrainParser.TryGetCustomScale(fullOriginal, out _, out _))
                    {
                        string targetName = terrainName + Path.GetExtension(fullOriginal);
                        string targetPath = Path.Combine(saveDir, targetName);
                        if (!string.Equals(fullOriginal, targetPath, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(fullOriginal, targetPath, true);
                            Console.WriteLine($"[TerrainCreatorScene] VERBATIM COPY of real GeoTIFF '{fullOriginal}' → '{targetPath}' (geo tags preserved)");
                        }
                        string relativePath = Path.GetRelativePath(projectPath, targetPath);
                        _sceneData.Terrain.HeightmapPath = relativePath;
                        return;
                    }
                }
            }

            string tifPath = Path.Combine(saveDir, terrainName + ".tif");
            string pngPath = Path.Combine(saveDir, terrainName + ".png");
            SaveAsPng(pngPath);
            CustomTerrainParser.SaveFloatTiff(tifPath, _heightmap, _worldScaleX, _worldScaleZ);

            if (_sceneData?.Terrain != null)
            {
                string relativePath = Path.GetRelativePath(projectPath, tifPath);
                _sceneData.Terrain.HeightmapPath = relativePath;
            }

            if (_paintData != null)
            {
                _paintData.SaveToDisk(projectPath, terrainName);
            }

            Console.WriteLine($"[TerrainCreatorScene] Saved custom terrain '{terrainName}' → {tifPath}");
            RebuildTerrainMesh();
            RebuildSplatTexture();
        }

        public void Export2D(string projectAssetsDir)
        {
            string fbxPath = Path.Combine(projectAssetsDir, "terrain2d.fbx");
            string atlasPath = Path.Combine(projectAssetsDir, "terrain_atlas.png");
            TilemapExporter.ExportToMesh(_heightmap, 0.3f, 0.7f, fbxPath, atlasPath);
            Console.WriteLine($"[TerrainCreatorScene] Exported 2D tilemap to {fbxPath}");
        }

        private void SaveAsPng(string path)
        {
            int w = _terrainWidth;
            int h = _terrainHeight;
            using var bmp = new Bitmap(w, h);
            float range = _maxHeight - _minHeight;
            if (range <= 0) range = 1f;
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float norm = (_heightmap[x, y] - _minHeight) / range;
                    byte gray = (byte)Math.Clamp((int)(norm * 255), 0, 255);
                    bmp.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
                }
            }
            bmp.Save(path, ImageFormat.Png);
        }

        public void SetActiveBrush(ToolChest.Brush brush)
        {
            _activeBrush = brush;
        }

        public ToolChest.Brush GetActiveBrush()
        {
            return _activeBrush;
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _ghostBuffer = new VertexBuffer(_renderContext);
        }

        private void UpdateGhostMesh()
        {
            if (_ghostBuffer == null) return;
            var vertices = new List<float>();
            var indices = new List<uint>();
            int segments = 48;
            float r = Math.Max(_activeBrush?.Size ?? 1f, 1f);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * MathF.PI * 2f / segments;
                float x = MathF.Cos(angle) * r;
                float y = MathF.Sin(angle) * r;
                vertices.Add(x); vertices.Add(y); vertices.Add(0f);
                vertices.Add(0f); vertices.Add(1f); vertices.Add(0f); vertices.Add(1f);
                vertices.Add(0f); vertices.Add(0f);
            }
            for (int i = 0; i < segments; i++)
            {
                indices.Add((uint)i);
                indices.Add((uint)((i + 1) % segments));
            }
            _ghostBuffer.UpdateCustomWithUV(vertices, indices);
        }

        public override void Update(float deltaTime, Vector2 relMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, bool cameraMode)
        {
            base.Update(deltaTime, relMousePos, mouseDown, mousePressed, mouseReleased, cameraMode);

            if (_heightmap == null || _activeBrush == null)
            {
                _ghostVisible = false;
                return;
            }

            Vector3 rayOrigin = _flyCamera.Position;
            Vector3 rayDir = GetLookDirection();
            if (RayTerrainIntersect(rayOrigin, rayDir, out var hit))
            {
                _ghostPosition = hit;
                _ghostPosition.Z += 0.1f;
                _ghostVisible = true;
                UpdateGhostMesh();
            }
            else
            {
                _ghostVisible = false;
            }

            if (mousePressed && _ghostVisible)
            {
                _isBrushing = true;
                _lastBrushUpdateTime = (float)_controlContext.GetTime();
                _lastGhostPosition = _ghostPosition;
            }

            if (mouseDown && _isBrushing && _ghostVisible)
            {
                float currentTime = (float)_controlContext.GetTime();
                float distanceMoved = Vector3.Distance(_ghostPosition, _lastGhostPosition);
                if (currentTime - _lastBrushUpdateTime > BrushUpdateInterval || distanceMoved > BrushMoveThreshold)
                {
                    var evt = new TerrainModifiedEvent(_ghostPosition, _activeBrush.Size, _activeBrush.Intensity * deltaTime,
                        _activeBrush.Mode.ToString().ToLower(), _activeBrush.Shape.ToString(), _activeBrush.Falloff.ToString(), 0, _activeBrush.PaintLayer);
                    _eventBus.Publish(evt, true);

                    _lastBrushUpdateTime = currentTime;
                    _lastGhostPosition = _ghostPosition;
                }
            }

            if (mouseReleased)
            {
                _isBrushing = false;
            }

            if (_sceneData?.Name != null)
            {
                ProjectSettings.Current.StoreUnsavedHeightmap(_sceneData.Name, _heightmap);
            }

            var level = ProjectSettings.Current.CurrentLevel;
            if (level != null && _sceneData != null)
            {
                level.Terrain = _sceneData.Terrain;
            }
        }

        private bool RayTerrainIntersect(Vector3 origin, Vector3 dir, out Vector3 hitPoint)
        {
            hitPoint = Vector3.Zero;
            const float maxDist = 10000f;
            const float step = 1f;
            for (float t = 0; t < maxDist; t += step)
            {
                Vector3 p = origin + dir * t;
                float h = GetHeight(p.X, p.Y);
                if (p.Z <= h)
                {
                    float tLow = t - step;
                    float tHigh = t;
                    for (int i = 0; i < 10; i++)
                    {
                        float tMid = (tLow + tHigh) / 2;
                        p = origin + dir * tMid;
                        h = GetHeight(p.X, p.Y);
                        if (p.Z <= h) tHigh = tMid;
                        else tLow = tMid;
                    }
                    hitPoint = origin + dir * tHigh;
                    return true;
                }
            }
            return false;
        }

        public override void Render(IReadOnlyList<Entity> entities)
        {
            _renderContext.ClearColor(0.05f, 0.08f, 0.15f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.Enable(_renderContext.Enums.DepthTest);

            Matrix4x4 view = _flyCamera.ViewMatrix;
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 180f * 65f, AspectRatio, 0.1f, 50000f);

            _terrainShader.Use();
            _terrainShader.SetMatrix4("uView", view);
            _terrainShader.SetMatrix4("uProjection", projection);
            _terrainShader.SetMatrix4("uModel", Matrix4x4.Identity);
            _terrainBuffer.Bind();

            _terrainShader.SetUniform("uHasTexture", 0);
            _renderContext.DrawElements(_renderContext.Enums.Lines, _terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

            if (_hasColorTexture && _terrainTextureId != 0)
            {
                _terrainShader.SetUniform("uHasTexture", 1);
                _renderContext.ActiveTexture(0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, _terrainTextureId);
                _terrainShader.SetUniform("uTexture", 0);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, _terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            }

            // Splat map (painting)
            if (_hasSplatMap && _splatTextureId != 0)
            {
                _terrainShader.SetUniform("uHasTexture", 1);
                _renderContext.ActiveTexture(0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, _splatTextureId);
                _terrainShader.SetUniform("uTexture", 0);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, _terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            }

            if (_ghostVisible && _ghostBuffer != null)
            {
                _renderContext.Enable(_renderContext.Enums.Blend);
                _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
                _renderContext.Disable(_renderContext.Enums.DepthTest);
                Matrix4x4 model = Matrix4x4.CreateTranslation(_ghostPosition);
                _terrainShader.SetMatrix4("uModel", model);
                _terrainShader.SetUniform("uHasTexture", 0);
                _ghostBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Lines, _ghostBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                _renderContext.Enable(_renderContext.Enums.DepthTest);
                _renderContext.Disable(_renderContext.Enums.Blend);
            }
        }

        private void RebuildSplatTexture()
        {
            if (_paintData == null) return;

            if (_splatTextureId != 0)
                _renderContext.DeleteTexture(_splatTextureId);

            _renderContext.GenTextures(1, out _splatTextureId);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, _splatTextureId);
            _renderContext.PixelStore(_renderContext.Enums.UnpackAlignment, 1);

            byte[] splatBytes = new byte[_paintData.SplatWeights.GetLength(0) * _paintData.SplatWeights.GetLength(1) * 4];
            int idx = 0;
            for (int z = 0; z < _paintData.SplatWeights.GetLength(1); z++)
            {
                for (int x = 0; x < _paintData.SplatWeights.GetLength(0); x++)
                {
                    for (int l = 0; l < 4; l++)
                    {
                        splatBytes[idx++] = (byte)(Math.Clamp(_paintData.SplatWeights[x, z, l], 0f, 1f) * 255f);
                    }
                }
            }

            unsafe
            {
                fixed (byte* ptr = splatBytes)
                {
                    _renderContext.TexImage2D(_renderContext.Enums.Texture2D, 0, _renderContext.Enums.InternalRgba,
                        (uint)_paintData.SplatWeights.GetLength(0), (uint)_paintData.SplatWeights.GetLength(1), 0,
                        _renderContext.Enums.PixelRgba, _renderContext.Enums.UnsignedByte, ptr);
                }
            }

            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMinFilter, _renderContext.Enums.Linear);
            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMagFilter, _renderContext.Enums.Linear);
            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapS, _renderContext.Enums.ClampToEdge);
            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapT, _renderContext.Enums.ClampToEdge);

            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
            _hasSplatMap = true;
        }

        public override void Dispose()
        {
            if (_terrainTextureId != 0)
            {
                _renderContext.DeleteTexture(_terrainTextureId);
                _terrainTextureId = 0;
            }
            if (_splatTextureId != 0)
            {
                _renderContext.DeleteTexture(_splatTextureId);
                _splatTextureId = 0;
            }
            _terrainBuffer?.Dispose();
            _terrainShader?.Dispose();
            _ghostBuffer?.Dispose();
            base.Dispose();
        }

        private void OnTerrainModified(TerrainModifiedEvent e)
        {
            if (_processedModifications.Contains(e.Id)) return;
            ApplyModification(e);
            _processedModifications.Add(e.Id);
        }

        private void ApplyModification(TerrainModifiedEvent e)
        {
            var brush = new ToolChest.Brush
            {
                Mode = (BrushMode)Enum.Parse(typeof(BrushMode), e.Operation, true),
                Shape = (BrushShape)Enum.Parse(typeof(BrushShape), e.Shape, true),
                Falloff = (BrushFalloff)Enum.Parse(typeof(BrushFalloff), e.Falloff, true),
                Size = e.Radius,
                Intensity = e.Strength,
                PaintLayer = e.PaintLayer
            };

            if (brush.Mode == BrushMode.Paint && _paintData != null)
            {
                _paintData.PaintSplat(brush.PaintLayer, new Vector2(e.WorldPos.X, e.WorldPos.Y),
                    brush.Size, brush.Intensity, _worldScaleX, _worldScaleZ,
                    brush.Shape == BrushShape.Circle, brush.Falloff.ToString());
                RebuildSplatTexture();
            }
            else
            {
                brush.Apply(ref _heightmap, new Vector2(e.WorldPos.X, e.WorldPos.Y), _worldScaleX, _worldScaleZ);
                UpdateAffectedVertices(e.WorldPos, e.Radius);
            }
        }

        public new float[,] GetHeightmap() => _heightmap;
    }
}