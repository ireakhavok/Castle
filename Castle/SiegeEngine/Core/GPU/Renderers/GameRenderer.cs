using SiegeEngine.Systems;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders.OpenGL;

namespace SiegeEngine.Core.GPU.Renderers
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
        private ShaderProgram _pointShader, _waterShader, _gridShader, _modelShader;
        private GpuHandle _pointPipeline;
        private GpuHandle _pointGpuBuffer;
        private GpuHandle _waterPipeline;
        private GpuHandle _waterGpuBuffer;
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
            float[] pointVertices = new float[] { 0.0f, 0.0f, 0.0f };
            PipelineDesc pointDesc = ShaderCatalog.Describe(ShaderId.Point, _renderContext);
            pointDesc.Layout = new VertexLayout(3 * sizeof(float), new[]
            {
                new VertexAttribute(VertexSemantic.Position, _renderContext.Enums.Float, 3, 0, 0)
            });
            _pointPipeline = _renderContext.CreatePipeline(pointDesc);
            _pointGpuBuffer = _renderContext.CreateBuffer(new BufferDesc
            {
                Target = _renderContext.Enums.ArrayBuffer,
                Usage = _renderContext.Enums.StaticDraw,
                ByteSize = 3 * sizeof(float)
            });
            fixed (float* ptr = pointVertices)
            {
                _renderContext.UpdateBuffer(_pointGpuBuffer, new ReadOnlySpan<byte>((byte*)ptr, 3 * sizeof(float)));
            }
            float[] waterVertices = new float[]
            {
                -0.5f, -0.5f, 0.0f,
                0.5f, -0.5f, 0.0f,
                0.5f, 0.5f, 0.0f,
                -0.5f, 0.5f, 0.0f
            };
            _waterPipeline = _renderContext.CreatePipeline(ShaderCatalog.Describe(ShaderId.Water, _renderContext));
            _waterGpuBuffer = _renderContext.CreateBuffer(new BufferDesc
            {
                Target = _renderContext.Enums.ArrayBuffer,
                Usage = _renderContext.Enums.StaticDraw,
                ByteSize = 4 * 3 * sizeof(float)
            });
            fixed (float* ptr = waterVertices)
            {
                _renderContext.UpdateBuffer(_waterGpuBuffer, new ReadOnlySpan<byte>((byte*)ptr, 4 * 3 * sizeof(float)));
            }
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
            _renderContext.BindCamera(view, projection, Matrix4x4.Identity);
            _gridShader.Use();
            _waterShader.Use();
            _modelShader.Use();
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
                        FrameCB frame = new FrameCB { View = view, Projection = projection };
                        ObjectCB obj = new ObjectCB { Model = modelMatrix, PointSize = 1f };
                        _renderContext.BindPipeline(_pointPipeline);
                        _renderContext.SetConstants(ConstantSlot.Frame, frame);
                        _renderContext.SetConstants(ConstantSlot.Object, obj);
                        _renderContext.BindVertexBuffer(_pointGpuBuffer, 0, 3 * sizeof(float), 0);
                        _renderContext.Draw(1);
                    }
                    else if (entity.Type == "Water")
                    {
                        FrameCB frame = new FrameCB { View = view, Projection = projection };
                        _renderContext.BindPipeline(_waterPipeline);
                        _renderContext.SetConstants(ConstantSlot.Frame, frame);
                        _renderContext.BindVertexBuffer(_waterGpuBuffer, 0, 3 * sizeof(float), 0);
                        _renderContext.Draw(4);
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
                        _renderContext.BindCamera(view, projection, modelMatrix);
                        bool hasBones = modelComp.Model.Skeleton != null && modelComp.Model.Skeleton.Bones.Count > 0;
                        _modelShader.SetUniform("uHasBones", hasBones ? 1 : 0);
                        if (hasBones)
                        {
                            var transforms = modelComp.Model.Skeleton.ComputeGlobalTransforms();
                            var normalTransforms = new Matrix3x3[transforms.Length];
                            for (int i = 0; i < transforms.Length; i++)
                            {
                                if (Matrix4x4.Invert(transforms[i], out var inv))
                                {
                                    var trans = Matrix4x4.Transpose(inv);
                                    normalTransforms[i] = new Matrix3x3(
                                        trans.M11, trans.M12, trans.M13,
                                        trans.M21, trans.M22, trans.M23,
                                        trans.M31, trans.M32, trans.M33);
                                }
                                else
                                {
                                    normalTransforms[i] = Matrix3x3.Identity;
                                }
                            }
                            _modelShader.SetMatrix4Array("uBoneTransforms", transforms);
                            _modelShader.SetMatrix3Array("uNormalBoneTransforms", normalTransforms);
                        }
                        foreach (var mmr in modelData.MeshRenders)
                        {
                            try
                            {
                                for (int i = 0; i < mmr.AlbedoTextures.Length; i++)
                                {
                                    _renderContext.BindTextureSlot(i, _renderContext.ImportTexture(mmr.AlbedoTextures[i], _renderContext.Enums.Texture2D), $"uAlbedoMap[{i}]");
                                }
                                for (int i = 0; i < mmr.NormalTextures.Length; i++)
                                {
                                    _renderContext.BindTextureSlot(4 + i, _renderContext.ImportTexture(mmr.NormalTextures[i], _renderContext.Enums.Texture2D), $"uNormalMap[{i}]");
                                }
                                for (int i = 0; i < mmr.MetallicTextures.Length; i++)
                                {
                                    _renderContext.BindTextureSlot(8 + i, _renderContext.ImportTexture(mmr.MetallicTextures[i], _renderContext.Enums.Texture2D), $"uMetallicMap[{i}]");
                                }
                            }
                            catch (ArgumentException ex)
                            {
                                Console.WriteLine($"GameRenderer: Shader uniform error: {ex.Message}. Falling back to single texture.");
                                uint fallback = mmr.AlbedoTextures.FirstOrDefault();
                                _renderContext.BindTextureSlot(0, _renderContext.ImportTexture(fallback, _renderContext.Enums.Texture2D), "uAlbedoMap[0]");
                            }
                            _modelShader.SetUniform("uDebugTextureOnly", 1);
                            _renderContext.BindMesh(mmr.VertexHandle, mmr.IndexHandle, mmr.Stride != 0 ? mmr.Stride : 20 * sizeof(float));
                            _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                            _modelShader.SetUniform("uDebugTextureOnly", 0);
                            _renderContext.BindMesh(mmr.VertexHandle, mmr.IndexHandle, mmr.Stride != 0 ? mmr.Stride : 20 * sizeof(float));
                            _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"GameRenderer: Failed to find model data for {modelKey}");
                    }
                }
            }
        }
        public override void Resize(int width, int height)
        {
            SetViewport(width, height);
        }
        public override void Dispose()
        {
            if (_disposed) return;
            if (_pointPipeline.IsValid)
                _renderContext.Destroy(_pointPipeline);
            if (_pointGpuBuffer.IsValid)
                _renderContext.Destroy(_pointGpuBuffer);
            if (_waterPipeline.IsValid)
                _renderContext.Destroy(_waterPipeline);
            if (_waterGpuBuffer.IsValid)
                _renderContext.Destroy(_waterGpuBuffer);
            _pointShader.Dispose();
            _waterShader.Dispose();
            _gridShader.Dispose();
            _modelShader.Dispose();
            _disposed = true;
        }
    }
}
