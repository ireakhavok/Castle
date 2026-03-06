// Folder: MapRoom
// File: TerrainCreatorScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Terrain;
using SiegeEngine.Scenes;
using ToolChest;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
namespace MapRoom
{
    public unsafe class TerrainCreatorScene : TerrainScene
    {
        private ToolChest.Brush _activeBrush = new ToolChest.Brush();
        private Vector3 _ghostPosition;
        private bool _ghostVisible = false;
        private VertexBuffer _ghostBuffer;

        public TerrainCreatorScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
        }
        public void CreateBlank()
        {
            _heightmap = new float[_terrainWidth, _terrainHeight];
            _minHeight = float.MaxValue;
            _maxHeight = float.MinValue;
            for (int x = 0; x < _terrainWidth; x++)
            {
                for (int z = 0; z < _terrainHeight; z++)
                {
                    float h = 0f;
                    _heightmap[x, z] = h;
                    if (h < _minHeight) _minHeight = h;
                    if (h > _maxHeight) _maxHeight = h;
                }
            }
            Console.WriteLine($"[TerrainCreatorScene] Created blank {_terrainWidth}×{_terrainHeight} terrain with height range {_minHeight:F1} to {_maxHeight:F1}");
            _useCustomScale = true;
            BuildWireframeMesh(1);
            float centerX = (_terrainWidth * _worldScaleX) / 2f;
            float centerZ = (_terrainHeight * _worldScaleZ) / 2f;
            _flyCamera.Position = new Vector3(centerX, _maxHeight * 1.5f + 10f, centerZ + 50f);
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
            int numCellsZ = (int)Math.Ceiling(parameters.Depth / cellSize);
            _terrainWidth = numCellsX + 1;
            _terrainHeight = numCellsZ + 1;
            _worldScaleX = cellSize;
            _worldScaleZ = cellSize;
            if (!string.IsNullOrEmpty(parameters.ImportPath))
            {
                Console.WriteLine($"[TerrainCreatorScene] Loading GeoTIFF: {parameters.ImportPath}");
                LoadTerrain(parameters.ImportPath);
            }
            else
            {
                _heightmap = new float[_terrainWidth, _terrainHeight];
                _minHeight = parameters.InitialHeight;
                _maxHeight = parameters.InitialHeight;
                for (int x = 0; x < _terrainWidth; x++)
                {
                    for (int z = 0; z < _terrainHeight; z++)
                    {
                        _heightmap[x, z] = parameters.InitialHeight * parameters.VerticalExaggeration;
                    }
                }
                Console.WriteLine($"[TerrainCreatorScene] SUCCESS: Created {parameters.Width}m × {parameters.Depth}m terrain");
                _useCustomScale = true;
                BuildWireframeMesh(1);
                float centerX = (_terrainWidth * _worldScaleX) / 2f;
                float centerZ = (_terrainHeight * _worldScaleZ) / 2f;
                _flyCamera.Position = new Vector3(centerX, _maxHeight * 1.5f + 10f, centerZ + 50f);
                _flyCamera.Yaw = 0f;
                _flyCamera.Pitch = -MathF.PI / 6f;
            }
        }
        public override void LoadTerrain(string path)
        {
            base.LoadTerrain(path);
        }
        public void SetColorTexture(string path)
        {
            base.SetColorTexture(path);
        }
        public void SaveTerrain(string terrainName)
        {
            if (string.IsNullOrEmpty(terrainName))
                terrainName = "UntitledTerrain";
            string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terrain", "Saved");
            Directory.CreateDirectory(saveDir);
            string tifPath = Path.Combine(saveDir, terrainName + ".tif");
            string pngPath = Path.Combine(saveDir, terrainName + ".png");
            SaveAsPng(pngPath);
            CustomTerrainParser.SaveFloatTiff(tifPath, _heightmap, _worldScaleX, _worldScaleZ);
            Console.WriteLine($"[TerrainCreatorScene] Saved terrain '{terrainName}'");
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
                for (int z = 0; z < h; z++)
                {
                    float norm = (_heightmap[x, z] - _minHeight) / range;
                    byte gray = (byte)Math.Clamp((int)(norm * 255), 0, 255);
                    bmp.SetPixel(x, z, Color.FromArgb(gray, gray, gray));
                }
            }
            bmp.Save(path, ImageFormat.Png);
        }
        public void SetActiveBrush(ToolChest.Brush brush)
        {
            _activeBrush = brush ?? new ToolChest.Brush();
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
            float r = Math.Max(_activeBrush.Size, 1f);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * MathF.PI * 2f / segments;
                float x = MathF.Cos(angle) * r;
                float z = MathF.Sin(angle) * r;
                vertices.Add(x); vertices.Add(0f); vertices.Add(z);
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
            if (_heightmap == null)
            {
                _ghostVisible = false;
                return;
            }
            Vector3 rayOrigin = _flyCamera.Position;
            Vector3 rayDir = GetLookDirection();
            if (MathF.Abs(rayDir.Y) > 0.001f)
            {
                float t = -rayOrigin.Y / rayDir.Y;
                if (t > 0.1f)
                {
                    _ghostPosition = rayOrigin + rayDir * t;
                    float terrainHeight = GetHeight(_ghostPosition.X, _ghostPosition.Z);
                    _ghostPosition.Y = terrainHeight + 0.1f;
                    _ghostVisible = true;
                    UpdateGhostMesh();
                }
            }
            if (mouseDown && _activeBrush != null && _ghostVisible)
            {
                Vector2 gridPos = new Vector2(_ghostPosition.X, _ghostPosition.Z);
                _activeBrush.Apply(ref _heightmap, gridPos, _worldScaleX, _worldScaleZ);
                BuildWireframeMesh(1);
            }
        }
        private Vector3 GetLookDirection()
        {
            float yawRad = _flyCamera.Yaw * (MathF.PI / 180f);
            float pitchRad = _flyCamera.Pitch * (MathF.PI / 180f);
            return Vector3.Normalize(new Vector3(
                MathF.Sin(yawRad) * MathF.Cos(pitchRad),
                MathF.Sin(pitchRad),
                MathF.Cos(yawRad) * MathF.Cos(pitchRad)
            ));
        }
        public override void Render(IReadOnlyList<Entity> entities)
        {
            _renderContext.ClearColor(0.05f, 0.08f, 0.15f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            Matrix4x4 view = _flyCamera.ViewMatrix;
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 180f * 65f, (float)_width / _height, 0.1f, 50000f);
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
        public override void Dispose()
        {
            if (_terrainTextureId != 0)
            {
                _renderContext.DeleteTexture(_terrainTextureId);
                _terrainTextureId = 0;
            }
            _terrainBuffer?.Dispose();
            _terrainShader?.Dispose();
            _ghostBuffer?.Dispose();
            base.Dispose();
        }
    }
}