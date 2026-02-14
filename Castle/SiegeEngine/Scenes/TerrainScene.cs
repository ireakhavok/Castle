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
            if (!_hasColorTexture || !_colorGeoRef.IsValid)
            {
                BuildWireframeMesh(WireframeStep);
                return;
            }

            var vertices = new List<float>();
            var indices = new List<uint>();
            int step = WireframeStep;
            int stepsX = _terrainWidth / step;
            int stepsZ = _terrainHeight / step;

            float tieEast = _colorGeoRef.TiePointModel.X;
            float tieNorth = _colorGeoRef.TiePointModel.Y;
            float scaleEast = _colorGeoRef.PixelScale.X;
            float scaleNorth = _colorGeoRef.PixelScale.Y;

            for (int x = 0; x <= stepsX; x++)
            {
                for (int z = 0; z <= stepsZ; z++)
                {
                    float wx = x * step;
                    float wz = z * step;
                    float y = GetHeight(wx, wz);

                    float u = (wx - tieEast) / (scaleEast * _colorGeoRef.TextureWidth);
                    float v = 1.0f - (wz - tieNorth) / (scaleNorth * _colorGeoRef.TextureHeight);

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
            Console.WriteLine($"[TerrainScene] Rebuilt textured mesh with {vertices.Count / 9} verts, geo-aligned UVs");
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

                // Overlap check between terrain DEM and color texture
                if (_terrainGeoRef.IsValid && _colorGeoRef.IsValid)
                {
                    bool overlaps = !(_colorGeoRef.TiePointModel.X + _colorGeoRef.PixelScale.X * _colorGeoRef.TextureWidth < _terrainGeoRef.TiePointModel.X ||
                                      _colorGeoRef.TiePointModel.X > _terrainGeoRef.TiePointModel.X + _terrainGeoRef.PixelScale.X * _terrainGeoRef.TextureWidth ||
                                      _colorGeoRef.TiePointModel.Y + _colorGeoRef.PixelScale.Y * _colorGeoRef.TextureHeight < _terrainGeoRef.TiePointModel.Y ||
                                      _colorGeoRef.TiePointModel.Y > _terrainGeoRef.TiePointModel.Y + _terrainGeoRef.PixelScale.Y * _terrainGeoRef.TextureHeight);
                    Console.WriteLine($"[TerrainScene] Texture-DEM overlap: {overlaps}");
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

            if (_hasColorTexture && _terrainTextureId != 0)
            {
                // Wireframe lines first (base)
                _terrainShader.SetUniform("uHasTexture", 0);
                _renderContext.DrawElements(_renderContext.Enums.Lines, _terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

                // Textured skin on top (filled triangles, discard outside geo)
                _terrainShader.SetUniform("uHasTexture", 1);
                _renderContext.ActiveTexture(0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, _terrainTextureId);
                _terrainShader.SetUniform("uTexture", 0);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, _terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            }
            else
            {
                // Pure cyan wireframe (no skin)
                _terrainShader.SetUniform("uHasTexture", 0);
                _renderContext.DrawElements(_renderContext.Enums.Lines, _terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
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