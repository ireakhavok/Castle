// Folder: ReadingChamber
// File: AnimationViewerPanelV2.cs
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.V2;
using SiegeEngine.Core.AssetParsing.V2.Model;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace ReadingChamber
{
    // Panel for viewing and testing animations on loaded models, with UI controls for loading mesh/armature/animation.
    public unsafe class AnimationViewerPanelV2 : BasePanel
    {
        // Static method to open the panel via event.
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new AnimationViewerPanelV2(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel));
        }
        // Inner UI overlay class for handling clicks.
        private class AssetUIOverlay : UIOverlay
        {
            private readonly AnimationViewerPanelV2 _parent;
            public AssetUIOverlay(AnimationViewerPanelV2 parent, IRenderContext renderContext, IControlContext controlContext, nint window) : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            protected override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }
        private FBXModel _model;
        private ModelManagerV2.ModelData _modelData;
        private ShaderProgram _assetShader;
        private EditorTextRenderer _textRenderer;
        private ShaderProgram _textShader;
        private Vector3 _cameraPosition = new Vector3(0, 500, 0);
        private Vector3 _cameraTarget = Vector3.Zero;
        private Vector3 _cameraUp = Vector3.UnitZ;
        private Quaternion _cameraRotation = Quaternion.Identity;
        private float _lastMouseX, _lastMouseY;
        private bool _firstMouse = true;
        private bool _isPanning = false;
        private string _meshPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters", "man_mesh.fbx");
        private float _cameraDistance;
        private float _maxExtent;
        private ModelManagerV2 _modelManagerV2;
        // Constructor, initializes shader, sets scaling mode.
        public AnimationViewerPanelV2(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus) : base(renderContext, controlContext, window, eventBus)
        {
            _assetShader = new ShaderProgram(_renderContext, AssetShader.VertexShaderSource, AssetShader.FragmentShaderSource);
            Scaling = ScalingMode.BestFit;
            BaseWidth = 1280f;
            BaseHeight = 720f;
            _modelManagerV2 = new ModelManagerV2(_renderContext);
            _modelData = new ModelManagerV2.ModelData(); // Initialize to avoid null reference
        }
        // Creates custom UI overlay.
        protected override UIOverlay CreateUIOverlay()
        {
            return new AssetUIOverlay(this, _renderContext, _controlContext, _window);
        }
        // Initializes buffers, shaders, loads initial mesh, discovers animations, updates UI, subscribes to events.
        public override void Init()
        {
            base.Init();
            _textShader = new ShaderProgram(_renderContext, TextShader.VertexShaderSource, TextShader.FragmentShaderSource);
            _textRenderer = new EditorTextRenderer(_renderContext, _window);
            _textRenderer.Initialize(_textShader);
            LoadMesh(_meshPath);
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }
        // Loads and parses mesh FBX, fixes unweighted vertices, centers camera, sets rest pose.
        private void LoadMesh(string path)
        {
            _meshPath = path;
            var forest = FBXParser.Load(path);
            var parsedModel = FBXParser.BuildModelFromForest(forest);
            _model = parsedModel;
            UpdateModelData();
            CenterCamera();
        }
        // Updates model data for rendering, chooses shader based on skinning.
        private void UpdateModelData()
        {
            _modelManagerV2.LoadModel(_meshPath);
            _modelManagerV2.TryGetModelData(Path.GetFileNameWithoutExtension(_meshPath).ToLower(), out _modelData);
        }
        // Centers camera on model bounds, sets distance based on extent.
        private void CenterCamera()
        {
            if (_model == null || _model.Meshes.Count == 0) return;
            Vector3 minBounds = new Vector3(float.MaxValue);
            Vector3 maxBounds = new Vector3(float.MinValue);
            foreach (var mesh in _model.Meshes)
            {
                foreach (var v in mesh.Vertices)
                {
                    minBounds = Vector3.Min(minBounds, v.Position);
                    maxBounds = Vector3.Max(maxBounds, v.Position);
                }
            }
            Vector3 center = (minBounds + maxBounds) / 2;
            _maxExtent = Math.Max(maxBounds.X - minBounds.X, Math.Max(maxBounds.Y - minBounds.Y, maxBounds.Z - minBounds.Z)) / 2;
            _cameraDistance = Math.Max(_maxExtent * 3.5f, 0.1f);
            _cameraTarget = center;
            Vector3 initialFront = new Vector3(0, 1, 0);
            _cameraPosition = _cameraTarget + initialFront * _cameraDistance;
            _cameraUp = Vector3.UnitZ;
        }
        // Handles file selection events for loading mesh/armature/animation.
        private void OnFileSelected(FileSelectedEvent e)
        {
            string hook = e.UserData as string;
            if (hook == "LoadMesh")
            {
                LoadMesh(e.Path);
            }
            _uiOverlay.RefreshUI();
        }
        // Handles UI clicks for loading files or selecting animations.
        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook == "LoadMesh")
            {
                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                fileSelector.UserData = "LoadMesh";
                _eventBus.Publish(new OpenPanelEvent(fileSelector));
            }
        }
        // Updates camera rotation/pan based on mouse, advances frame with arrows.
        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased);
            Vector2 relMouse = absMousePos - Position;
            bool over = relMouse.X >= 0 && relMouse.X <= Size.X && relMouse.Y >= TitleHeight && relMouse.Y <= Size.Y; // Below title
            //if (!over) return;
            // Camera control
            float mouseX = relMouse.X;
            float mouseY = relMouse.Y - TitleHeight; // Adjust for title
            if (_firstMouse)
            {
                _lastMouseX = mouseX;
                _lastMouseY = mouseY;
                _firstMouse = false;
            }
            float deltaX = mouseX - _lastMouseX;
            float deltaY = _lastMouseY - mouseY;
            _lastMouseX = mouseX;
            _lastMouseY = mouseY;
            if (mouseDown && !mousePressed) // Ongoing press
            {
                Quaternion yawQuat = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -deltaX * 0.002f);
                Quaternion pitchQuat = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -deltaY * 0.002f);
                _cameraRotation = Quaternion.Normalize(yawQuat * _cameraRotation * pitchQuat);
            }
            if (_controlContext.GetMouseButton(_window, MouseButton.Right) == InputAction.Press)
            {
                _isPanning = true;
                Vector3 right = Vector3.Normalize(Vector3.Cross(_cameraPosition - _cameraTarget, _cameraUp));
                Vector3 up = Vector3.Normalize(Vector3.Cross(right, _cameraPosition - _cameraTarget));
                _cameraPosition += -right * deltaX * 0.01f + up * deltaY * 0.01f;
                _cameraTarget += -right * deltaX * 0.01f + up * deltaY * 0.01f;
            }
            else
            {
                _isPanning = false;
            }
            Vector3 front = Vector3.Normalize(Vector3.Transform(new Vector3(0, 1, 0), _cameraRotation));
            _cameraUp = Vector3.Normalize(Vector3.Transform(new Vector3(0, 0, 1), _cameraRotation));
            if (!_isPanning)
            {
                _cameraPosition = _cameraTarget + front * _cameraDistance;
            }
        }
        // Renders model with lighting, textures, skeleton lines, UI, text info.
        public override void Render()
        {
            if (!Visible) return;
            if (_lastW != (int)Size.X || _lastH != (int)Size.Y)
            {
                _lastW = (int)Size.X;
                _lastH = (int)Size.Y;
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }
            _renderContext.ClearColor(0.118f, 0.118f, 0.118f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.DepthFunc(_renderContext.Enums.Less);
            _renderContext.DepthMask(true);
            _renderContext.Enable(_renderContext.Enums.CullFace);
            _renderContext.FrontFace(_renderContext.Enums.Clockwise);
            _renderContext.CullFace(_renderContext.Enums.Back);
            _renderContext.Disable(_renderContext.Enums.Blend);
            // Set matrices
            Matrix4x4 modelMatrix = Matrix4x4.Identity;
            Matrix4x4 view = Matrix4x4.CreateLookAt(_cameraPosition, _cameraTarget, _cameraUp);
            float currentDist = Vector3.Distance(_cameraPosition, _cameraTarget);
            float near = Math.Max(0.01f, currentDist - _maxExtent * 2f);
            float far = currentDist + _maxExtent * 2f;
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, Size.X / Size.Y, near, far);
            _assetShader.Use();
            _assetShader.SetMatrix4("uModel", modelMatrix);
            _assetShader.SetMatrix4("uView", view);
            _assetShader.SetMatrix4("uProjection", projection);
            _assetShader.SetUniform("uLightDir", -0.707f, -0.707f, 0.707f);
            _assetShader.SetUniform("uLightColor", 1.0f, 1.0f, 1.0f);
            _assetShader.SetUniform("uLightIntensity", 1.0f);
            _assetShader.SetUniform("uViewPos", _cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
            _assetShader.SetUniform("uAmbientStrength", 0.3f);
            _assetShader.SetUniform("uSpecularStrength", 0.05f);
            _assetShader.SetUniform("uShininess", 4.0f);
            _assetShader.SetUniform("uHasBones", 0);
            if (_modelData != null)
            {
                foreach (var mmr in _modelData.MeshRenders)
                {
                    // Bind textures
                    try
                    {
                        for (int i = 0; i < Math.Min(mmr.AlbedoTextures.Length, 4); i++)
                        {
                            _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + i);
                            _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.AlbedoTextures[i]);
                            _assetShader.SetUniform($"uAlbedoMap[{i}]", i);
                        }
                        for (int i = 0; i < Math.Min(mmr.NormalTextures.Length, 4); i++)
                        {
                            _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 4 + i);
                            _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.NormalTextures[i]);
                            _assetShader.SetUniform($"uNormalMap[{i}]", 4 + i);
                        }
                        for (int i = 0; i < Math.Min(mmr.MetallicTextures.Length, 4); i++)
                        {
                            _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 8 + i);
                            _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.MetallicTextures[i]);
                            _assetShader.SetUniform($"uMetallicMap[{i}]", 8 + i);
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        if (mmr.AlbedoTextures.Length > 0)
                        {
                            _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                            _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.AlbedoTextures[0]);
                            _assetShader.SetUniform("uAlbedoMap[0]", 0);
                        }
                    }
                    _renderContext.BindVertexArray(mmr.Vao);
                    _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                    _renderContext.BindVertexArray(0);
                }
            }
            _renderContext.Clear(_renderContext.Enums.DepthBufferBit);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Disable(_renderContext.Enums.CullFace);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            // Render title bar
            _quadRenderer.DrawQuad(0, 0, Size.X, TitleHeight, new Vector4(0.2f, 0.2f, 0.2f, 1.0f), Size.X, Size.Y);
            // Render UI overlay
            _uiOverlay.Render();
            // Render frame info
            string frameInfo = "Static Mesh Viewer";
            _textRenderer.RenderText(frameInfo, 10, TitleHeight + 10, (int)Size.X, (int)Size.Y, 12f);
            // Render 2px border
            float bw = 2f;
            Vector4 bc = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            // Bottom
            _quadRenderer.DrawQuad(0, Size.Y - bw, Size.X, bw, bc, Size.X, Size.Y);
            // Left
            _quadRenderer.DrawQuad(0, 0, bw, Size.Y, bc, Size.X, Size.Y);
            // Right
            _quadRenderer.DrawQuad(Size.X - bw, 0, bw, Size.Y, bc, Size.X, Size.Y);
            _renderContext.Disable(_renderContext.Enums.Blend);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            // Log OpenGL errors
            var error = _renderContext.GetError();
            if (error != _renderContext.Enums.NoError)
                Console.WriteLine($"AnimationViewerPanelV2: OpenGL Error: {error}");
        }
    }
}