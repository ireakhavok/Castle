using SiegeEngine.Systems;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Core.Rendering.Shaders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
namespace SiegeEngine.Core.Rendering
{
    public unsafe class GameRenderer : Renderer
    {
        private readonly IRenderContext _renderContext;
        private readonly nint _window;
        private readonly InputHandler _inputHandler;
        private readonly IGameServer _server;
        private readonly EventBus _eventBus;
        private readonly ModelManager _modelManager;
        private bool _disposed;
        private uint _vao, _pointBuffer, _waterBuffer;
        private ShaderProgram _pointShader, _waterShader, _gridShader, _modelShader;
        private int _width, _height;
        public GameRenderer(IRenderContext renderContext, nint window, InputHandler inputHandler, Player player, ModelManager modelManager, IGameServer server = null, EventBus eventBus = null) : base(player)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _window = window;
            _inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
            _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
            _server = server;
            _eventBus = eventBus;
        }
        public override void Initialize(nint windowHandle, int width, int height, Player player)
        {
            _width = width;
            _height = height;
            _renderContext.ClearColor(0.0f, 0.2f, 0.4f, 1.0f);
            _renderContext.Viewport(0, 0, (uint)width, (uint)height);
            Console.WriteLine($"GameRenderer: Initialized with {width}x{height}");
            var shaders = ShaderSetup.InitializeShaders(_renderContext);
            _pointShader = shaders.pointShader;
            _waterShader = shaders.waterShader;
            _gridShader = shaders.gridShader;
            _modelShader = shaders.modelShader;
            _vao = _renderContext.GenVertexArray();
            _pointBuffer = _renderContext.GenBuffer();
            _waterBuffer = _renderContext.GenBuffer();
            float[] pointVertices = new float[] { 0.0f, 0.0f, 0.0f };
            _renderContext.BindVertexArray(_vao);
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _pointBuffer);
            fixed (float* ptr = pointVertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(pointVertices.Length * sizeof(float)), ptr, _renderContext.Enums.StaticDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false, 3 * sizeof(float), (void*)0);
            float[] waterVertices = new float[]
            {
                -0.5f, -0.5f, 0.0f,
                0.5f, -0.5f, 0.0f,
                0.5f, 0.5f, 0.0f,
                -0.5f, 0.5f, 0.0f
            };
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _waterBuffer);
            fixed (float* ptr = waterVertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(waterVertices.Length * sizeof(float)), ptr, _renderContext.Enums.StaticDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false, 3 * sizeof(float), (void*)0);
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, 0);
            _renderContext.BindVertexArray(0);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
        }
        public void Clear()
        {
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
        }
        public void SetViewport(int width, int height)
        {
            _width = width;
            _height = height;
            _renderContext.Viewport(0, 0, (uint)width, (uint)height);
            Console.WriteLine($"GameRenderer: Set viewport to {width}x{height}");
        }
        public override void Render(IReadOnlyList<Entity> entities)
        {
            if (_disposed) return;
            var predictionSystem = new ClientPredictionSystem(_server, _eventBus);
            _player.Update(0.016f, _window, 0.0f, new PlayerMovement(_inputHandler, predictionSystem, _eventBus), true);
            Clear();
            Matrix4x4 view = _player.Camera?.ViewMatrix ?? Matrix4x4.Identity;
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)_width / _height, 0.1f, 100.0f);
            _gridShader.Use();
            _gridShader.SetMatrix4("uView", view);
            _gridShader.SetMatrix4("uProjection", projection);
            _pointShader.Use();
            _pointShader.SetMatrix4("uView", view);
            _pointShader.SetMatrix4("uProjection", projection);
            _waterShader.Use();
            _waterShader.SetMatrix4("uView", view);
            _waterShader.SetMatrix4("uProjection", projection);
            _modelShader.Use();
            _modelShader.SetMatrix4("uView", view);
            _modelShader.SetMatrix4("uProjection", projection);
            _renderContext.BindVertexArray(_vao);
            foreach (var entity in entities)
            {
                var physics = entity.GetComponent<PhysicsComponent>();
                var modelComp = entity.GetComponent<ModelComponent>();
                if (physics != null)
                {
                    Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(physics.Rotation);
                    Matrix4x4 modelMatrix = rotation * Matrix4x4.CreateTranslation(physics.Position);
                    if (entity.Type == "Player" || entity.Type == "Default")
                    {
                        _pointShader.Use();
                        _pointShader.SetMatrix4("uModel", modelMatrix);
                        _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _pointBuffer);
                        _renderContext.DrawArrays(_renderContext.Enums.Points, 0, 1);
                    }
                    else if (entity.Type == "Water")
                    {
                        _waterShader.Use();
                        _waterShader.SetMatrix4("uModel", modelMatrix);
                        _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _waterBuffer);
                        _renderContext.DrawArrays(_renderContext.Enums.TriangleFan, 0, 4);
                    }
                }
                if (modelComp != null && physics != null)
                {
                    string modelKey = modelComp.Key ?? "default_cube";
                    Console.WriteLine($"GameRenderer: Rendering entity {entity.Id} with model key {modelKey}");
                    if (_modelManager.TryGetModelData(modelKey, out var modelData))
                    {
                        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(physics.Rotation);
                        Matrix4x4 modelMatrix = rotation * Matrix4x4.CreateTranslation(physics.Position);
                        _modelShader.Use();
                        _modelShader.SetMatrix4("uModel", modelMatrix);
                        _modelShader.SetUniform("uHasBones", modelComp.Model.HasSkin ? 1 : 0);
                        if (modelComp.Model.HasSkin && modelComp.Model.Skeleton != null && modelComp.NormalBoneTransforms != null)
                        {
                            var transforms = modelComp.Model.Skeleton.GetTransforms();
                            _modelShader.SetMatrix4Array("uBoneTransforms", transforms);
                            _modelShader.SetMatrix3Array("uNormalBoneTransforms", modelComp.NormalBoneTransforms);
                        }
                        foreach (var mmr in modelData.MeshRenders)
                        {
                            // Bind textures
                            try
                            {
                                for (int i = 0; i < mmr.AlbedoTextures.Length; i++)
                                {
                                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + i);
                                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.AlbedoTextures[i]);
                                    _modelShader.SetUniform($"uAlbedoMap[{i}]", i);
                                }
                                for (int i = 0; i < mmr.NormalTextures.Length; i++)
                                {
                                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 4 + i);
                                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.NormalTextures[i]);
                                    _modelShader.SetUniform($"uNormalMap[{i}]", 4 + i);
                                }
                                for (int i = 0; i < mmr.MetallicTextures.Length; i++)
                                {
                                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 8 + i);
                                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.MetallicTextures[i]);
                                    _modelShader.SetUniform($"uMetallicMap[{i}]", 8 + i);
                                }
                            }
                            catch (ArgumentException ex)
                            {
                                Console.WriteLine($"GameRenderer: Shader uniform error: {ex.Message}. Falling back to single texture.");
                                _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                                _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.AlbedoTextures.FirstOrDefault());
                                _modelShader.SetUniform("uAlbedoMap[0]", 0); // Fallback
                            }
                            // Debug texture-only pass
                            _modelShader.SetUniform("uDebugTextureOnly", 1);
                            _renderContext.BindVertexArray(mmr.Vao);
                            _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                            _modelShader.SetUniform("uDebugTextureOnly", 0);
                            // Normal rendering pass
                            _renderContext.BindVertexArray(mmr.Vao);
                            _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"GameRenderer: Failed to find model data for {modelKey}");
                    }
                }
            }
            _renderContext.BindVertexArray(0);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
        }
        public override void Resize(int width, int height)
        {
            SetViewport(width, height);
        }
        public override void Dispose()
        {
            if (_disposed) return;
            _renderContext.DeleteVertexArray(_vao);
            _renderContext.DeleteBuffer(_pointBuffer);
            _renderContext.DeleteBuffer(_waterBuffer);
            _pointShader.Dispose();
            _waterShader.Dispose();
            _gridShader.Dispose();
            _modelShader.Dispose();
            _disposed = true;
        }
    }
}