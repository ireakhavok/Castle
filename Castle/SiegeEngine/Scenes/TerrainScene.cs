// Folder: SiegeEngine.Scenes
// File: TerrainScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.PlayerSystem;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Scenes
{
    public unsafe class TerrainScene : Scene
    {
        protected CameraController _flyCamera;
        protected float[,] _heightmap;
        protected const int TerrainResolution = 2048;
        protected const float MeterPerUnit = 1.0f;

        protected VertexBuffer _terrainBuffer;
        protected ShaderProgram _terrainShader;

        public TerrainScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _flyCamera = new CameraController(controlContext, window, null);
        }

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);

            _heightmap = new float[TerrainResolution, TerrainResolution];
            for (int x = 0; x < TerrainResolution; x++)
                for (int z = 0; z < TerrainResolution; z++)
                    _heightmap[x, z] = 0f;

            _terrainBuffer = new VertexBuffer(_renderContext);
            BuildDebugTerrainMesh();

            _terrainShader = new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
        }

        protected virtual void BuildDebugTerrainMesh()
        {
            var vertices = new List<Vertex>();
            var indices = new List<uint>();
            int steps = 64;
            float stepSize = TerrainResolution / (float)steps;

            for (int x = 0; x <= steps; x++)
            {
                for (int z = 0; z <= steps; z++)
                {
                    float wx = x * stepSize;
                    float wz = z * stepSize;
                    float y = GetHeight(wx, wz);
                    vertices.Add(new Vertex(wx, y, wz, 0.4f, 0.8f, 0.4f, 1.0f));
                }
            }

            for (int x = 0; x < steps; x++)
            {
                for (int z = 0; z < steps; z++)
                {
                    uint tl = (uint)(x * (steps + 1) + z);
                    uint tr = tl + 1;
                    uint bl = tl + (uint)(steps + 1);
                    uint br = bl + 1;

                    indices.Add(tl); indices.Add(tr);
                    indices.Add(tl); indices.Add(bl);
                }
            }

            _terrainBuffer.UpdateCustom(vertices, indices);
        }

        protected float GetHeight(float x, float z)
        {
            int ix = (int)Math.Clamp(x / MeterPerUnit, 0, TerrainResolution - 1);
            int iz = (int)Math.Clamp(z / MeterPerUnit, 0, TerrainResolution - 1);
            return _heightmap[ix, iz];
        }

        public virtual void LoadTerrain(string path)
        {
            Console.WriteLine($"[TerrainScene] Loading terrain from {path}");
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            _flyCamera.Update(deltaTime, 0f, true);
        }

        public virtual void Update(float deltaTime, Vector2 relMousePos, bool mouseDown, bool mousePressed, bool mouseReleased)
        {
            base.Update(deltaTime);
            _flyCamera.Update(deltaTime, 0f, true);
        }

        public override void Render(IReadOnlyList<Entity> entities)
        {
            _renderContext.ClearColor(0.15f, 0.25f, 0.4f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.Enable(_renderContext.Enums.DepthTest);

            Matrix4x4 view = _flyCamera.ViewMatrix;
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, (float)_width / _height, 0.1f, 10000f);

            _terrainShader.Use();
            _terrainShader.SetMatrix4("uView", view);          // Fixed order: name first
            _terrainShader.SetMatrix4("uProjection", projection);
            _terrainShader.SetMatrix4("uModel", Matrix4x4.Identity);

            _terrainBuffer.Bind();
            _renderContext.DrawElements(_renderContext.Enums.Lines, _terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
        }

        public override void Dispose()
        {
            _terrainBuffer?.Dispose();
            _terrainShader?.Dispose();
            base.Dispose();
        }
    }
}