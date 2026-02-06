// Folder: SiegeEngine.Scenes
// File: SandboxScene.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.AssetParsing.V2;
using SiegeEngine.Core.AssetParsing.V2.Model;

namespace SiegeEngine.Scenes.StartingPoints
{
    public unsafe class SandboxScene : Scene
    {
        public static void Launch(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            eventBus.Publish(new SwitchSceneEvent("Sandbox"));
        }
        private readonly Player _player;
        private readonly PlayerMovement _playerMovement;
        private readonly ModelManagerV2 _modelManager;
        private readonly IGameServer _server;
        private float _scrollDelta;
        private ShaderProgram _modelShader;
        private ShaderProgram _gridShader;
        public SandboxScene(IRenderContext renderContext, IControlContext controlContext, nint window, Player player, IGameServer server, PlayerMovement playerMovement, EventBus eventBus, ModelManagerV2 modelManager)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _playerMovement = playerMovement ?? throw new ArgumentNullException(nameof(playerMovement));
            _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _scrollDelta = 0f;
            var lightingSystem = _systems.Find(s => s is LightingSystem) as LightingSystem;
            if (lightingSystem != null)
            {
                foreach (var light in lightingSystem.GetDirectionalLights().ToList())
                {
                    lightingSystem.RemoveLight(light);
                }
                var sun = new LightComponent(LightType.Directional, new Vector3(1f, 1f, 1f), 1.0f, new Vector3(-0.707f, -0.707f, 0.707f));
                lightingSystem.AddLight(sun);
                //Console.WriteLine("SandboxScene: Added directional light at direction (-0.707, -0.707, 0.707)");
            }
        }
        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);
            _renderContext.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
            var shaders = ShaderSetup.InitializeShaders(_renderContext);
            _modelShader = shaders.modelShader;
            _gridShader = new ShaderProgram(_renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
            _controlContext.SetScrollCallback(_window, (w, xoffset, yoffset) => _scrollDelta = (float)yoffset);
            _controlContext.SetWindowSizeCallback(_window, (w, newWidth, newHeight) =>
            {
                _width = newWidth;
                _height = newHeight;
                _renderContext.Viewport(0, 0, (uint)newWidth, (uint)newHeight);
            });
        }
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            _player.Update(deltaTime, _window, _scrollDelta, _playerMovement, true);
            _scrollDelta = 0f;
        }
        public override void Render(IReadOnlyList<Entity> entities)
        {
            _controlContext.GetWindowSize(_window, out int curW, out int curH);
            if (curW != _width || curH != _height)
            {
                _width = curW;
                _height = curH;
            }
            _renderContext.Viewport(0, 0, (uint)_width, (uint)_height);
            _renderContext.Scissor(0, 0, (uint)_width, (uint)_height);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            Matrix4x4 view = _player.Camera?.ViewMatrix ?? Matrix4x4.Identity;
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)_width / _height, 0.1f, 1000f);
            // Render grid
            _gridShader.Use();
            _gridShader.SetMatrix4("uModel", Matrix4x4.Identity);
            _gridShader.SetMatrix4("uView", view);
            _gridShader.SetMatrix4("uProjection", projection);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _gridBuffer.Bind();
            _renderContext.DrawArrays(_renderContext.Enums.Lines, 0, _gridBuffer.GetVertexCount());
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            // Render player model
            _modelShader.Use();
            _modelShader.SetMatrix4("uView", view);
            _modelShader.SetMatrix4("uProjection", projection);
            var lightingSystem = _systems.Find(s => s is LightingSystem) as LightingSystem;
            if (lightingSystem != null)
            {
                var lightData = lightingSystem.GetShaderUniforms();
                if (lightData.HasValue)
                {
                    _modelShader.SetUniform("uLightDir", lightData.Value.direction.X, lightData.Value.direction.Y, lightData.Value.direction.Z, 0.0f);
                    _modelShader.SetUniform("uLightColor", lightData.Value.color.X, lightData.Value.color.Y, lightData.Value.color.Z, 0.0f);
                    _modelShader.SetUniform("uLightIntensity", lightData.Value.intensity);
                    //Console.WriteLine($"SandboxScene: Light direction: {lightData.Value.direction}, intensity: {lightData.Value.intensity}");
                }
            }
            _modelShader.SetUniform("uViewPos", _player?.Camera?.Position.X ?? 0f, _player?.Camera?.Position.Y ?? 0f, _player?.Camera?.Position.Z ?? 0f, 0.0f);
            _modelShader.SetUniform("uAmbientStrength", 0.3f);
            _modelShader.SetUniform("uSpecularStrength", 0.05f);
            _modelShader.SetUniform("uShininess", 4.0f);
            var playerEntity = _server.GetEntityById(_player.EntityId);
            var modelComponent = playerEntity?.GetComponent<ModelComponent>();
            string modelKey = modelComponent?.Key?.ToLower() ?? "man_mesh";
            var physics = _player.Physics;
            if (physics != null && _modelManager.TryGetModelData(modelKey, out var modelData))
            {
                // Apply position and rotation
                Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(physics.Rotation);
                Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(physics.Position);
                Matrix4x4 modelMatrix = rotationMatrix * translationMatrix;
                _modelShader.Use();
                _modelShader.SetMatrix4("uModel", modelMatrix);
                bool hasBones = modelComponent.Model.Skeleton != null && modelComponent.Model.Skeleton.Bones.Count > 0;
                _modelShader.SetUniform("uHasBones", hasBones ? 1 : 0);
                if (hasBones)
                {
                    var globals = modelComponent.Model.Skeleton.ComputeGlobalTransforms();
                    var transforms = new Matrix4x4[globals.Length];
                    for (int i = 0; i < globals.Length; i++)
                    {
                        transforms[i] = modelComponent.Model.Skeleton.Bones[i].BindPose * globals[i];
                    }
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
                int total_albedo_count = 0;
                int total_meshes = modelData.MeshRenders.Count;
                int total_normal_count = 0;
                int total_metallic_count = 0;
                foreach (var mmr in modelData.MeshRenders)
                {
                    // Bind textures
                    try
                    {
                        // Bind albedo textures (up to 4)
                        for (int i = 0; i < Math.Min(mmr.AlbedoTextures.Length, 4); i++)
                        {
                            _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + i);
                            _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.AlbedoTextures[i]);
                            _modelShader.SetUniform($"uAlbedoMap[{i}]", i);
                            total_albedo_count++;
                            ////Console.WriteLine($"SandboxScene: Bound albedo texture {i} with ID {mmr.AlbedoTextures[i]}");
                        }
                        // Bind normal textures (up to 4)
                        for (int i = 0; i < Math.Min(mmr.NormalTextures.Length, 4); i++)
                        {
                            _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 4 + i);
                            _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.NormalTextures[i]);
                            _modelShader.SetUniform($"uNormalMap[{i}]", 4 + i);
                            total_normal_count++;
                            ////Console.WriteLine($"SandboxScene: Bound normal texture {i} with ID {mmr.NormalTextures[i]}");
                        }
                        // Bind metallic textures (up to 4)
                        for (int i = 0; i < Math.Min(mmr.MetallicTextures.Length, 4); i++)
                        {
                            _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 8 + i);
                            _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.MetallicTextures[i]);
                            _modelShader.SetUniform($"uMetallicMap[{i}]", 8 + i);
                            total_metallic_count++;
                            ////Console.WriteLine($"SandboxScene: Bound metallic texture {i} with ID {mmr.MetallicTextures[i]}");
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        //Console.WriteLine($"SandboxScene: Texture binding error: {ex.Message}. Falling back to first albedo texture.");
                        if (mmr.AlbedoTextures.Length > 0)
                        {
                            _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                            _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.AlbedoTextures[0]);
                            _modelShader.SetUniform("uAlbedoMap[0]", 0);
                        }
                    }
                    // Debug material index pass (disabled)
                    try
                    {
                        _modelShader.SetUniform("uDebugMaterialIndex", 0);
                        _renderContext.BindVertexArray(mmr.Vao);
                        _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                    }
                    catch (ArgumentException ex)
                    {
                        //Console.WriteLine($"SandboxScene: Debug material index error: {ex.Message}. Skipping debug pass.");
                    }
                    // Debug texture-only pass
                    try
                    {
                        _modelShader.SetUniform("uDebugTextureOnly", 1);
                        _renderContext.BindVertexArray(mmr.Vao);
                        _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                        _modelShader.SetUniform("uDebugTextureOnly", 0);
                    }
                    catch (ArgumentException ex)
                    {
                        //Console.WriteLine($"SandboxScene: Debug texture-only error: {ex.Message}. Skipping debug pass.");
                    }
                    // Normal rendering pass
                    _renderContext.BindVertexArray(mmr.Vao);
                    _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                    _renderContext.BindVertexArray(0);
                }
                //Console.WriteLine($"SandboxScene: Rendered {total_meshes} player with {total_albedo_count} total albedo textures, {total_normal_count} normal textures, {total_metallic_count} metallic textures");//, VAO {mmr.Vao}, indices {mmr.IndexCount}");
            }
            else
            {
                //Console.WriteLine($"SandboxScene: Error: Model data for {modelKey} not found or physics unavailable");
            }
            // Log OpenGL errors
            var error = _renderContext.GetError();
            if (error != _renderContext.Enums.NoError)
                Console.WriteLine($"SandboxScene: OpenGL Error: {error}");
        }
        public override void Dispose()
        {
            if (!_disposed)
            {
                _modelShader.Dispose();
                _gridShader.Dispose();
                base.Dispose();
            }
        }
    }
}