// Folder: ReadingChamber
// File: AssetViewerPanel.cs
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
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
        private float _duration = 0f;
        private string _currentAnimation;
        private bool _playing = false;
        private ShaderProgram _assetShader;
        private VertexBuffer _skeletonBuffer;
        private ShaderProgram _pointShader;
        private EditorTextRenderer _textRenderer;
        private ShaderProgram _textShader;
        private Matrix4x4[] _currentGlobalTransforms;
        private Vector3 _cameraPosition = new Vector3(0, 0, 5);
        private Vector3 _cameraTarget = Vector3.Zero;
        private Vector3 _cameraUp = Vector3.UnitZ;
        private Quaternion _cameraRotation = Quaternion.Identity;
        private float _lastMouseX, _lastMouseY;
        private bool _firstMouse = true;
        private bool _isPanning = false;
        private string _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters", "man_mesh.fbx");
        private List<string> _animationFiles = new List<string>();
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
            _skeletonBuffer = new VertexBuffer(_renderContext);
            _pointShader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
            _textShader = new ShaderProgram(_renderContext, TextShader.VertexShaderSource, TextShader.FragmentShaderSource);
            _textRenderer = new EditorTextRenderer(_renderContext, _window);
            _textRenderer.Initialize(_textShader);
            LoadModel(_path);
            DiscoverAnimationFiles();
            UpdateUIControls();
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }
        private void LoadModel(string path)
        {
            var modelManager = new ModelManager(renderContext: _renderContext);
            modelManager.LoadModel(path, new HashSet<string>(), new Dictionary<string, string>());
            string key = Path.GetFileNameWithoutExtension(path).ToLower();
            if (modelManager.TryGetModel(key, out _model) && modelManager.TryGetModelData(key, out _modelData))
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
                _cameraPosition = center + new Vector3(0, 0, maxExtent * 3.5f);
                _cameraTarget = center;
                _cameraUp = Vector3.UnitZ;
                Console.WriteLine($"AssetViewerPanel: Model center: {center}, maxExtent: {maxExtent}, cameraPosition: {_cameraPosition}");
                if (_model.Animations.Count > 0)
                {
                    _currentAnimation = _model.Animations[0].Name;
                    _duration = _model.Animations[0].Keyframes.LastOrDefault()?.Time ?? 0f;
                }
                else
                {
                    _currentAnimation = null;
                    _duration = 0f;
                }
                _time = 0f;
                _playing = false;
                _currentGlobalTransforms = null;
            }
            else
            {
                Console.WriteLine("AssetViewerPanel: Failed to load selected model");
            }
        }
        private void DiscoverAnimationFiles()
        {
            string fbmDir = Path.Combine(Path.GetDirectoryName(_path), Path.GetFileNameWithoutExtension(_path) + ".fbm");
            if (Directory.Exists(fbmDir))
            {
                _animationFiles = Directory.GetFiles(fbmDir, "*.fbx").ToList();
                Console.WriteLine($"Discovered {_animationFiles.Count} animation files");
            }
        }
        private void LoadAnimation(string animPath)
        {
            var animForest = FBXParser.Load(animPath);
            var animModel = FBXParser.BuildModelFromForest(animForest, true);
            var validAnimations = animModel.Animations.Where(a => a.Keyframes.Count > 0).ToList();
            if (validAnimations.Count > 0)
            {
                var anim = validAnimations[0];
                anim.Name = Path.GetFileNameWithoutExtension(animPath);
                // Remap keyframes to main model's skeleton by bone name
                Dictionary<string, int> mainBoneIndices = new Dictionary<string, int>();
                for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
                {
                    mainBoneIndices[_model.Skeleton.Bones[i].Name.ToLowerInvariant()] = i;
                }
                HashSet<int> mappedBones = new HashSet<int>();
                foreach (var kf in anim.Keyframes)
                {
                    List<Matrix4x4> newTransforms = new List<Matrix4x4>();
                    for (int j = 0; j < _model.Skeleton.Bones.Count; j++)
                    {
                        newTransforms.Add(_model.Skeleton.Bones[j].LocalRest);
                    }
                    for (int i = 0; i < animModel.Skeleton.Bones.Count; i++)
                    {
                        string boneName = animModel.Skeleton.Bones[i].Name.ToLowerInvariant();
                        if (mainBoneIndices.TryGetValue(boneName, out int targetIdx))
                        {
                            newTransforms[targetIdx] = kf.BoneTransforms[i];
                            mappedBones.Add(targetIdx);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Bone {boneName} from animation not found in main model");
                        }
                            
                    }
                    kf.BoneTransforms = newTransforms;
                }
                Console.WriteLine($"Mapped {mappedBones.Count} unique bones for animation {anim.Name}");
                if (mappedBones.Count < _model.Skeleton.Bones.Count)
                {
                    Console.WriteLine($"Warning: Not all bones mapped for animation {anim.Name} ({mappedBones.Count}/{_model.Skeleton.Bones.Count})");
                }
                _model.Animations.Add(anim);
                _currentAnimation = anim.Name;
                _duration = anim.Keyframes.Count > 0 ? anim.Keyframes.Last().Time : 0f;
                _time = 0f;
                Console.WriteLine($"Loaded animation {anim.Name} with {anim.Keyframes.Count} keyframes");
            }
            else
            {
                Console.WriteLine($"No valid animations found in {animPath}");
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
        private void OnFileSelected(FileSelectedEvent e)
        {
            _path = e.Path;
            LoadModel(e.Path);
            DiscoverAnimationFiles();
            UpdateUIControls();
            _time = 0f;
            _playing = false;
            _cameraRotation = Quaternion.Identity;
            _firstMouse = true;
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
                    Console.WriteLine($"Selected and loaded animation: {Path.GetFileNameWithoutExtension(val)}");
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
                if (_time > _duration)
                {
                    _time = 0f;
                }
                if (_model.Skeleton != null && _model.Animations.Count > 0)
                {
                    var animation = _model.Animations.Find(a => a.Name == _currentAnimation);
                    if (animation != null)
                    {
                        var localTransforms = animation.GetBoneTransforms(_time);
                        var globalTransforms = _model.Skeleton.ComputeGlobalTransforms(localTransforms);
                        _currentGlobalTransforms = globalTransforms;
                        var finalTransforms = _model.Skeleton.ComputeFinalTransforms(globalTransforms);
                        _model.Skeleton.UpdateTransforms(finalTransforms);
                    }
                }
            }
            // Handle input for animation selection, play/pause, etc. (expand with UI buttons)
            if (_controlContext.GetKey(_window, Key.Space) == InputAction.Press)
            {
                _playing = !_playing;
            }
            UpdateSkeletonVisualization();
        }
        private void UpdateSkeletonVisualization()
        {
            if (_model?.Skeleton == null || _currentGlobalTransforms == null || _currentGlobalTransforms.Length != _model.Skeleton.Bones.Count) return;
            var vertices = new List<Vertex>();
            var indices = new List<uint>();
            uint idx = 0;
            for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
            {
                Matrix4x4.Decompose(_currentGlobalTransforms[i], out _, out _, out Vector3 pos);
                bool hasChild = false;
                for (int j = 0; j < _model.Skeleton.Bones.Count; j++)
                {
                    if (_model.Skeleton.Bones[j].ParentIndex == i)
                    {
                        hasChild = true;
                        Matrix4x4.Decompose(_currentGlobalTransforms[j], out _, out _, out Vector3 childPos);
                        vertices.Add(new Vertex(pos.X, pos.Y, pos.Z, 1, 0, 0, 1));
                        vertices.Add(new Vertex(childPos.X, childPos.Y, childPos.Z, 0, 1, 0, 1));
                        indices.Add(idx);
                        indices.Add(idx + 1);
                        idx += 2;
                    }
                }
                if (!hasChild && _model.Skeleton.Bones[i].BoneType == "LimbNode")
                {
                    // Draw a line for leaf bones using Size
                    Vector3 dir = new Vector3(0, 0, _model.Skeleton.Bones[i].Size); // along Z up
                    Matrix4x4 rotScale = _currentGlobalTransforms[i];
                    rotScale.Translation = Vector3.Zero;
                    Vector3 tailDir = Vector3.Transform(dir, rotScale);
                    Vector3 tail = pos + tailDir;
                    vertices.Add(new Vertex(pos.X, pos.Y, pos.Z, 1, 0, 0, 1));
                    vertices.Add(new Vertex(tail.X, tail.Y, tail.Z, 0, 1, 0, 1));
                    indices.Add(idx);
                    indices.Add(idx + 1);
                    idx += 2;
                }
            }
            _skeletonBuffer.UpdateCustom(vertices, indices);
        }
        private Vector3 ToEuler(Quaternion q)
        {
            Vector3 euler = new Vector3();
            float sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            float cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            euler.X = MathF.Atan2(sinr_cosp, cosr_cosp);
            float sinp = MathF.Sqrt(1 + 2 * (q.W * q.Y - q.X * q.Z));
            float cosp = MathF.Sqrt(1 - 2 * (q.W * q.Y - q.X * q.Z));
            euler.Y = 2 * MathF.Atan2(sinp, cosp) - MathF.PI / 2;
            float siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            float cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            euler.Z = MathF.Atan2(siny_cosp, cosy_cosp);
            return euler * (180f / MathF.PI);
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
                _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                int error;
                while ((error = _renderContext.GetError()) != _renderContext.Enums.NoError)
                {
                    Console.WriteLine($"AssetViewerPanel: OpenGL error after draw: {error}");
                }
            }
            // Render skeleton
            _pointShader.Use();
            _pointShader.SetMatrix4("uView", view);
            _pointShader.SetMatrix4("uProjection", projection);
            _skeletonBuffer.Bind();
            _renderContext.DrawElements(_renderContext.Enums.Lines, _skeletonBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            _renderContext.Clear(_renderContext.Enums.DepthBufferBit);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Disable(_renderContext.Enums.CullFace);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            // Render title bar
            _quadRenderer.DrawQuad(0, 0, Size.X, TitleHeight, new Vector4(0.2f, 0.2f, 0.2f, 1.0f), Size.X, Size.Y);
            // Render UI overlay
            _uiOverlay.Render();
            // Render bone info text
            float currentY = TitleHeight + 10;
            if (_model?.Skeleton != null && _currentGlobalTransforms != null && _currentGlobalTransforms.Length == _model.Skeleton.Bones.Count)
            {
                for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
                {
                    if (currentY > Size.Y - 20) break; // Prevent overflow
                    Matrix4x4.Decompose(_currentGlobalTransforms[i], out _, out Quaternion rot, out Vector3 pos);
                    Vector3 euler = ToEuler(rot);
                    string info = $"{_model.Skeleton.Bones[i].Name}: Pos({pos.X:F2},{pos.Y:F2},{pos.Z:F2}) Rot({euler.X:F2},{euler.Y:F2},{euler.Z:F2})";
                    _textRenderer.RenderText(info, 10, currentY, (int)Size.X, (int)Size.Y, 12f);
                    currentY += 15;
                }
            }
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