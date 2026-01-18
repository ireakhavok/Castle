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
        private float _duration = 0f;
        private string _currentAnimation;
        private ShaderProgram _assetShader;
        private VertexBuffer _skeletonBuffer;
        private ShaderProgram _pointShader;
        private EditorTextRenderer _textRenderer;
        private ShaderProgram _textShader;
        private Matrix4x4[] _currentGlobalTransforms;
        private Matrix4x4[] _boneMatrices;
        private Matrix3x3[] _currentNormalTransforms;
        private Vector3 _cameraPosition = new Vector3(0, 500, 0);
        private Vector3 _cameraTarget = Vector3.Zero;
        private Vector3 _cameraUp = Vector3.UnitZ;
        private Quaternion _cameraRotation = Quaternion.Identity;
        private float _lastMouseX, _lastMouseY;
        private bool _firstMouse = true;
        private bool _isPanning = false;
        private string _meshPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters", "man_mesh.fbx");
        private string _armaturePath = "";
        private string _animationPath = "";
        private List<string> _animationFiles = new List<string>();
        private float _cameraDistance;
        private float _maxExtent;
        private int _currentFrameIndex = 0;
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
            _skeletonBuffer = new VertexBuffer(_renderContext);
            _pointShader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
            _textShader = new ShaderProgram(_renderContext, TextShader.VertexShaderSource, TextShader.FragmentShaderSource);
            _textRenderer = new EditorTextRenderer(_renderContext, _window);
            _textRenderer.Initialize(_textShader);
            LoadMesh(_meshPath);
            DiscoverAnimationFiles();
            UpdateUIControls();
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
            if (_model.HasSkin)
            {
                SetRestPose();
            }
            CenterCamera();
        }
        // Loads armature FBX, remaps bone properties to match mesh coordinate system, recomputes locals, fixes weights, updates data.
        private void LoadArmature(string path)
        {
            _armaturePath = path;
            var forest = FBXParser.Load(path);
            var parsedModel = FBXParser.BuildModelFromForest(forest);
            if (_model == null)
            {
                _model = new FBXModel();
            }
            var oldSkeleton = _model.Skeleton;
            var tempSkeleton = parsedModel.Skeleton;
            // Unremap and remap bone components to match mesh's axis system
            //stubbed for future implementation
            _model.Skeleton = tempSkeleton;
            if (_model.HasSkin)
            {
                SetRestPose();
            }
            UpdateModelData();
        }
        // Loads animation FBX, remaps to match mesh, aligns hierarchies by name matching, adjusts transforms.
        private void LoadAnimation(string animPath)
        {
            var animForest = FBXParser.Load(animPath);
            var objectsNode = animForest.TreeList.FirstOrDefault(n => n.Name == "Objects");
            //var objectsById = FBXParser.GatherObjectsById(objectsNode);
            //var conns = FBXParser.GatherConnections(animForest);
            var animModel = FBXParser.BuildModelFromForest(animForest);
            var validAnimations = animModel.Animations.Where(a => a.Keyframes.Count > 0).ToList();
            if (validAnimations.Count > 0)
            {
                //stubbed for future implementation
            }
        }
        // Updates transforms and normals from a specific animation frame, updates skeleton visualization.
        private void UpdateTransformsFromFrame(int frame)
        {
            //STUBBED FOR FUTURE USE
        }
        // Sets up model data for rendering, chooses shader based on skinning.
        private void UpdateModelData()
        {
            _modelManagerV2.LoadModel(_meshPath);
            _modelManagerV2.TryGetModelData(Path.GetFileNameWithoutExtension(_meshPath).ToLower(), out _modelData);
            if (_model.HasSkin)
            {
                _assetShader = new ShaderProgram(_renderContext, AnimationShader.VertexShaderSource, AnimationShader.FragmentShaderSource);
            }
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
        // Finds .fbx files in .fbm subdirectory for animations.
        private void DiscoverAnimationFiles()
        {
            string fbmDir = Path.Combine(Path.GetDirectoryName(_meshPath), Path.GetFileNameWithoutExtension(_meshPath) + ".fbm");
            if (Directory.Exists(fbmDir))
            {
                _animationFiles = Directory.GetFiles(fbmDir, "*.fbx").ToList();
            }
        }
        // Updates vertex buffers with current vertex data (e.g., after weight changes).
        private void UpdateModelBuffers()
        {
            //stubbed for future use
        }
        // Updates dynamic select in HTML for animations.
        private void UpdateUIControls()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AssetViewerUI.html");
            if (!File.Exists(htmlPath))
            {
                return;
            }
            string baseHtml = File.ReadAllText(htmlPath);
            int insertIndex = baseHtml.IndexOf("");
            if (insertIndex == -1)
            {
                return;
            }
            StringBuilder dynamicSelect = new StringBuilder();
            dynamicSelect.Append("<select id=\"animSelect\" style=\"\">");
            foreach (var file in _animationFiles)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                dynamicSelect.Append($"<option value=\"{file}\">{name}</option>");
            }
            dynamicSelect.Append("</select>");
            string modifiedHtml = baseHtml.Insert(insertIndex, dynamicSelect.ToString());
            _uiOverlay.LoadUI(modifiedHtml);
            var animSelect = _uiOverlay.FindElementById("animSelect");
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }
        // Handles file selection events for loading mesh/armature/animation.
        private void OnFileSelected(FileSelectedEvent e)
        {
            string hook = e.UserData as string;
            if (hook == "LoadMesh")
            {
                LoadMesh(e.Path);
            }
            else if (hook == "LoadArmature")
            {
                LoadArmature(e.Path);
            }
            else if (hook == "LoadAnimation")
            {
                LoadAnimation(e.Path);
            }
            _currentFrameIndex = 0;
        }
        // Handles UI clicks for loading files or selecting animations.
        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook == "TogglePlay")
            {
                // Removed
            }
            else if (hook == "LoadMesh")
            {
                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                fileSelector.UserData = "LoadMesh";
                _eventBus.Publish(new OpenPanelEvent(fileSelector));
            }
            else if (hook == "LoadArmature")
            {
                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                fileSelector.UserData = "LoadArmature";
                _eventBus.Publish(new OpenPanelEvent(fileSelector));
            }
            else if (hook == "LoadAnimation")
            {
                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                fileSelector.UserData = "LoadAnimation";
                _eventBus.Publish(new OpenPanelEvent(fileSelector));
            }
            else if (elem.Tag == "select")
            {
                var select = elem as SelectElement;
                if (select != null)
                {
                    var allSelects = _uiOverlay.FindElementsByTag("select");
                    foreach (var s in allSelects)
                    {
                        if (s is SelectElement sel)
                        {
                            sel.IsOpen = false;
                        }
                    }
                    select.IsOpen = !select.IsOpen;
                    _uiOverlay.RefreshUI();
                }
            }
            else if (elem.Tag == "option")
            {
                var select = elem.Parent as SelectElement;
                if (select != null)
                {
                    string val = elem.Attributes.GetValueOrDefault("value", string.Join("", elem.Children.OfType<TextElement>().Select(t => t.Content)));
                    LoadAnimation(val);
                    select.IsOpen = false;
                    _uiOverlay.RefreshUI();
                }
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
            if (_model.Skeleton != null && _model.Animations.Count > 0)
            {
                //stubbed for future use
            }
            //UpdateSkeletonVisualization();
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
            _renderContext.FrontFace(_renderContext.Enums.CounterClockwise);
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
            string frameInfo = "Frame: " + _currentFrameIndex;
            if (_model != null && _model.Animations.Count > 0)
            {
                var animation = _model.Animations.Find(a => a.Name == _currentAnimation);
                if (animation != null)
                {
                    frameInfo += " / " + (animation.Keyframes.Count - 1);
                }
            }
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
            _renderContext.Enable(_renderContext.Enums.CullFace);
        }
        // Builds line buffer for visualizing skeleton bones.
        private void UpdateSkeletonVisualization()
        {
            //stubbed for future use
        }
        private void SetRestPose()
        {
            if (_model.Skeleton == null || _model.Skeleton.Bones.Count == 0) return;
            Matrix4x4[] locals = _model.Skeleton.Bones.Select(b => b.LocalRest).ToArray();
            _currentGlobalTransforms = _model.Skeleton.ComputeGlobalTransforms();
            _boneMatrices = new Matrix4x4[_currentGlobalTransforms.Length];
            for (int i = 0; i < _currentGlobalTransforms.Length; i++)
            {
                _boneMatrices[i] = _currentGlobalTransforms[i] * _model.Skeleton.Bones[i].BindPose;
            }
            _currentNormalTransforms = new Matrix3x3[_boneMatrices.Length];
            for (int i = 0; i < _boneMatrices.Length; i++)
            {
                if (Matrix4x4.Invert(_boneMatrices[i], out Matrix4x4 inv))
                {
                    Matrix4x4 transInv = Matrix4x4.Transpose(inv);
                    _currentNormalTransforms[i] = new Matrix3x3(
                        transInv.M11, transInv.M12, transInv.M13,
                        transInv.M21, transInv.M22, transInv.M23,
                        transInv.M31, transInv.M32, transInv.M33);
                }
                else
                {
                    _currentNormalTransforms[i] = Matrix3x3.Identity;
                }
            }
            UpdateSkeletonVisualization();
        }
        private void PrintMatrix(Matrix4x4 m)
        {
            Console.WriteLine($"({m.M11:F4}, {m.M12:F4}, {m.M13:F4}, {m.M14:F4})");
            Console.WriteLine($"({m.M21:F4}, {m.M22:F4}, {m.M23:F4}, {m.M24:F4})");
            Console.WriteLine($"({m.M31:F4}, {m.M32:F4}, {m.M33:F4}, {m.M34:F4})");
            Console.WriteLine($"({m.M41:F4}, {m.M42:F4}, {m.M43:F4}, {m.M44:F4})");
        }
    }
}