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
using SiegeEngine.Core.AssetParsing.Model;

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
            _animationFiles.Clear();
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
                float cameraDist = Math.Max(maxExtent * 3.5f, 0.1f);
                _cameraPosition = center + new Vector3(0, 0, cameraDist);
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
                _skeletonBuffer.UpdateCustom(new List<Vertex>(), new List<uint>());
                _assetShader = new ShaderProgram(_renderContext, AnimationShader.VertexShaderSource, AnimationShader.FragmentShaderSource);

                // Compute bind poses after loading the model
                _model.ComputeBindPoses();
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
            int[] sourceToTarget = _model.SourceToTarget ?? new int[3];
            int[] signs = _model.Signs ?? new int[3];
            float modelScale = _model.ModelScale;
            Matrix4x4 P4 = _model.P4;
            Matrix4x4 invP4 = _model.InvP4;
            bool reverseWinding = _model.ReverseWinding;
            if (sourceToTarget.Length == 0)
            {
                (sourceToTarget, signs, modelScale, P4, invP4, reverseWinding) = FBXParser.ParseGlobalSettingsAndRemapping(animForest);
            }
            var objectsNode = animForest.TreeList.FirstOrDefault(n => n.Name == "Objects");
            var objectsById = FBXParser.GatherObjectsById(objectsNode);
            var conns = FBXParser.GatherConnections(animForest);
            var animModel = new FBXModel();
            animModel.SourceToTarget = sourceToTarget;
            animModel.Signs = signs;
            animModel.ModelScale = modelScale;
            animModel.P4 = P4;
            animModel.InvP4 = invP4;
            animModel.ReverseWinding = reverseWinding;
            var (boneIndexById, rootIndices) = FBXSkeletonParser.ParseSkeleton(animModel, objectsNode, objectsById, conns, sourceToTarget, signs, modelScale);
            FBXSkeletonParser.BuildHierarchy(animModel, conns, boneIndexById);
            Matrix4x4 rootRot = Matrix4x4.Identity; // Simplified: No root alignment computation
            List<int> animRootIndices = new List<int>();
            for (int i = 0; i < animModel.Skeleton.Bones.Count; i++)
            {
                if (animModel.Skeleton.Bones[i].ParentIndex == -1)
                    animRootIndices.Add(i);
            }
            // Removed ApplyRootRotation call
            FBXAnimationParser.ParseAnimations(animModel, objectsNode, conns, objectsById, boneIndexById, sourceToTarget, signs, modelScale, rootRot, animRootIndices, P4, invP4);
            // Moved ParseMeshes inside the weight copy check below
            var validAnimations = animModel.Animations.Where(a => a.Keyframes.Count > 0).ToList();
            if (validAnimations.Count > 0)
            {
                var anim = validAnimations[0];
                anim.Name = Path.GetFileNameWithoutExtension(animPath);
                if (_model.Skeleton == null || _model.Skeleton.Bones.Count == 0)
                {
                    _model.Skeleton = new Skeleton();
                    _model.Skeleton.Bones = new List<Bone>();
                    for (int i = 0; i < animModel.Skeleton.Bones.Count; i++)
                    {
                        var copiedBone = new Bone
                        {
                            Name = animModel.Skeleton.Bones[i].Name,
                            BindPose = animModel.Skeleton.Bones[i].BindPose,
                            ParentIndex = animModel.Skeleton.Bones[i].ParentIndex,
                            LocalRest = animModel.Skeleton.Bones[i].LocalRest,
                            LclTranslation = animModel.Skeleton.Bones[i].LclTranslation,
                            LclRotation = animModel.Skeleton.Bones[i].LclRotation,
                            LclScaling = animModel.Skeleton.Bones[i].LclScaling,
                            PreRotation = animModel.Skeleton.Bones[i].PreRotation,
                            PostRotation = animModel.Skeleton.Bones[i].PostRotation,
                            RotationPivot = animModel.Skeleton.Bones[i].RotationPivot,
                            RotationOffset = animModel.Skeleton.Bones[i].RotationOffset,
                            ScalingPivot = animModel.Skeleton.Bones[i].ScalingPivot,
                            ScalingOffset = animModel.Skeleton.Bones[i].ScalingOffset,
                            RotationOrder = animModel.Skeleton.Bones[i].RotationOrder,
                            BoneType = animModel.Skeleton.Bones[i].BoneType,
                            Size = animModel.Skeleton.Bones[i].Size,
                            GeometricTranslation = animModel.Skeleton.Bones[i].GeometricTranslation,
                            GeometricRotation = animModel.Skeleton.Bones[i].GeometricRotation,
                            GeometricScaling = animModel.Skeleton.Bones[i].GeometricScaling
                        };
                        _model.Skeleton.Bones.Add(copiedBone);
                    }
                    Console.WriteLine("Copied skeleton from animation model to main model");
                    // Compute bind poses after copying skeleton
                    _model.ComputeBindPoses();
                }
                // Remap keyframes to main model's skeleton by name with retargeting
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
                            Matrix4x4 local = kf.BoneTransforms[i];
                            if (Matrix4x4.Invert(animModel.Skeleton.Bones[i].LocalRest, out Matrix4x4 invAnimRest))
                            {
                                Matrix4x4 delta = invAnimRest * local;
                                Matrix4x4 modelRest = _model.Skeleton.Bones[targetIdx].LocalRest;
                                Matrix4x4 newLocal = modelRest * delta;
                                newTransforms[targetIdx] = newLocal;
                            }
                            else
                            {
                                Console.WriteLine($"Failed to invert anim rest for bone {boneName}, using anim local directly");
                                newTransforms[targetIdx] = local;
                            }
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
                // Add mapping threshold check
                if (mappedBones.Count < (int)(_model.Skeleton.Bones.Count * 0.8f))
                {
                    Console.WriteLine($"Error: Insufficient bone mapping for animation {anim.Name} ({mappedBones.Count}/{_model.Skeleton.Bones.Count}), skipping");
                    return;
                }
                _model.Animations.Add(anim);
                _currentAnimation = anim.Name;
                _duration = anim.Keyframes.Count > 0 ? anim.Keyframes.Last().Time : 0f;
                _time = 0f;
                Console.WriteLine($"Loaded animation {anim.Name} with {anim.Keyframes.Count} keyframes");
                // Copy weights if main model has unweighted vertices
                if (_model.HasUnweightedVertices())
                {
                    // Parse meshes only if needed for weights
                    FBXMeshParser.ParseMeshes(animModel, objectsNode, conns, objectsById, sourceToTarget, signs, modelScale, reverseWinding, boneIndexById, rootRot, animRootIndices, P4, invP4, animForest);
                    _model.CopyWeightsFrom(animModel);
                    Console.WriteLine($"Copied weights from animation model to main model for {anim.Name}");
                    // Update VBOs with new vertex data (weights updated)
                    UpdateModelBuffers();
                }
                // Switch to animation shader if skin present
                if (_model.HasSkin)
                {
                    _assetShader = new ShaderProgram(_renderContext, AnimationShader.VertexShaderSource, AnimationShader.FragmentShaderSource);
                }
                _skeletonBuffer.UpdateCustom(new List<Vertex>(), new List<uint>());
            }
            else
            {
                Console.WriteLine($"No valid animations found in {animPath}");
            }
        }
        private void UpdateModelBuffers()
        {
            if (_model == null || _modelData == null || _modelData.MeshRenders.Count != _model.Meshes.Count)
            {
                Console.WriteLine("UpdateModelBuffers: Mismatch in mesh/render count, skipping update");
                return;
            }
            for (int mi = 0; mi < _model.Meshes.Count; mi++)
            {
                var mesh = _model.Meshes[mi];
                var mmr = _modelData.MeshRenders[mi];
                float[] vertexData = new float[mesh.Vertices.Count * 20];
                for (int vi = 0; vi < mesh.Vertices.Count; vi++)
                {
                    var vertex = mesh.Vertices[vi];
                    int offset = vi * 20;
                    vertexData[offset + 0] = vertex.X;
                    vertexData[offset + 1] = vertex.Y;
                    vertexData[offset + 2] = vertex.Z;
                    vertexData[offset + 3] = vertex.Nx;
                    vertexData[offset + 4] = vertex.Ny;
                    vertexData[offset + 5] = vertex.Nz;
                    vertexData[offset + 6] = vertex.U;
                    vertexData[offset + 7] = vertex.V;
                    vertexData[offset + 8] = vertex.MatIdx;
                    vertexData[offset + 9] = vertex.Tx;
                    vertexData[offset + 10] = vertex.Ty;
                    vertexData[offset + 11] = vertex.Tz;
                    vertexData[offset + 12] = (float)vertex.BoneID0;
                    vertexData[offset + 13] = (float)vertex.BoneID1;
                    vertexData[offset + 14] = (float)vertex.BoneID2;
                    vertexData[offset + 15] = (float)vertex.BoneID3;
                    vertexData[offset + 16] = vertex.Weight0;
                    vertexData[offset + 17] = vertex.Weight1;
                    vertexData[offset + 18] = vertex.Weight2;
                    vertexData[offset + 19] = vertex.Weight3;
                }
                _renderContext.BindVertexArray(mmr.Vao);
                _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, mmr.Vbo);
                fixed (float* ptr = vertexData)
                {
                    _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(vertexData.Length * sizeof(float)), ptr, _renderContext.Enums.StaticDraw);
                }
                _renderContext.BindVertexArray(0);
                Console.WriteLine($"Updated VBO for mesh {mi} with new weights");
            }
            _model.HasSkin = true;
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