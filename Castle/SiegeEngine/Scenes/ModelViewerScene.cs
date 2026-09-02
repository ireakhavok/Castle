using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Lighting;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
namespace SiegeEngine.Scenes
{
    public unsafe class ModelViewerScene : Scene
    {
        public FBXModel _model;
        private ModelManager.ModelData _modelData;
        public List<int> HiddenMeshIndices { get; private set; } = new List<int>();
        private string _currentAnimationPath;
        private VertexBuffer _skeletonBuffer;
        private VertexBuffer _bindSkeletonBuffer;
        private ShaderProgram _pointShader;
        private ShaderProgram _textShader;
        private Matrix4x4[] _currentGlobalTransforms;
        private Matrix4x4[] _currentBindGlobals;
        private Matrix4x4[] _currentBindGlobalsVis;
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
        private List<string> _animationFiles = new List<string>();
        private float _cameraDistance;
        private float _maxExtent;
        private ModelManager _ModelManager;
        private string _currentModelKey;
        private bool _isPlaying = false;
        private float _currentTime = 0f;
        private float _duration = 0f;
        private int _currentFrameIndex = 0;
        private bool _showSkeleton = false;
        private bool _showBindPoseSkeleton = false;
        private AnimationBlendStack _blendPreviewStack;
        private Vector3 _blendPreviewParams = Vector3.Zero;
        private List<string> _lastAttachedPaths = new List<string>();
        private float _trimStart = 0f;
        private float _trimEnd = -1f;
        private float _playbackSpeed = 1f;
        public float CurrentTime
        {
            get => _currentTime;
            set
            {
                _currentTime = value;
                if (_model != null && _model.Animations.Count > 0)
                {
                    UpdateTransformsFromTime(_currentTime);
                }
            }
        }
        public ModelViewerScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            SetOwnsFramebuffer(false);
            _ModelManager = new ModelManager(_renderContext);
            _modelData = new ModelManager.ModelData();
        }
        public override void Initialize(int height, int width)
        {
            base.Initialize(height, width);
            _modelRenderer.Initialize();
            _skeletonBuffer = new VertexBuffer(_renderContext);
            _bindSkeletonBuffer = new VertexBuffer(_renderContext);
            _pointShader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
            _textShader = new ShaderProgram(_renderContext, TextShader.VertexShaderSource, TextShader.FragmentShaderSource);
            LoadMesh(_meshPath);
            DiscoverAnimationFiles();
        }
        private void ResetAnimationState()
        {
            _currentAnimationPath = null;
            _isPlaying = false;
            _currentTime = 0f;
            _duration = 0f;
            _currentFrameIndex = 0;
            _currentGlobalTransforms = null;
            _boneMatrices = null;
            _currentNormalTransforms = null;
            _trimStart = 0f;
            _trimEnd = -1f;
            _playbackSpeed = 1f;
        }
        public void LoadMesh(string path)
        {
            _meshPath = path;
            _ModelManager.LoadModel(path);
            _currentModelKey = Path.GetFileNameWithoutExtension(path).ToLower();
            _ModelManager.TryGetModel(_currentModelKey, out _model);
            _ModelManager.TryGetModelData(_currentModelKey, out _modelData);
            ResetAnimationState();
            if (_model.HasSkin)
            {
                SetRestPose();
            }
            CenterCamera();
        }
        public void LoadArmature(string path)
        {
            _armaturePath = path;
            _ModelManager.AttachSkeleton(_currentModelKey, path);
            _ModelManager.TryGetModel(_currentModelKey, out _model);
            _ModelManager.TryGetModelData(_currentModelKey, out _modelData);
            ResetAnimationState();
            if (_model.HasSkin)
            {
                SetRestPose();
            }
        }
        public void LoadAnimation(string animPath)
        {
            if (string.IsNullOrEmpty(animPath)) return;
            ResetAnimationState();
            FBXFileForest animForest = FBXParser.Load(animPath);
            FBXModel animModel = FBXParser.BuildModelFromForest(animForest);
            _ModelManager.AttachAnimation(_currentModelKey, animPath);
            _ModelManager.TryGetModel(_currentModelKey, out _model);
            ApplyRestPoseFromModel(animModel);
            SetRestPose();
            _currentAnimationPath = animPath;
            if (_model.Animations.Count > 0)
            {
                var newAnim = _model.Animations.Last();
                _duration = newAnim.Duration;
                _trimEnd = _duration;
                Console.WriteLine($"[ModelViewerScene] Loaded animation from {Path.GetFileName(animPath)} → duration {_duration:F2}s");
            }
            _currentTime = 0f;
            _isPlaying = false;
            _currentFrameIndex = 0;
            if (!string.IsNullOrEmpty(_currentAnimationPath))
            {
                UpdateTransformsFromTime(0f);
            }
        }
        public void AttachBlendAnimations(AnimationBlendStack stack)
        {
            if (stack == null || _model == null || string.IsNullOrEmpty(_currentModelKey) || stack.Clips.Count == 0) return;
            var uniquePaths = stack.Clips
                .Where(c => !string.IsNullOrEmpty(c.AnimationPath))
                .Select(c => c.AnimationPath)
                .Distinct()
                .ToList();
            bool attachedAny = false;
            foreach (var animPath in uniquePaths)
            {
                if (animPath == _currentAnimationPath) continue;
                try
                {
                    FBXFileForest animForest = FBXParser.Load(animPath);
                    FBXModel animModel = FBXParser.BuildModelFromForest(animForest);
                    _ModelManager.AttachAnimation(_currentModelKey, animPath);
                    _ModelManager.TryGetModel(_currentModelKey, out _model);
                    ApplyRestPoseFromModel(animModel);
                    attachedAny = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ModelViewerScene] Failed to attach additional blend animation {animPath}: {ex.Message}");
                }
            }
            if (attachedAny)
            {
                SetRestPose();
            }
        }
        private void ApplyRestPoseFromModel(FBXModel sourceModel)
        {
            var targetSkeleton = _model.Skeleton;
            if (sourceModel.Skeleton == null || targetSkeleton == null) return;
            var nameToTargetBone = targetSkeleton.Bones.ToDictionary(b => b.Name.ToLowerInvariant());
            foreach (var srcBone in sourceModel.Skeleton.Bones)
            {
                string key = srcBone.Name.ToLowerInvariant();
                if (nameToTargetBone.TryGetValue(key, out Bone tgtBone))
                {
                    tgtBone.LclTranslation = srcBone.LclTranslation;
                    tgtBone.LclRotation = srcBone.LclRotation;
                    tgtBone.LclScaling = srcBone.LclScaling;
                    tgtBone.PreRotation = srcBone.PreRotation;
                    tgtBone.PostRotation = srcBone.PostRotation;
                    tgtBone.RotationPivot = srcBone.RotationPivot;
                    tgtBone.RotationOffset = srcBone.RotationOffset;
                    tgtBone.ScalingPivot = srcBone.ScalingPivot;
                    tgtBone.ScalingOffset = srcBone.ScalingOffset;
                    tgtBone.RotationOrder = srcBone.RotationOrder;
                    tgtBone.InheritType = srcBone.InheritType;
                    tgtBone.GeometricTranslation = srcBone.GeometricTranslation;
                    tgtBone.GeometricRotation = srcBone.GeometricRotation;
                    tgtBone.GeometricScaling = srcBone.GeometricScaling;
                    tgtBone.GeometricTransform = srcBone.GeometricTransform;
                    tgtBone.LocalRest = tgtBone.ComputeLocal();
                }
            }
        }
        private void UpdateTransformsFromTime(float time)
        {
            if (_model == null || _model.Skeleton == null || string.IsNullOrEmpty(_currentAnimationPath) || _model.Animations.Count == 0) return;
            var tempStack = new AnimationBlendStack();
            tempStack.Clips.Add(new AnimationClipEntry
            {
                AnimationPath = _currentAnimationPath,
                LocalTime = time,
                StartFrame = _trimStart,
                EndFrame = _trimEnd > 0 ? _trimEnd : -1f,
                PlaybackSpeed = _playbackSpeed,
                Loop = false
            });
            var locals = tempStack.ComputeBlendedLocals(Vector3.Zero, 0f, false, _model);
            if (locals == null) return;
            _currentGlobalTransforms = _model.Skeleton.ComputeGlobalTransforms(locals);
            _boneMatrices = new Matrix4x4[_model.Skeleton.Bones.Count];
            _currentNormalTransforms = new Matrix3x3[_model.Skeleton.Bones.Count];
            for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
            {
                _boneMatrices[i] = _model.Skeleton.Bones[i].BindPose * _currentGlobalTransforms[i];
                if (Matrix4x4.Invert(_boneMatrices[i], out Matrix4x4 inv))
                {
                    Matrix4x4 invT = Matrix4x4.Transpose(inv);
                    _currentNormalTransforms[i] = new Matrix3x3(
                        invT.M11, invT.M12, invT.M13,
                        invT.M21, invT.M22, invT.M23,
                        invT.M31, invT.M32, invT.M33);
                }
                else
                {
                    _currentNormalTransforms[i] = Matrix3x3.Identity;
                }
            }
            UpdateSkeletonVisualization();
        }
        private void ComputeBlendedTransforms(float deltaTime)
        {
            if (_blendPreviewStack == null || _blendPreviewStack.Clips.Count == 0 || _model == null || _model.Skeleton == null) return;
            var blendedLocals = _blendPreviewStack.ComputeBlendedLocals(_blendPreviewParams, deltaTime, _isPlaying, _model);
            if (blendedLocals == null) return;
            _currentGlobalTransforms = _model.Skeleton.ComputeGlobalTransforms(blendedLocals);
            _boneMatrices = new Matrix4x4[_model.Skeleton.Bones.Count];
            _currentNormalTransforms = new Matrix3x3[_model.Skeleton.Bones.Count];
            for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
            {
                _boneMatrices[i] = _model.Skeleton.Bones[i].BindPose * _currentGlobalTransforms[i];
                if (Matrix4x4.Invert(_boneMatrices[i], out Matrix4x4 inv))
                {
                    Matrix4x4 invT = Matrix4x4.Transpose(inv);
                    _currentNormalTransforms[i] = new Matrix3x3(
                        invT.M11, invT.M12, invT.M13,
                        invT.M21, invT.M22, invT.M23,
                        invT.M31, invT.M32, invT.M33);
                }
                else
                {
                    _currentNormalTransforms[i] = Matrix3x3.Identity;
                }
            }
            UpdateSkeletonVisualization();
        }
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
        public void DiscoverAnimationFiles()
        {
            string fbmDir = Path.Combine(Path.GetDirectoryName(_meshPath), Path.GetFileNameWithoutExtension(_meshPath) + ".fbm");
            if (Directory.Exists(fbmDir))
            {
                _animationFiles = Directory.GetFiles(fbmDir, "*.fbx").ToList();
            }
        }
        private void UpdateSkeletonVisualization()
        {
            if (_model == null || _model.Skeleton == null || _currentGlobalTransforms == null || _currentGlobalTransforms.Length != _model.Skeleton.Bones.Count) return;
            var vertices = new List<Vertex>();
            var indices = new List<uint>();
            for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
            {
                if (!_model.Skeleton.Bones[i].IsDrawable) continue;
                Vector3 pos = _currentGlobalTransforms[i].Translation;
                vertices.Add(new Vertex(pos.X, pos.Y, pos.Z, 0, 1, 0, 1));
            }
            for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
            {
                if (!_model.Skeleton.Bones[i].IsDrawable) continue;
                if (_model.Skeleton.Bones[i].ParentIndex >= 0)
                {
                    indices.Add((uint)_model.Skeleton.Bones[i].ParentIndex);
                    indices.Add((uint)i);
                }
            }
            _skeletonBuffer.UpdateCustom(vertices, indices);
        }
        private void SetRestPose()
        {
            Matrix4x4[] restLocals = new Matrix4x4[_model.Skeleton.Bones.Count];
            for (int i = 0; i < restLocals.Length; i++)
            {
                restLocals[i] = _model.Skeleton.Bones[i].LocalRest;
            }
            if (_model.Skeleton == null || _model.Skeleton.Bones.Count == 0) return;
            _currentGlobalTransforms = _model.Skeleton.ComputeGlobalTransforms();
            _currentBindGlobals = new Matrix4x4[_model.Skeleton.Bones.Count];
            _currentBindGlobalsVis = new Matrix4x4[_model.Skeleton.Bones.Count];
            for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
            {
                var bindPose = _model.Skeleton.Bones[i].BindPose;
                if (Matrix4x4.Invert(bindPose, out Matrix4x4 globalBind))
                {
                    _currentBindGlobalsVis[i] = globalBind;
                    _currentBindGlobals[i] = globalBind;
                }
                else
                {
                    _currentBindGlobals[i] = Matrix4x4.Identity;
                    _currentBindGlobalsVis[i] = Matrix4x4.Identity;
                }
            }
            UpdateSkeletonVisualization();
            if (_model.Skeleton != null && _currentBindGlobalsVis != null)
            {
                var vertices = new List<Vertex>();
                var indices = new List<uint>();
                for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
                {
                    if (!_model.Skeleton.Bones[i].IsDrawable) continue;
                    Vector3 pos = _currentBindGlobalsVis[i].Translation;
                    vertices.Add(new Vertex(pos.X, pos.Y, pos.Z, 0, 1, 0, 1));
                }
                for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
                {
                    if (!_model.Skeleton.Bones[i].IsDrawable) continue;
                    if (_model.Skeleton.Bones[i].ParentIndex >= 0)
                    {
                        indices.Add((uint)_model.Skeleton.Bones[i].ParentIndex);
                        indices.Add((uint)i);
                    }
                }
                _bindSkeletonBuffer.UpdateCustom(vertices, indices);
            }
            _boneMatrices = new Matrix4x4[_model.Skeleton.Bones.Count];
            _currentNormalTransforms = new Matrix3x3[_model.Skeleton.Bones.Count];
            for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
            {
                _boneMatrices[i] = _model.Skeleton.Bones[i].BindPose * _currentGlobalTransforms[i];
                if (Matrix4x4.Invert(_boneMatrices[i], out Matrix4x4 inv))
                {
                    Matrix4x4 invT = Matrix4x4.Transpose(inv);
                    _currentNormalTransforms[i] = new Matrix3x3(
                        invT.M11, invT.M12, invT.M13,
                        invT.M21, invT.M22, invT.M23,
                        invT.M31, invT.M32, invT.M33);
                }
                else
                {
                    _currentNormalTransforms[i] = Matrix3x3.Identity;
                }
            }
        }
        public void SetBlendPreview(AnimationBlendStack stack, Vector3 currentParams)
        {
            _blendPreviewStack = stack;
            _blendPreviewParams = currentParams;
            if (stack != null)
            {
                foreach (var clip in stack.Clips)
                {
                    if (clip.LocalTime == 0f && _lastAttachedPaths.Count == 0)
                        clip.LocalTime = 0f;
                }
                if (stack.Clips.Count > 0)
                {
                    var currentPaths = stack.Clips.Select(c => c.AnimationPath).Where(p => !string.IsNullOrEmpty(p)).ToList();
                    bool pathsChanged = !_lastAttachedPaths.SequenceEqual(currentPaths);
                    if (pathsChanged)
                    {
                        AttachBlendAnimations(stack);
                        _lastAttachedPaths = new List<string>(currentPaths);
                    }
                    ComputeBlendedTransforms(0f);
                }
            }
        }
        public void UpdateBlendPreviewParams(Vector3 currentParams)
        {
            _blendPreviewParams = currentParams;
            if (_blendPreviewStack != null && _blendPreviewStack.Clips.Count > 0)
            {
                ComputeBlendedTransforms(0f);
            }
        }
        public void SetTrimParams(float start, float end, float speed)
        {
            _trimStart = Math.Max(0, start);
            _trimEnd = end > 0 ? end : _duration;
            _playbackSpeed = Math.Max(0.1f, speed);
            if (_currentTime < _trimStart || (_trimEnd > 0 && _currentTime > _trimEnd))
                _currentTime = _trimStart;
        }
        public List<string> GetAnimationFiles()
        {
            return _animationFiles;
        }
        public string GetFrameInfo()
        {
            string frameInfo = "Keyframe: " + _currentFrameIndex;
            if (_model?.Animations.Count > 0)
            {
                frameInfo += " / " + (_model.Animations.Last().Keyframes.Count - 1);
            }
            return frameInfo;
        }
        public void TogglePlay()
        {
            _isPlaying = !_isPlaying;
        }
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }
        public void Update(float deltaTime, Vector2 relMousePos, bool mouseDown, bool mousePressed, bool mouseReleased)
        {
            float mouseX = relMousePos.X;
            float mouseY = relMousePos.Y;
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
            if (mouseDown && !mousePressed)
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
            if (_blendPreviewStack != null && _blendPreviewStack.Clips.Count > 0)
            {
                ComputeBlendedTransforms(deltaTime);
            }
            else if (_model.Skeleton != null && _model.Animations.Count > 0)
            {
                var animation = _model.Animations.Last();
                if (animation.Keyframes.Count > 0)
                {
                    if (_isPlaying)
                    {
                        float effectiveDuration = (_trimEnd > _trimStart) ? (_trimEnd - _trimStart) : _duration;
                        _currentTime += deltaTime * _playbackSpeed;
                        if (_currentTime >= _trimStart + effectiveDuration)
                        {
                            _currentTime = _trimStart;
                        }
                    }
                    else
                    {
                        int lower = 0;
                        int upper = animation.Keyframes.Count - 1;
                        for (int i = 1; i < animation.Keyframes.Count; i++)
                        {
                            if (animation.Keyframes[i].Time > _currentTime)
                            {
                                upper = i;
                                lower = i - 1;
                                break;
                            }
                        }
                        if (_controlContext.GetKey(_window, Key.Right) == InputAction.Press)
                        {
                            int next = upper;
                            _currentTime = animation.Keyframes[next].Time;
                        }
                        if (_controlContext.GetKey(_window, Key.Left) == InputAction.Press)
                        {
                            int prev = lower - 1;
                            if (prev >= 0) _currentTime = animation.Keyframes[prev].Time;
                        }
                    }
                    float displayTime = Math.Max(_trimStart, Math.Min(_currentTime, _trimEnd > 0 ? _trimEnd : _duration));
                    UpdateTransformsFromTime(displayTime);
                }
            }
            UpdateSkeletonVisualization();
        }
        protected override void GetViewProjection(out Matrix4x4 view, out Matrix4x4 projection)
        {
            view = Matrix4x4.CreateLookAt(_cameraPosition, _cameraTarget, _cameraUp);
            projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, AspectRatio, 0.1f, 1000f);
        }
        public override void Render(IReadOnlyList<Entity> entities)
        {
            GetViewProjection(out Matrix4x4 view, out Matrix4x4 projection);
            LightingFrame prev = LightingFrame.Current;
            LightingFrame.Current = LightingFrame.Studio(_cameraPosition, _cameraTarget);
            try
            {
                RenderContent(entities, view, projection);
            }
            finally
            {
                LightingFrame.Current = prev;
            }
        }

        protected override List<ShadowCaster> CollectShadowCasters(IReadOnlyList<Entity> entities)
        {
            return new List<ShadowCaster>();
        }

        public void SetMeshHidden(int meshIndex, bool hidden)
        {
            if (HiddenMeshIndices == null) HiddenMeshIndices = new List<int>();
            if (hidden)
            {
                if (!HiddenMeshIndices.Contains(meshIndex)) HiddenMeshIndices.Add(meshIndex);
            }
            else
            {
                HiddenMeshIndices.Remove(meshIndex);
            }
            Console.WriteLine($"[ModelViewerScene] Mesh {meshIndex} hidden={hidden} list=[{string.Join(",", HiddenMeshIndices)}]");
        }

        public int GetMeshCount()
        {
            if (_modelData?.MeshRenders != null) return _modelData.MeshRenders.Count;
            if (_model?.Meshes != null) return _model.Meshes.Count;
            return 0;
        }

        public FBXModel GetModel() => _model;

        public IReadOnlyList<Material> GetMeshMaterials(int meshIndex)
        {
            if (_model?.Meshes == null || meshIndex < 0 || meshIndex >= _model.Meshes.Count)
                return System.Array.Empty<Material>();
            var mats = _model.Meshes[meshIndex].Materials;
            return mats ?? (IReadOnlyList<Material>)System.Array.Empty<Material>();
        }

        protected override void RenderContent(IReadOnlyList<Entity> entities, Matrix4x4 view, Matrix4x4 projection)
        {
            _modelRenderer.RenderModel(_model, _modelData, view, projection, _cameraPosition, Matrix4x4.Identity, _boneMatrices, _currentNormalTransforms, receiveShadows: false, hiddenMeshIndices: HiddenMeshIndices);
            if (_showSkeleton) _modelRenderer.RenderSkeletonDebug(_skeletonBuffer, _pointShader, view, projection);
            if (_bindSkeletonBuffer != null && _showBindPoseSkeleton) _modelRenderer.RenderSkeletonDebug(_bindSkeletonBuffer, _pointShader, view, projection);
        }
        public override void Dispose()
        {
            _pointShader?.Dispose();
            _textShader?.Dispose();
            _skeletonBuffer?.Dispose();
            _bindSkeletonBuffer?.Dispose();
            base.Dispose();
        }
    }
}
