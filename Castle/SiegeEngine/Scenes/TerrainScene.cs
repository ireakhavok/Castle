// Folder: SiegeEngine/Scenes
// File: TerrainScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.Terrain;
using SiegeEngine.PlayerSystem;
using System;
using System.Collections.Generic;
using System.Numerics;
namespace SiegeEngine.Scenes
{
    public unsafe class TerrainScene : Scene
    {
        protected FlyCameraController _flyCamera;
        protected float[,] _heightmap;
        protected int _terrainWidth = 2048;
        protected int _terrainHeight = 2048;
        protected float _minHeight = 0;
        protected float _maxHeight = 0;
        protected const float VerticalExaggeration = 1.0f;
        protected const int WireframeStep = 8;
        protected VertexBuffer _terrainBuffer;
        protected ShaderProgram _terrainShader;
        protected uint _terrainTextureId = 0;
        protected bool _hasColorTexture = false;
        protected TerrainParser.GeoReference _colorGeoRef;
        protected TerrainParser.GeoReference _terrainGeoRef;
        public TerrainScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _flyCamera = new FlyCameraController(controlContext, window);
            _flyCamera.Position = new Vector3(_terrainWidth / 2f, -1000, 500);
            _flyCamera.Yaw = 0f;
            _flyCamera.Pitch = -MathF.PI / 6f;
        }
        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _heightmap = new float[_terrainWidth, _terrainHeight];
            _terrainBuffer = new VertexBuffer(_renderContext);
            BuildWireframeMesh(WireframeStep);
            _terrainShader = new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
        }
        protected virtual void BuildWireframeMesh(int step)
        {
            var vertices = new List<float>();
            var indices = new List<uint>();
            int stepsX = _terrainWidth / step;
            int stepsZ = _terrainHeight / step;
            for (int x = 0; x <= stepsX; x++)
            {
                for (int z = 0; z <= stepsZ; z++)
                {
                    float wx = x * step;
                    float wz = z * step;
                    float y = GetHeight(wx, wz);
                    vertices.Add(wx); vertices.Add(wz); vertices.Add(y);
                    vertices.Add(0.7f); vertices.Add(0.9f); vertices.Add(1.0f); vertices.Add(1.0f);
                    vertices.Add(0.0f); vertices.Add(0.0f);
                }
            }
            for (int x = 0; x < stepsX; x++)
            {
                for (int z = 0; z < stepsZ; z++)
                {
                    uint tl = (uint)(x * (stepsZ + 1) + z);
                    uint tr = tl + 1;
                    uint bl = tl + (uint)(stepsZ + 1);
                    uint br = bl + 1;
                    indices.Add(tl); indices.Add(tr);
                    indices.Add(tl); indices.Add(bl);
                }
            }
            _terrainBuffer.UpdateCustomWithUV(vertices, indices);
        }
        protected virtual void BuildTexturedMesh()
        {
            if (!_hasColorTexture || !_colorGeoRef.IsValid || !_terrainGeoRef.IsValid)
            {
                BuildWireframeMesh(WireframeStep);
                return;
            }
            var vertices = new List<float>();
            var indices = new List<uint>();
            int step = WireframeStep;
            int stepsX = _terrainWidth / step;
            int stepsZ = _terrainHeight / step;
            // NEW: Convert DEM tiepoint to UTM meters (matches NAIP's CRS)
            var (tieEastMeters, tieNorthMeters, demZone) = TerrainParser.ConvertLatLonToUTM(
                _terrainGeoRef.TiePointModel.Y, _terrainGeoRef.TiePointModel.X);
            float scaleEastMeters = _terrainGeoRef.PixelScale.X * 111000f * (float)Math.Cos(_terrainGeoRef.TiePointModel.Y * Math.PI / 180.0);
            float scaleNorthMeters = _terrainGeoRef.PixelScale.Y * 111000f;
            // Color is already in meters (UTM)
            float tieEast = _colorGeoRef.TiePointModel.X;
            float tieNorth = _colorGeoRef.TiePointModel.Y;
            float scaleEast = _colorGeoRef.PixelScale.X;
            float scaleNorth = _colorGeoRef.PixelScale.Y;
            bool overlaps = !(_colorGeoRef.TiePointModel.X + _colorGeoRef.PixelScale.X * _colorGeoRef.TextureWidth < tieEastMeters ||
                              _colorGeoRef.TiePointModel.X > tieEastMeters + scaleEastMeters * _terrainGeoRef.TextureWidth ||
                              _colorGeoRef.TiePointModel.Y + _colorGeoRef.PixelScale.Y * _colorGeoRef.TextureHeight < tieNorthMeters ||
                              _colorGeoRef.TiePointModel.Y > tieNorthMeters + scaleNorthMeters * _terrainGeoRef.TextureHeight);
            // DETAILED DIAGNOSTIC LOGS
            Console.WriteLine($"[TerrainScene] DEM converted to UTM (Zone {demZone}): East [{tieEastMeters:F1}-{tieEastMeters + scaleEastMeters * _terrainGeoRef.TextureWidth:F1}], North [{tieNorthMeters:F1}-{tieNorthMeters + scaleNorthMeters * _terrainGeoRef.TextureHeight:F1}]");
            Console.WriteLine($"[TerrainScene] Color (meters): East [{tieEast:F1}-{tieEast + scaleEast * _colorGeoRef.TextureWidth:F1}], North [{tieNorth:F1}-{tieNorth + scaleNorth * _colorGeoRef.TextureHeight:F1}]");
            Console.WriteLine($"[TerrainScene] Overlap: {overlaps}");
            float minU = float.MaxValue, maxU = float.MinValue;
            float minV = float.MaxValue, maxV = float.MinValue;
            for (int x = 0; x <= stepsX; x++)
            {
                for (int z = 0; z <= stepsZ; z++)
                {
                    float wx = x * step;
                    float wz = z * step;
                    float y = GetHeight(wx, wz);
                    // Convert mesh grid point (pixel index) to real degrees from DEM geo
                    float real_deg_east = _terrainGeoRef.TiePointModel.X + (wx / _terrainGeoRef.TextureWidth) * (_terrainGeoRef.PixelScale.X * _terrainGeoRef.TextureWidth);
                    float real_deg_north = _terrainGeoRef.TiePointModel.Y + (wz / _terrainGeoRef.TextureHeight) * (_terrainGeoRef.PixelScale.Y * _terrainGeoRef.TextureHeight);
                    // Convert to UTM meters (common space with color texture)
                    var (meshEastMeters, meshNorthMeters, _) = TerrainParser.ConvertLatLonToUTM(real_deg_north, real_deg_east);
                    float u = (float)(meshEastMeters - tieEast) / (scaleEast * _colorGeoRef.TextureWidth);
                    float v = 1.0f - (float)(meshNorthMeters - tieNorth) / (scaleNorth * _colorGeoRef.TextureHeight);
                    minU = Math.Min(minU, u);
                    maxU = Math.Max(maxU, u);
                    minV = Math.Min(minV, v);
                    maxV = Math.Max(maxV, v);
                    vertices.Add(wx); vertices.Add(wz); vertices.Add(y);
                    vertices.Add(0.7f); vertices.Add(0.9f); vertices.Add(1.0f); vertices.Add(1.0f);
                    vertices.Add(u); vertices.Add(v);
                }
            }
            for (int x = 0; x < stepsX; x++)
            {
                for (int z = 0; z < stepsZ; z++)
                {
                    uint tl = (uint)(x * (stepsZ + 1) + z);
                    uint tr = tl + 1;
                    uint bl = tl + (uint)(stepsZ + 1);
                    uint br = bl + 1;
                    indices.Add(tl); indices.Add(tr); indices.Add(bl);
                    indices.Add(tr); indices.Add(br); indices.Add(bl);
                }
            }
            _terrainBuffer.UpdateCustomWithUV(vertices, indices);
            Console.WriteLine($"[TerrainScene] Rebuilt textured mesh with {vertices.Count / 9} verts, REAL geo-aligned UVs");
            Console.WriteLine($"[TerrainScene] UV range: U [{minU:F3}-{maxU:F3}], V [{minV:F3}-{maxV:F3}]");
        }
        protected float GetHeight(float x, float z)
        {
            int ix = (int)Math.Clamp(x, 0, _terrainWidth - 1);
            int iz = (int)Math.Clamp(z, 0, _terrainHeight - 1);
            return _heightmap[ix, iz];
        }
        public virtual void LoadTerrain(string path)
        {
            Console.WriteLine($"[TerrainScene] Loading terrain from {path}");
            try
            {
                _heightmap = TerrainParser.LoadUSGSDEM(path, out _terrainWidth, out _terrainHeight, out _minHeight, out _maxHeight);
                _terrainGeoRef = TerrainParser.ParseGeoReference(path);
                Console.WriteLine($"[TerrainScene] Heightmap loaded: {_terrainWidth}x{_terrainHeight}, Height range: {_minHeight:F1} to {_maxHeight:F1}");
                BuildWireframeMesh(WireframeStep);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TerrainScene] Failed to load TIFF: {ex.Message}");
            }
        }
        public void SetColorTexture(string path)
        {
            _terrainTextureId = TerrainTextureParser.LoadColorTexture(_renderContext, path);
            if (_terrainTextureId != 0)
            {
                _colorGeoRef = TerrainParser.ParseGeoReference(path);
                _hasColorTexture = _colorGeoRef.IsValid;
                if (_terrainGeoRef.IsValid && _colorGeoRef.IsValid)
                {
                    bool overlaps = !(_colorGeoRef.TiePointModel.X + _colorGeoRef.PixelScale.X * _colorGeoRef.TextureWidth < _terrainGeoRef.TiePointModel.X ||
                                      _colorGeoRef.TiePointModel.X > _terrainGeoRef.TiePointModel.X + _terrainGeoRef.PixelScale.X * _terrainGeoRef.TextureWidth ||
                                      _colorGeoRef.TiePointModel.Y + _colorGeoRef.PixelScale.Y * _colorGeoRef.TextureHeight < _terrainGeoRef.TiePointModel.Y ||
                                      _colorGeoRef.TiePointModel.Y > _terrainGeoRef.TiePointModel.Y + _terrainGeoRef.PixelScale.Y * _terrainGeoRef.TextureHeight);
                    Console.WriteLine($"[TerrainScene] Texture-DEM overlap (pre-conversion): {overlaps}");
                }
                Console.WriteLine($"[TerrainScene] Color texture loaded: {path} (geo valid: {_hasColorTexture})");
                BuildTexturedMesh();
            }
        }
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            _flyCamera.Update(deltaTime, 0f, true);
        }
        public virtual void Update(float deltaTime, Vector2 relMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, bool cameraMode)
        {
            base.Update(deltaTime);
            _flyCamera.Update(deltaTime, 0f, cameraMode);
        }
        public override void Render(IReadOnlyList<Entity> entities)
        {
            _renderContext.ClearColor(0.05f, 0.08f, 0.15f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            Matrix4x4 view = _flyCamera.ViewMatrix;
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, (float)_width / _height, 0.1f, 50000f);
            _terrainShader.Use();
            _terrainShader.SetMatrix4("uView", view);
            _terrainShader.SetMatrix4("uProjection", projection);
            _terrainShader.SetMatrix4("uModel", Matrix4x4.Identity);
            _terrainBuffer.Bind();
            // Base: cyan wireframe lines (always)
            _terrainShader.SetUniform("uHasTexture", 0);
            _renderContext.DrawElements(_renderContext.Enums.Lines, _terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            // Skin: filled triangles ONLY where UV in [0,1] (no rusty fill)
            if (_hasColorTexture && _terrainTextureId != 0)
            {
                _terrainShader.SetUniform("uHasTexture", 1);
                _renderContext.ActiveTexture(0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, _terrainTextureId);
                _terrainShader.SetUniform("uTexture", 0);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, _terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
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
            base.Dispose();
        }
    }
}