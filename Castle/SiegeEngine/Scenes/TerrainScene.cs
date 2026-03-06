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
        protected int WireframeStep = 8;
        protected VertexBuffer _terrainBuffer;
        protected ShaderProgram _terrainShader;
        protected uint _terrainTextureId = 0;
        protected bool _hasColorTexture = false;
        protected GeoTiffParser.GeoReference _colorGeoRef;
        protected GeoTiffParser.GeoReference _terrainGeoRef;
        protected float _worldScaleX = 1.0f;
        protected float _worldScaleZ = 1.0f;
        protected bool _useCustomScale = false;

        // Cached vertex and index data for surgical updates (extracted once)
        protected List<float> _terrainVertices = new List<float>();
        protected List<uint> _terrainIndices = new List<uint>();

        public TerrainScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _flyCamera = new FlyCameraController(controlContext, window);
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _heightmap = new float[_terrainWidth, _terrainHeight];
            _terrainBuffer = new VertexBuffer(_renderContext);
            BuildWireframeMesh(WireframeStep);
            _terrainShader = new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
        }

        protected virtual void BuildWireframeMesh(float step)
        {
            ComputeWorldScale();
            _terrainVertices.Clear();
            _terrainIndices.Clear();

            int stepsX = (int)Math.Floor(_terrainWidth / step);
            int stepsY = (int)Math.Floor(_terrainHeight / step);

            for (int x = 0; x <= stepsX; x++)
            {
                for (int y = 0; y <= stepsY; y++)
                {
                    float wx = x * step * _worldScaleX;
                    float wy = y * step * _worldScaleZ;
                    float z = GetHeight(wx, wy) * VerticalExaggeration;
                    _terrainVertices.Add(wx); _terrainVertices.Add(wy); _terrainVertices.Add(z);
                    _terrainVertices.Add(0.7f); _terrainVertices.Add(0.9f); _terrainVertices.Add(1.0f); _terrainVertices.Add(1.0f);
                    _terrainVertices.Add(0.0f); _terrainVertices.Add(0.0f);
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
                    _terrainIndices.Add(tl); _terrainIndices.Add(tr);
                    _terrainIndices.Add(tl); _terrainIndices.Add(bl);
                }
            }

            _terrainBuffer.UpdateCustomWithUV(_terrainVertices, _terrainIndices);
        }

        protected virtual void BuildTexturedMesh()
        {
            if (!_hasColorTexture || !_colorGeoRef.IsValid || !_terrainGeoRef.IsValid)
            {
                BuildWireframeMesh(WireframeStep);
                return;
            }
            ComputeWorldScale();
            _terrainVertices.Clear();
            _terrainIndices.Clear();

            int step = WireframeStep;
            int stepsX = _terrainWidth / step;
            int stepsY = _terrainHeight / step;

            double tieEastMeters, tieNorthMeters;
            int demZone = 0;
            float scaleEastMeters, scaleNorthMeters;
            if (_terrainGeoRef.IsMeters)
            {
                tieEastMeters = _terrainGeoRef.TiePointModel.X;
                tieNorthMeters = _terrainGeoRef.TiePointModel.Y;
                scaleEastMeters = _terrainGeoRef.PixelScale.X;
                scaleNorthMeters = _terrainGeoRef.PixelScale.Y;
            }
            else
            {
                var (e, n, z) = GeoTiffParser.ConvertLatLonToUTM(
                    _terrainGeoRef.TiePointModel.Y, _terrainGeoRef.TiePointModel.X);
                tieEastMeters = e;
                tieNorthMeters = n;
                demZone = z;
                scaleEastMeters = (float)(_terrainGeoRef.PixelScale.X * 111319.9f * Math.Cos(_terrainGeoRef.TiePointModel.Y * Math.PI / 180.0));
                scaleNorthMeters = _terrainGeoRef.PixelScale.Y * 111319.9f;
            }

            float colorMinEast = _colorGeoRef.MinEast;
            float colorMaxEast = _colorGeoRef.MaxEast;
            float colorMinNorth = _colorGeoRef.MinNorth;
            float colorMaxNorth = _colorGeoRef.MaxNorth;
            float colorExtentEast = colorMaxEast - colorMinEast;
            float colorExtentNorth = colorMaxNorth - colorMinNorth;

            for (int x = 0; x <= stepsX; x++)
            {
                for (int y = 0; y <= stepsY; y++)
                {
                    float wx = x * step * _worldScaleX;
                    float wy = y * step * _worldScaleZ;
                    float z = GetHeight(wx, wy) * VerticalExaggeration;
                    _terrainVertices.Add(wx); _terrainVertices.Add(wy); _terrainVertices.Add(z);
                    _terrainVertices.Add(0.7f); _terrainVertices.Add(0.9f); _terrainVertices.Add(1.0f); _terrainVertices.Add(1.0f);

                    float fracX = (float)x / stepsX;
                    float fracY = (float)y / stepsY;
                    float meshEastMeters, meshNorthMeters;
                    if (_terrainGeoRef.IsMeters)
                    {
                        meshEastMeters = _terrainGeoRef.TiePointModel.X + fracX * (_terrainGeoRef.PixelScale.X * _terrainGeoRef.TextureWidth);
                        meshNorthMeters = _terrainGeoRef.TiePointModel.Y + fracY * (_terrainGeoRef.PixelScale.Y * _terrainGeoRef.TextureHeight);
                    }
                    else
                    {
                        float real_deg_east = _terrainGeoRef.TiePointModel.X + fracX * (_terrainGeoRef.PixelScale.X * _terrainGeoRef.TextureWidth);
                        float real_deg_north = _terrainGeoRef.TiePointModel.Y + fracY * (_terrainGeoRef.PixelScale.Y * _terrainGeoRef.TextureHeight);
                        var (e, n, _) = GeoTiffParser.ConvertLatLonToUTM(real_deg_north, real_deg_east);
                        meshEastMeters = (float)e;
                        meshNorthMeters = (float)n;
                    }
                    float u = (meshEastMeters - colorMinEast) / colorExtentEast;
                    float v = 1.0f - (meshNorthMeters - colorMinNorth) / colorExtentNorth;
                    _terrainVertices.Add(u); _terrainVertices.Add(v);
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
                    _terrainIndices.Add(tl); _terrainIndices.Add(tr); _terrainIndices.Add(bl);
                    _terrainIndices.Add(tr); _terrainIndices.Add(br); _terrainIndices.Add(bl);
                }
            }

            _terrainBuffer.UpdateCustomWithUV(_terrainVertices, _terrainIndices);
        }

        // Surgical replacement of affected vertices only (Z updated from heightmap, UV/color untouched)
        protected void UpdateAffectedVertices(Vector3 worldPos, float radius)
        {
            if (_terrainVertices.Count == 0 || _heightmap == null)
            {
                BuildWireframeMesh(1);
                return;
            }

            int stride = 9;
            float cellSize = Math.Max(_worldScaleX, _worldScaleZ);
            float radiusCells = radius / cellSize + 2f; // padding for safety

            int centerX = (int)Math.Clamp(worldPos.X / _worldScaleX, 0, _terrainWidth - 1);
            int centerZ = (int)Math.Clamp(worldPos.Y / _worldScaleZ, 0, _terrainHeight - 1);

            int minX = Math.Max(0, (int)(centerX - radiusCells));
            int maxX = Math.Min(_terrainWidth, (int)(centerX + radiusCells));
            int minZ = Math.Max(0, (int)(centerZ - radiusCells));
            int maxZ = Math.Min(_terrainHeight, (int)(centerZ + radiusCells));

            int vertsPerRow = (int)Math.Floor(_terrainHeight / 1f) + 1; // step=1 case for editor

            for (int x = minX; x < maxX; x++)
            {
                for (int z = minZ; z < maxZ; z++)
                {
                    int vertexIndex = (x * vertsPerRow + z) * stride + 2; // Z component offset
                    if (vertexIndex + 1 < _terrainVertices.Count)
                    {
                        float wx = x * _worldScaleX;
                        float wy = z * _worldScaleZ;
                        _terrainVertices[vertexIndex] = GetHeight(wx, wy) * VerticalExaggeration;
                    }
                }
            }

            // Surgical GPU upload of updated vertex data (no index rebuild, no full regeneration)
            _terrainBuffer.UpdateCustomWithUV(_terrainVertices, _terrainIndices);
        }

        private void ComputeWorldScale()
        {
            if (_useCustomScale)
            {
                return;
            }
            if (_terrainGeoRef == null || !_terrainGeoRef.IsValid)
            {
                _worldScaleX = _worldScaleZ = 10.0f;
                return;
            }
            if (_terrainGeoRef.IsMeters)
            {
                _worldScaleX = Math.Abs(_terrainGeoRef.PixelScale.X);
                _worldScaleZ = Math.Abs(_terrainGeoRef.PixelScale.Y);
            }
            else
            {
                double lat = _terrainGeoRef.TiePointModel.Y;
                _worldScaleX = (float)(Math.Abs(_terrainGeoRef.PixelScale.X) * 111319.9 * Math.Cos(lat * Math.PI / 180.0));
                _worldScaleZ = (float)(Math.Abs(_terrainGeoRef.PixelScale.Y) * 111319.9);
            }
        }

        protected float GetHeight(float x, float y)
        {
            int ix = (int)Math.Clamp(x / _worldScaleX, 0, _terrainWidth - 1);
            int iy = (int)Math.Clamp(y / _worldScaleZ, 0, _terrainHeight - 1);
            return _heightmap[ix, iy];
        }

        public virtual void LoadTerrain(string path)
        {
            Console.WriteLine($"[TerrainScene] Loading terrain from {path}");
            try
            {
                bool isCustomFlat;
                float customScaleX, customScaleZ;
                _heightmap = TerrainManager.LoadTerrain(path, out _terrainWidth, out _terrainHeight, out _minHeight, out _maxHeight, out isCustomFlat, out customScaleX, out customScaleZ);
                _terrainGeoRef = GeoTiffParser.ParseGeoReference(path);
                Console.WriteLine($"[TerrainScene] Heightmap loaded: {_terrainWidth}x{_terrainHeight}, Height range: {_minHeight:F1} to {_maxHeight:F1}");
                float avgScale = (_worldScaleX + _worldScaleZ) / 2f;
                WireframeStep = avgScale > 5f ? 8 : avgScale > 2f ? 4 : 1;
                Console.WriteLine($"[TerrainScene] Adjusted wireframe step to {WireframeStep} based on scale ~{avgScale:F1}m/cell");
                if (isCustomFlat)
                {
                    _worldScaleX = customScaleX;
                    _worldScaleZ = customScaleZ;
                    _useCustomScale = true;
                    BuildWireframeMesh(1);
                    float centerX = ((_terrainWidth - 1) * _worldScaleX) * 0.5f;
                    float centerY = ((_terrainHeight - 1) * _worldScaleZ) * 0.5f;
                    float centerHeight = GetHeight(centerX, centerY);
                    _flyCamera.Position = new Vector3(centerX, centerY + 8f, centerHeight + 5f);
                    _flyCamera.Yaw = 0f;
                    _flyCamera.Pitch = -0.85f;
                }
                else
                {
                    ComputeWorldScale();
                    BuildWireframeMesh(WireframeStep);
                    float centerX = (_terrainWidth * _worldScaleX) / 2f;
                    float centerY = (_terrainHeight * _worldScaleZ) / 2f;
                    _flyCamera.Position = new Vector3(centerX, centerY + 5000, _maxHeight * 1.5f);
                }
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
                _colorGeoRef = GeoTiffParser.ParseGeoReference(path);
                _hasColorTexture = _colorGeoRef.IsValid;
                if (_terrainGeoRef.IsValid && _colorGeoRef.IsValid)
                {
                    bool overlaps = !(_colorGeoRef.MaxEast < _terrainGeoRef.MinEast ||
                                      _colorGeoRef.MinEast > _terrainGeoRef.MaxEast ||
                                      _colorGeoRef.MaxNorth < _terrainGeoRef.MinNorth ||
                                      _colorGeoRef.MinNorth > _terrainGeoRef.MaxNorth);
                    Console.WriteLine($"[TerrainScene] Texture-DEM overlap (exact bounds): {overlaps}");
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