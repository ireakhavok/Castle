using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using SiegeEngine.Core.UI;
namespace ReadingChamber
{
    public unsafe class AssetViewerPanel : BasePanel
    {
        private class AssetUIOverlay : UIOverlay
        {
            private readonly AssetViewerPanel _parent;
            public AssetUIOverlay(AssetViewerPanel parent, IRenderContext renderContext, IControlContext controlContext, IntPtr window) : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            protected override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }
        private FBXModel _model;
        private ModelManager.ModelData _modelData;
        private float _time = 0f;
        private string _currentAnimation;
        private bool _playing = true;
        private ShaderProgram _assetShader;
        private Vector3 _cameraPosition = new Vector3(0, 0, 5);
        private Vector3 _cameraTarget = Vector3.Zero;
        private Vector3 _cameraUp = Vector3.UnitZ;
        private Quaternion _cameraRotation = Quaternion.Identity;
        private float _lastMouseX, _lastMouseY;
        private bool _firstMouse = true;
        private bool _isPanning = false;
        private string _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters", "cube.fbx");
        public AssetViewerPanel(IRenderContext renderContext, IControlContext controlContext, IntPtr window, EventBus eventBus) : base(renderContext, controlContext, window, eventBus)
        {
            _assetShader = new ShaderProgram(_renderContext, AssetShader.VertexShaderSource, AssetShader.FragmentShaderSource);
            Scaling = ScalingMode.BestFit;
            BaseWidth = 800f;
            BaseHeight = 600f;
        }
        protected override UIOverlay CreateUIOverlay()
        {
            return new AssetUIOverlay(this, _renderContext, _controlContext, _window);
        }
        public override void Init()
        {
            base.Init();
            // Initialize shader first
            var modelManager = new ModelManager(renderContext: _renderContext);
            modelManager.LoadModel(_path, new HashSet<string>(), new Dictionary<string, string>());
            string key = Path.GetFileNameWithoutExtension(_path).ToLower();
            if (!modelManager.TryGetModel(key, out _model))
            {
                Console.WriteLine("AssetViewerPanel: Failed to load or parse model");
            }
            else
            {
                Console.WriteLine($"AssetViewerPanel: Loaded model with {_model.Meshes.Count} meshes");
            }
            if (!modelManager.TryGetModelData(key, out _modelData))
            {
                Console.WriteLine("AssetViewerPanel: Failed to get model data");
            }
            LoadAdditionalAnimations();
            if (_model != null)
            {
                // Center model based on bounds
                Vector3 minBounds = new Vector3(float.MaxValue);
                Vector3 maxBounds = new Vector3(float.MinValue);
                foreach (var mesh in _model.Meshes)
                {
                    foreach (var v in mesh.Vertices)
                    {
                        minBounds = Vector3.Min(minBounds, new Vector3(v.X, v.Y, v.Z));
                        maxBounds = Vector3.Max(maxBounds, new Vector3(v.X, v.Y, v.Z));
                    }
                }
                Vector3 center = (minBounds + maxBounds) / 2;
                float maxExtent = Math.Max(maxBounds.X - minBounds.X, Math.Max(maxBounds.Y - minBounds.Y, maxBounds.Z - minBounds.Z)) / 2;
                _cameraPosition = center + new Vector3(0, -maxExtent * 2.5f, 0);
                _cameraTarget = center;
                Console.WriteLine($"AssetViewerPanel: Model center: {center}, maxExtent: {maxExtent}, cameraPosition: {_cameraPosition}");
                if (_model.Animations.Count > 0)
                {
                    _currentAnimation = _model.Animations[0].Name;
                }
            }
            UpdateUIControls();
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }
        private void LoadAdditionalAnimations()
        {
            string fbmDir = Path.Combine(Path.GetDirectoryName(_path), Path.GetFileNameWithoutExtension(_path) + ".fbm");
            if (Directory.Exists(fbmDir))
            {
                var animFiles = Directory.GetFiles(fbmDir, "*.fbx");
                foreach (var animPath in animFiles)
                {
                    var animForest = FBXParser.Load(animPath);
                    var animModel = FBXParser.BuildModelFromForest(animForest, true);
                    foreach (var anim in animModel.Animations)
                    {
                        anim.Name = Path.GetFileNameWithoutExtension(animPath) + (animModel.Animations.Count > 1 ? "_" + anim.Name : "");
                        Console.WriteLine($"Added animation {anim.Name} with {anim.Keyframes.Count} keyframes");
                    }
                    _model.Animations.AddRange(animModel.Animations);
                }
                Console.WriteLine($"Total animations loaded: {_model.Animations.Count}");
            }
        }
        private void UpdateUIControls()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AssetViewerUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine($"AssetViewerPanel: UI HTML file not found at {htmlPath}");
                return;
            }
            string baseHtml = File.ReadAllText(htmlPath);
            int insertIndex = baseHtml.IndexOf("<!-- Animation buttons will be added here dynamically -->");
            if (insertIndex == -1)
            {
                Console.WriteLine("AssetViewerPanel: Insertion point not found in HTML");
                return;
            }
            StringBuilder dynamicSelect = new StringBuilder();
            dynamicSelect.Append("<select id=\"animSelect\" style=\"position: absolute; left: 10px; top: 30px;\">");
            if (_model != null)
            {
                foreach (var a in _model.Animations)
                {
                    dynamicSelect.Append($"<option value=\"{a.Name}\">{a.Name}</option>");
                }
            }
            dynamicSelect.Append("</select>");
            string modifiedHtml = baseHtml.Insert(insertIndex, dynamicSelect.ToString());
            _uiOverlay.LoadUI(modifiedHtml);
            var animSelect = _uiOverlay.FindElementById("animSelect");
            if (animSelect != null)
            {
                animSelect.Attributes["data-hook"] = "SelectAnim";
            }
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }
        private void OnFileSelected(FileSelectedEvent e)
        {
            _path = e.Path;
            var modelManager = new ModelManager(renderContext: _renderContext);
            modelManager.LoadModel(_path, new HashSet<string>(), new Dictionary<string, string>());
            string key = Path.GetFileNameWithoutExtension(_path).ToLower();
            if (modelManager.TryGetModel(key, out _model) && modelManager.TryGetModelData(key, out _modelData))
            {
                LoadAdditionalAnimations();
                if (_model.Animations.Count > 0)
                {
                    _currentAnimation = _model.Animations[0].Name;
                }
                UpdateUIControls();
            }
            else
            {
                Console.WriteLine("AssetViewerPanel: Failed to load selected model");
            }
        }
        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            Console.WriteLine($"Clicked on {elem.Tag}, hook: {hook}");
            if (hook == "TogglePlay")
            {
                _playing = !_playing;
                Console.WriteLine($"Toggled play to {_playing}");
            }
            else if (hook == "LoadFBX")
            {
                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                _eventBus.Publish(new OpenPanelEvent(fileSelector));
            }
            else if (elem.Tag == "option")
            {
                var select = elem.Parent as SelectElement;
                if (select != null)
                {
                    string val = elem.Attributes.GetValueOrDefault("value", string.Join("", elem.Children.OfType<TextElement>().Select(t => t.Content)));
                    _currentAnimation = val;
                    _time = 0f;
                    foreach (var opt in select.Children.Where(c => c.Tag.ToLower() == "option"))
                    {
                        opt.Attributes.Remove("selected");
                    }
                    elem.Attributes["selected"] = "";
                    select.IsOpen = false;
                    _uiOverlay.RefreshUI();
                    Console.WriteLine($"Selected animation: {val}");
                }
            }
        }
        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased);
            Vector2 relMouse = absMousePos - Position;
            bool over = relMouse.X >= 0 && relMouse.X <= Size.X && relMouse.Y >= TitleHeight && relMouse.Y <= Size.Y; // Below title
            if (!over) return;
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
                _cameraPosition = _cameraTarget + front * (_cameraPosition - _cameraTarget).Length();
            }
            if (_playing && _model != null)
            {
                _time += deltaTime;
                if (_model.Skeleton != null && _model.Animations.Count > 0)
                {
                    var animation = _model.Animations.Find(a => a.Name == _currentAnimation);
                    if (animation != null)
                    {
                        var transforms = animation.GetBoneTransforms(_time);
                        _model.Skeleton.UpdateTransforms(transforms);
                    }
                }
            }
            // Handle input for animation selection, play/pause, etc. (expand with UI buttons)
            if (_controlContext.GetKey(_window, Key.Space) == InputAction.Press)
            {
                _playing = !_playing;
            }
        }
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
            _renderContext.Enable(_renderContext.Enums.CullFace);
            _renderContext.CullFace(_renderContext.Enums.Back);
            // Set matrices
            Matrix4x4 modelMatrix = Matrix4x4.Identity;
            Matrix4x4 view = Matrix4x4.CreateLookAt(_cameraPosition, _cameraTarget, _cameraUp);
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, Size.X / Size.Y, 0.1f, 100f);
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
            _assetShader.SetUniform("uHasBones", _model.HasSkin ? 1 : 0);
            if (_model.Skeleton != null && _model.HasSkin)
            {
                var transforms = _model.Skeleton.GetTransforms();
                _assetShader.SetMatrix4Array("uBoneTransforms", transforms);
            }
            foreach (var mmr in _modelData.MeshRenders)
            {
                // Bind textures
                for (int t = 0; t < Math.Min(mmr.AlbedoTextures.Length, 4); t++)
                {
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + t);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.AlbedoTextures[t]);
                    _assetShader.SetUniform($"uAlbedoMap[{t}]", t);
                }
                for (int t = 0; t < Math.Min(mmr.NormalTextures.Length, 4); t++)
                {
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 4 + t);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.NormalTextures[t]);
                    _assetShader.SetUniform($"uNormalMap[{t}]", 4 + t);
                }
                for (int t = 0; t < Math.Min(mmr.MetallicTextures.Length, 4); t++)
                {
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 8 + t);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.MetallicTextures[t]);
                    _assetShader.SetUniform($"uMetallicMap[{t}]", 8 + t);
                }
                _renderContext.BindVertexArray(mmr.Vao);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, (void*)0);
                int error;
                while ((error = _renderContext.GetError()) != _renderContext.Enums.NoError)
                {
                    Console.WriteLine($"AssetViewerPanel: OpenGL error after draw: {error}");
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
            _renderContext.Enable(_renderContext.Enums.CullFace);
        }
    }
}