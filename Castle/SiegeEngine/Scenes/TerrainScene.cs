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
    public unsafe class TerrainScene : GameScene
    {
        protected FlyCameraController _flyCamera;
        protected float[,] _heightmap;
        protected int _terrainWidth = 200;
        protected int _terrainHeight = 200;
        protected float _minHeight = 0;
        protected float _maxHeight = 0;
        protected const float VerticalExaggeration = 1.0f;
        protected int WireframeStep = 1;
        protected VertexBuffer _terrainBuffer;
        protected ShaderProgram _terrainShader;
        protected uint _terrainTextureId = 0;
        protected bool _hasColorTexture = false;
        protected GeoTiffParser.GeoReference _colorGeoRef;
        protected GeoTiffParser.GeoReference _terrainGeoRef;
        protected float _worldScaleX = 1.0f;
        protected float _worldScaleZ = 1.0f;
        protected bool _useCustomScale = false;
        protected List<float> _terrainVertices = new List<float>();
        protected List<uint> _terrainIndices = new List<uint>();
        protected int _meshVertsX = 0;
        protected int _meshVertsY = 0;
        protected int _currentMeshStep = 1;
        protected bool _isEditorContext = false;

        protected ISceneStateProvider _liveState;

        public TerrainScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus, SceneData sceneData = null)
            : base(renderContext, controlContext, window, server, eventBus, sceneData)
        {
            _flyCamera = new FlyCameraController(controlContext, window);
            _terrainGeoRef = new GeoTiffParser.GeoReference { IsValid = false };
            _colorGeoRef = new GeoTiffParser.GeoReference { IsValid = false };
        }

        // Step 2 fix: public (accessible from Keystone and MapRoom)
        public virtual void BindLiveState(ISceneStateProvider liveState)
        {
            _liveState = liveState;
        }

        protected virtual void SyncFromLiveState()
        {
            if (_liveState != null)
            {
                // placeholder for later steps
            }
        }

        public override void LoadSceneData(SceneData data)
        {
            base.LoadSceneData(data);
            _terrainGeoRef = new GeoTiffParser.GeoReference { IsValid = false };
            _colorGeoRef = new GeoTiffParser.GeoReference { IsValid = false };
            _hasColorTexture = false;
            _terrainTextureId = 0;
            _useCustomScale = false;
            _heightmap = null;
            if (data?.Terrain != null)
            {
                if (!string.IsNullOrEmpty(data.Terrain.HeightmapPath))
                {
                    LoadTerrain(data.Terrain.HeightmapPath);
                }
                else if (!string.IsNullOrEmpty(data.Terrain.ColorTexturePath))
                {
                    SetColorTexture(data.Terrain.ColorTexturePath);
                }
                else
                {
                    InitializeBlankTerrain();
                }
            }
            else
            {
                InitializeBlankTerrain();
            }
        }

        private void InitializeBlankTerrain()
        {
            _terrainWidth = 200;
            _terrainHeight = 200;
            _heightmap = new float[_terrainWidth, _terrainHeight];
            _minHeight = 0;
            _maxHeight = 0;
            for (int x = 0; x < _terrainWidth; x++)
                for (int y = 0; y < _terrainHeight; y++)
                    _heightmap[x, y] = 0f;
            _useCustomScale = true;
            BuildWireframeMesh(1);
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _terrainBuffer = new VertexBuffer(_renderContext);
            _terrainShader = new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
        }

        protected virtual void BuildWireframeMesh(float step)
        {
            float effectiveStep = _isEditorContext ? 1f : step;
            ComputeWorldScale();
            _terrainVertices.Clear();
            _terrainIndices.Clear();
            _currentMeshStep = (int)effectiveStep;
            int stepsX = (int)Math.Floor(_terrainWidth / effectiveStep);
            int stepsY = (int)Math.Floor(_terrainHeight / effectiveStep);
            _meshVertsX = stepsX + 1;
            _meshVertsY = stepsY + 1;
            for (int x = 0; x <= stepsX; x++)
            {
                for (int y = 0; y <= stepsY; y++)
                {
                    float wx = x * effectiveStep * _worldScaleX;
                    float wy = y * effectiveStep * _worldScaleZ;
                    float z = GetHeight(wx, wy) * VerticalExaggeration;
                    _terrainVertices.Add(wx); _terrainVertices.Add(wy); _terrainVertices.Add(z);
                    _terrainVertices.Add(0.7f); _terrainVertices.Add(0.9f); _terrainVertices.Add(1.0f); _terrainVertices.Add(1.0f);
                    float u = (float)x / stepsX;
                    float v = (float)y / stepsY;
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

        public virtual void RebuildTerrainMesh()
        {
            if (_heightmap == null) return;
            BuildWireframeMesh(1);
        }

        protected virtual void BuildTexturedMesh()
        {
            if (!_hasColorTexture || _colorGeoRef == null || !_colorGeoRef.IsValid || _terrainGeoRef == null || !_terrainGeoRef.IsValid)
            {
                BuildWireframeMesh(WireframeStep);
                return;
            }
            ComputeWorldScale();
            _terrainVertices.Clear();
            _terrainIndices.Clear();
            _currentMeshStep = WireframeStep;
            int step = WireframeStep;
            int stepsX = _terrainWidth / step;
            int stepsY = _terrainHeight / step;
            _meshVertsX = stepsX + 1;
            _meshVertsY = stepsY + 1;
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

        protected void UpdateAffectedVertices(Vector3 worldPos, float radius)
        {
            if (_terrainVertices.Count == 0 || _heightmap == null || _currentMeshStep < 1 || _meshVertsX == 0)
            {
                RebuildTerrainMesh();
                return;
            }
            float worldCellSize = Math.Max(_worldScaleX, _worldScaleZ);
            float radiusInMeshCells = (radius / worldCellSize) / _currentMeshStep + 2f;
            int centerMeshX = (int)Math.Clamp(worldPos.X / (_worldScaleX * _currentMeshStep), 0, _meshVertsX - 1);
            int centerMeshY = (int)Math.Clamp(worldPos.Y / (_worldScaleZ * _currentMeshStep), 0, _meshVertsY - 1);
            int minMeshX = Math.Max(0, (int)(centerMeshX - radiusInMeshCells));
            int maxMeshX = Math.Min(_meshVertsX - 1, (int)(centerMeshX + radiusInMeshCells));
            int minMeshY = Math.Max(0, (int)(centerMeshY - radiusInMeshCells));
            int maxMeshY = Math.Min(_meshVertsY - 1, (int)(centerMeshY + radiusInMeshCells));
            const int stride = 9;
            for (int mx = minMeshX; mx <= maxMeshX; mx++)
            {
                for (int my = minMeshY; my <= maxMeshY; my++)
                {
                    int vertexIndex = (mx * _meshVertsY + my) * stride + 2;
                    if (vertexIndex + 1 < _terrainVertices.Count)
                    {
                        float wx = mx * _currentMeshStep * _worldScaleX;
                        float wy = my * _currentMeshStep * _worldScaleZ;
                        _terrainVertices[vertexIndex] = GetHeight(wx, wy) * VerticalExaggeration;
                    }
                }
            }
            for (int mx = minMeshX; mx <= maxMeshX; mx++)
            {
                int rowStartVertex = mx * _meshVertsY + minMeshY;
                int rowVertexCount = maxMeshY - minMeshY + 1;
                _terrainBuffer.UpdateVerticesPartial(_terrainVertices, rowStartVertex, rowVertexCount, 9);
            }
        }

        private void ComputeWorldScale()
        {
            if (_terrainGeoRef != null && _terrainGeoRef.IsValid)
            {
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
                _useCustomScale = false;
                return;
            }
            if (_useCustomScale) return;
            _worldScaleX = _worldScaleZ = 1.0f;
        }

        protected float GetHeight(float x, float y)
        {
            int ix = (int)Math.Clamp(x / _worldScaleX, 0, _terrainWidth - 1);
            int iy = (int)Math.Clamp(y / _worldScaleZ, 0, _terrainHeight - 1);
            return _heightmap[ix, iy];
        }

        protected float GetInterpolatedHeight(float worldX, float worldY)
        {
            if (_heightmap == null) return 0f;
            float fx = worldX / _worldScaleX;
            float fy = worldY / _worldScaleZ;
            int x0 = (int)Math.Clamp(Math.Floor(fx), 0, _terrainWidth - 1);
            int y0 = (int)Math.Clamp(Math.Floor(fy), 0, _terrainHeight - 1);
            int x1 = Math.Min(x0 + 1, _terrainWidth - 1);
            int y1 = Math.Min(y0 + 1, _terrainHeight - 1);
            float tx = fx - x0;
            float ty = fy - y0;
            float h00 = _heightmap[x0, y0];
            float h10 = _heightmap[x1, y0];
            float h01 = _heightmap[x0, y1];
            float h11 = _heightmap[x1, y1];
            float h0 = h00 * (1 - tx) + h10 * tx;
            float h1 = h01 * (1 - tx) + h11 * tx;
            return h0 * (1 - ty) + h1 * ty;
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
                if (isCustomFlat)
                {
                    _worldScaleX = customScaleX;
                    _worldScaleZ = customScaleZ;
                    _useCustomScale = true;
                }
                else
                {
                    ComputeWorldScale();
                }
                BuildWireframeMesh(1);
                float centerX = (_terrainWidth * _worldScaleX) / 2f;
                float centerY = (_terrainHeight * _worldScaleZ) / 2f;
                _flyCamera.Position = new Vector3(centerX, centerY + 50f, _maxHeight * 1.5f + 10f);
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
                BuildTexturedMesh();
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            _flyCamera.Update(deltaTime, 0f, true);
            SyncFromLiveState();
        }

        public virtual void Update(float deltaTime, Vector2 relMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, bool cameraMode)
        {
            base.Update(deltaTime);
            _flyCamera.Update(deltaTime, 0f, cameraMode);
            SyncFromLiveState();
        }

        public bool GetMouseRay(Vector2 normalizedMouse, float viewportWidth, float viewportHeight, out Vector3 rayOrigin, out Vector3 rayDir)
        {
            rayOrigin = Vector3.Zero;
            rayDir = Vector3.Zero;
            if (_flyCamera == null) return false;
            float aspect = viewportWidth / viewportHeight;
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 180f * 65f, aspect, 0.1f, 50000f);
            Matrix4x4 view = _flyCamera.ViewMatrix;
            if (!Matrix4x4.Invert(proj, out Matrix4x4 invProj)) return false;
            if (!Matrix4x4.Invert(view, out Matrix4x4 invView)) return false;
            float ndcX = normalizedMouse.X * 2f - 1f;
            float ndcY = 1f - normalizedMouse.Y * 2f;
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

        public float[,] GetHeightmap() => _heightmap;
    }
}