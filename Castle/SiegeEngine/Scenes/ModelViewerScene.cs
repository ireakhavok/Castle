// Folder: SiegeEngine.Scenes
// File: ModelViewerScene.cs
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
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
namespace SiegeEngine.Scenes
{
    public unsafe class ModelViewerScene : Scene
    {
        private FBXModel _model;
        private ModelManager.ModelData _modelData;
        private string _currentAnimation;
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
        private string _animationPath = "";
        private List<string> _animationFiles = new List<string>();
        private float _cameraDistance;
        private float _maxExtent;
        private ModelManager _ModelManager;
        private string _currentModelKey;
        private bool _isPlaying = false;
        private float _currentTime = 0f;
        private float _duration = 0f;
        private int _currentFrameIndex = 0;

        // Toggle for animated/current skeleton visualization (off for now, code left intact)
        private bool _showSkeleton = false;
        private bool _showBindPoseSkeleton = false; // separate toggle for bind-pose skeleton (turned on by default)

        public ModelViewerScene(IRenderContext renderContext, IControlContext controlContext, nint window, IGameServer server, EventBus eventBus)
            : base(renderContext, controlContext, window, server, eventBus)
        {
            _ModelManager = new ModelManager(_renderContext);
            _modelData = new ModelManager.ModelData();
        }
        public override void Initialize(int height, int width)
        {
            base.Initialize(height, width);
            _skeletonBuffer = new VertexBuffer(_renderContext);
            _bindSkeletonBuffer = new VertexBuffer(_renderContext);
            _pointShader = new ShaderProgram(_renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
            _textShader = new ShaderProgram(_renderContext, TextShader.VertexShaderSource, TextShader.FragmentShaderSource);
            LoadMesh(_meshPath);
            DiscoverAnimationFiles();
        }
        public void LoadMesh(string path)
        {
            _meshPath = path;
            _ModelManager.LoadModel(path);
            _currentModelKey = Path.GetFileNameWithoutExtension(path).ToLower();
            _ModelManager.TryGetModel(_currentModelKey, out _model);
            _ModelManager.TryGetModelData(_currentModelKey, out _modelData);
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
            if (_model.HasSkin)
            {
                SetRestPose();
            }
        }
        public void LoadAnimation(string animPath)
        {
            FBXFileForest animForest = FBXParser.Load(animPath);
            FBXModel animModel = FBXParser.BuildModelFromForest(animForest);
            _ModelManager.AttachAnimation(_currentModelKey, animPath);
            _ModelManager.TryGetModel(_currentModelKey, out _model);
            ApplyRestPoseFromModel(animModel);
            SetRestPose();
            if (_model.Animations.Count > 0)
            {
                _currentAnimation = _model.Animations.Last().Name;
                _duration = _model.Animations.Last().Duration;
                _currentTime = 0f;
                _isPlaying = false;
                _currentFrameIndex = 0;
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
            if (_model == null || _model.Skeleton == null || string.IsNullOrEmpty(_currentAnimation)) return;
            var animation = _model.Animations.FirstOrDefault(a => a.Name == _currentAnimation);
            if (animation == null || animation.Keyframes.Count == 0) return;
            int lower = 0;
            int upper = animation.Keyframes.Count - 1;
            for (int i = 1; i < animation.Keyframes.Count; i++)
            {
                if (animation.Keyframes[i].Time > time)
                {
                    upper = i;
                    lower = i - 1;
                    break;
                }
            }
            float t0 = animation.Keyframes[lower].Time;
            float t1 = animation.Keyframes[upper].Time;
            float frac = (t1 - t0 > 0) ? (time - t0) / (t1 - t0) : 0f;
            _currentFrameIndex = lower;
            //Console.WriteLine($"Time {time}: lower={lower} (t0={t0}), upper={upper} (t1={t1}), frac={frac}");
            var l0 = animation.Keyframes[lower].BoneTransforms;
            var l1 = animation.Keyframes[upper].BoneTransforms;
            var lerpedLocals = new Matrix4x4[l0.Count];
            for (int i = 0; i < lerpedLocals.Length; i++)
            {
                var lm0 = l0[i];
                var lm1 = l1[i];
                if (Matrix4x4.Decompose(lm0, out Vector3 s0, out Quaternion r0, out Vector3 p0) && Matrix4x4.Decompose(lm1, out Vector3 s1, out Quaternion r1, out Vector3 p1))
                {
                    Vector3 p = Vector3.Lerp(p0, p1, frac);
                    Quaternion r = Quaternion.Normalize(Quaternion.Slerp(r0, r1, frac));
                    Vector3 s = Vector3.Lerp(s0, s1, frac);
                    lerpedLocals[i] = _model.Skeleton.Bones[i].ComputeLocal(p, r, s);
                }
                else
                {
                    lerpedLocals[i] = lm0;
                }
            }
            //if (_currentTime == 0)
            //{
            //    //Console.WriteLine("Lerped Locals at First Keyframe:");
            //    for (int i = 0; i < lerpedLocals.Length; i++)
            //    {
            //        //Console.WriteLine($"Bone {i} Lerped Local:");
            //        FBXParserUtils.PrintMatrix(lerpedLocals[i]);
            //    }
            //}
            _currentGlobalTransforms = _model.Skeleton.ComputeGlobalTransforms(lerpedLocals);
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
            //Console.WriteLine("Rest Pose Locals:");
            //for (int i = 0; i < restLocals.Length; i++)
            //{
            //    Console.WriteLine($"Bone {i} Rest Local:");
            //    FBXParserUtils.PrintMatrix(restLocals[i]);
            //}
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

            // Minimal setup for bind-pose skeleton visualization (turned on)
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
        public List<string> GetAnimationFiles()
        {
            return _animationFiles;
        }
        public string GetFrameInfo()
        {
            string frameInfo = "Keyframe: " + _currentFrameIndex;
            var animation = _model?.Animations.FirstOrDefault(a => a.Name == _currentAnimation);
            if (animation != null)
            {
                frameInfo += " / " + (animation.Keyframes.Count - 1);
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
            base.Update(deltaTime);
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
            if (_model.Skeleton != null && _model.Animations.Count > 0)
            {
                var animation = _model.Animations.FirstOrDefault(a => a.Name == _currentAnimation);
                if (animation != null && animation.Keyframes.Count > 0)
                {
                    if (_isPlaying)
                    {
                        _currentTime += deltaTime;
                        if (_currentTime > _duration) _currentTime -= _duration;
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
                    UpdateTransformsFromTime(_currentTime);
                }
            }
            UpdateSkeletonVisualization();
        }
        public override void Render(IReadOnlyList<Entity> entities)
        {
            _renderContext.ClearColor(0.118f, 0.118f, 0.118f, 1.0f);
            _renderContext.Clear(_renderContext.Enums.ColorBufferBit | _renderContext.Enums.DepthBufferBit);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.DepthFunc(_renderContext.Enums.Less);
            _renderContext.DepthMask(true);
            _renderContext.Enable(_renderContext.Enums.CullFace);
            _renderContext.FrontFace(_renderContext.Enums.CounterClockwise);
            _renderContext.CullFace(_renderContext.Enums.Back);
            _renderContext.Disable(_renderContext.Enums.Blend);
            Matrix4x4 modelMatrix = Matrix4x4.Identity;
            Matrix4x4 view = Matrix4x4.CreateLookAt(_cameraPosition, _cameraTarget, _cameraUp);
            float currentDist = Vector3.Distance(_cameraPosition, _cameraTarget);
            float near = Math.Max(0.01f, currentDist - _maxExtent * 2f);
            float far = currentDist + _maxExtent * 2f;
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, AspectRatio, near, far);

            // Consolidated model rendering via shared ModelRenderer (viewer context: identity transform + custom camera + precomputed bone matrices)
            _modelRenderer.RenderModel(_model, _modelData, view, projection, _cameraPosition, modelMatrix, _boneMatrices, _currentNormalTransforms);

            // Skeleton visualization (animated/current pose - toggled off for now; code left intact)
            // Bind-pose skeleton visualization (turned on)
            _pointShader.Use();
            _pointShader.SetMatrix4("uModel", modelMatrix);
            _pointShader.SetMatrix4("uView", view);
            _pointShader.SetMatrix4("uProjection", projection);

            if (_showSkeleton)
            {
                _renderContext.BindVertexArray(_skeletonBuffer.Vao);
                _renderContext.DrawElements(_renderContext.Enums.Lines, _skeletonBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                _renderContext.BindVertexArray(0);
            }

            // Bind pose skeleton (always drawn)
            if (_bindSkeletonBuffer != null && _showBindPoseSkeleton)
            {
                _renderContext.BindVertexArray(_bindSkeletonBuffer.Vao);
                _renderContext.DrawElements(_renderContext.Enums.Lines, _bindSkeletonBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                _renderContext.BindVertexArray(0);
            }

            _renderContext.Clear(_renderContext.Enums.DepthBufferBit);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Disable(_renderContext.Enums.CullFace);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
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