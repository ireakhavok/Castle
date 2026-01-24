using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace ReadingChamber
{
    public unsafe class AssetViewerPanel : BasePanel
    {
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new AssetViewerPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel));
        }
        private class AssetUIOverlay : UIOverlay
        {
            private readonly AssetViewerPanel _parent;
            public AssetUIOverlay(AssetViewerPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window) : base(renderContext, controlContext, window)
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
        public AssetViewerPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus) : base(renderContext, controlContext, window, eventBus)
        {
            _assetShader = new ShaderProgram(_renderContext, AssetShader.VertexShaderSource, AssetShader.FragmentShaderSource);
            Scaling = ScalingMode.BestFit;
            BaseWidth = 1280f;
            BaseHeight = 720f;
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
            LoadMesh(_meshPath);
            DiscoverAnimationFiles();
            UpdateUIControls();
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
            SetRestPose();
        }
        private void LoadMesh(string path)
        {
            _meshPath = path;
            var forest = FBXParser.Load(path);
            var parsedModel = FBXParser.BuildModelFromForest(forest);
            _model = parsedModel;
            if (_model.HasUnweightedVertices())
            {
                _model.FixUnweightedVertices();
            }
            UpdateModelData();
            if (_model.HasSkin)
            {
            }
            CenterCamera();
            SetRestPose();
        }
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
            for (int i = 0; i < tempSkeleton.Bones.Count; i++)
            {
                var bone = tempSkeleton.Bones[i];
                // Unremap to source
                bone.LclTranslation = FBXCoordinateUtils.UnremapVector(bone.LclTranslation, parsedModel.SourceToTarget, parsedModel.Signs);
                Vector3 lclRotDegUn = bone.ToEuler(bone.LclRotation);
                lclRotDegUn = FBXCoordinateUtils.UnremapRotation(lclRotDegUn, parsedModel.SourceToTarget, parsedModel.Signs);
                bone.LclRotation = bone.ToQuaternion(lclRotDegUn, bone.RotationOrder);
                bone.LclScaling = FBXCoordinateUtils.UnremapScale(bone.LclScaling, parsedModel.SourceToTarget, parsedModel.Signs);
                Vector3 preDegUn = bone.ToEuler(bone.PreRotation);
                preDegUn = FBXCoordinateUtils.UnremapRotation(preDegUn, parsedModel.SourceToTarget, parsedModel.Signs);
                bone.PreRotation = bone.ToQuaternion(preDegUn, 0);
                Vector3 postDegUn = bone.ToEuler(bone.PostRotation);
                postDegUn = FBXCoordinateUtils.UnremapRotation(postDegUn, parsedModel.SourceToTarget, parsedModel.Signs);
                bone.PostRotation = bone.ToQuaternion(postDegUn, 0);
                bone.RotationPivot = FBXCoordinateUtils.UnremapVector(bone.RotationPivot, parsedModel.SourceToTarget, parsedModel.Signs);
                bone.RotationOffset = FBXCoordinateUtils.UnremapVector(bone.RotationOffset, parsedModel.SourceToTarget, parsedModel.Signs);
                bone.ScalingPivot = FBXCoordinateUtils.UnremapVector(bone.ScalingPivot, parsedModel.SourceToTarget, parsedModel.Signs);
                bone.ScalingOffset = FBXCoordinateUtils.UnremapVector(bone.ScalingOffset, parsedModel.SourceToTarget, parsedModel.Signs);
                bone.GeometricTranslation = FBXCoordinateUtils.UnremapVector(bone.GeometricTranslation, parsedModel.SourceToTarget, parsedModel.Signs);
                Vector3 geoRotDegUn = bone.ToEuler(bone.GeometricRotation);
                geoRotDegUn = FBXCoordinateUtils.UnremapRotation(geoRotDegUn, parsedModel.SourceToTarget, parsedModel.Signs);
                bone.GeometricRotation = bone.ToQuaternion(geoRotDegUn, 0);
                bone.GeometricScaling = FBXCoordinateUtils.UnremapScale(bone.GeometricScaling, parsedModel.SourceToTarget, parsedModel.Signs);
                bone.RotationOrder = FBXCoordinateUtils.UnremapRotationOrder(bone.RotationOrder, parsedModel.SourceToTarget);
                bone.Size *= _model.ModelScale / parsedModel.ModelScale;
                // Remap to mesh's engine
                bone.LclTranslation = FBXCoordinateUtils.RemapVector(bone.LclTranslation, _model.SourceToTarget, _model.Signs);
                Vector3 lclRotDegRe = bone.ToEuler(bone.LclRotation);
                lclRotDegRe = FBXCoordinateUtils.RemapRotation(lclRotDegRe, _model.SourceToTarget, _model.Signs);
                bone.LclRotation = bone.ToQuaternion(lclRotDegRe, bone.RotationOrder);
                bone.LclScaling = FBXCoordinateUtils.RemapScale(bone.LclScaling, _model.SourceToTarget, _model.Signs);
                Vector3 preDegRe = bone.ToEuler(bone.PreRotation);
                preDegRe = FBXCoordinateUtils.RemapRotation(preDegRe, _model.SourceToTarget, _model.Signs);
                bone.PreRotation = bone.ToQuaternion(preDegRe, 0);
                Vector3 postDegRe = bone.ToEuler(bone.PostRotation);
                postDegRe = FBXCoordinateUtils.RemapRotation(postDegRe, _model.SourceToTarget, _model.Signs);
                bone.PostRotation = bone.ToQuaternion(postDegRe, 0);
                bone.RotationPivot = FBXCoordinateUtils.RemapVector(bone.RotationPivot, _model.SourceToTarget, _model.Signs);
                bone.RotationOffset = FBXCoordinateUtils.RemapVector(bone.RotationOffset, _model.SourceToTarget, _model.Signs);
                bone.ScalingPivot = FBXCoordinateUtils.RemapVector(bone.ScalingPivot, _model.SourceToTarget, _model.Signs);
                bone.ScalingOffset = FBXCoordinateUtils.RemapVector(bone.ScalingOffset, _model.SourceToTarget, _model.Signs);
                bone.GeometricTranslation = FBXCoordinateUtils.RemapVector(bone.GeometricTranslation, _model.SourceToTarget, _model.Signs);
                Vector3 geoRotDegRe = bone.ToEuler(bone.GeometricRotation);
                geoRotDegRe = FBXCoordinateUtils.RemapRotation(geoRotDegRe, _model.SourceToTarget, _model.Signs);
                bone.GeometricRotation = bone.ToQuaternion(geoRotDegRe, 0);
                bone.GeometricScaling = FBXCoordinateUtils.RemapScale(bone.GeometricScaling, _model.SourceToTarget, _model.Signs);
                bone.RotationOrder = FBXCoordinateUtils.RemapRotationOrder(bone.RotationOrder, _model.SourceToTarget);
                // Recompute LocalRest
                bone.LocalRest = bone.ComputeLocal();
            }
            _model.Skeleton = tempSkeleton;
            if (oldSkeleton != null && _model.Meshes.Count > 0)
            {
                // Remap bone IDs to new skeleton order based on names
                var nameToNewIndex = new Dictionary<string, int>();
                for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
                {
                    nameToNewIndex[_model.Skeleton.Bones[i].Name.ToLowerInvariant()] = i;
                }
                var nameToOldIndex = new Dictionary<string, int>();
                for (int i = 0; i < oldSkeleton.Bones.Count; i++)
                {
                    nameToOldIndex[oldSkeleton.Bones[i].Name.ToLowerInvariant()] = i;
                }
                var oldToNewMap = new Dictionary<int, int>();
                foreach (var kv in nameToOldIndex)
                {
                    if (nameToNewIndex.TryGetValue(kv.Key, out int newI))
                    {
                        oldToNewMap[kv.Value] = newI;
                    }
                }
                var newToOldMap = new Dictionary<int, int>();
                foreach (var kv in oldToNewMap)
                {
                    newToOldMap[kv.Value] = kv.Key;
                }
                foreach (var mesh in _model.Meshes)
                {
                    for (int vi = 0; vi < mesh.Vertices.Count; vi++)
                    {
                        var v = mesh.Vertices[vi];
                        v.BoneID0 = oldToNewMap.GetValueOrDefault(v.BoneID0, -1);
                        v.BoneID1 = oldToNewMap.GetValueOrDefault(v.BoneID1, -1);
                        v.BoneID2 = oldToNewMap.GetValueOrDefault(v.BoneID2, -1);
                        v.BoneID3 = oldToNewMap.GetValueOrDefault(v.BoneID3, -1);
                        mesh.Vertices[vi] = v;
                    }
                }
                // Log old and new bone positions
                var oldRestLocals = oldSkeleton.Bones.Select(b => b.LocalRest).ToArray();
                var oldGlobals = oldSkeleton.ComputeGlobalTransforms(oldRestLocals);
                var newRestLocals = _model.Skeleton.Bones.Select(b => b.LocalRest).ToArray();
                var newGlobals = _model.Skeleton.ComputeGlobalTransforms(newRestLocals);
                Console.WriteLine("Old and New Bone Positions:");
                for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
                {
                    if (newToOldMap.TryGetValue(i, out int i_old))
                    {
                        Vector3 oldPos = oldGlobals[i_old].Translation;
                        Vector3 newPos = newGlobals[i].Translation;
                        string name = _model.Skeleton.Bones[i].Name;
                        Console.WriteLine($"Bone {name} (index {i}): Old Pos = ({oldPos.X:F2}, {oldPos.Y:F2}, {oldPos.Z:F2}), New Pos = ({newPos.X:F2}, {newPos.Y:F2}, {newPos.Z:F2})");
                    }
                    else
                    {
                        Console.WriteLine($"Bone index {i} not mapped");
                    }
                }
            }
            _model.ComputeBindPoses();
            if (_model.Meshes.Count > 0 && _model.HasUnweightedVertices())
            {
                _model.FixUnweightedVertices();
            }
            UpdateModelData();
            CenterCamera();
            SetRestPose();
        }
        private void LoadAnimation(string animPath)
        {
            var animForest = FBXParser.Load(animPath);
            var objectsNode = animForest.TreeList.FirstOrDefault(n => n.Name == "Objects");
            var objectsById = FBXParser.GatherObjectsById(objectsNode);
            var conns = FBXParser.GatherConnections(animForest);
            var animModel = FBXParser.BuildModelFromForest(animForest);
            var validAnimations = animModel.Animations.Where(a => a.Keyframes.Count > 0).ToList();
            if (validAnimations.Count > 0)
            {
                var anim = validAnimations[0];
                anim.Name = Path.GetFileNameWithoutExtension(animPath);
                if (_model.Skeleton == null || _model.Skeleton.Bones.Count == 0)
                {
                    return;
                }
                // Build hierarchy trees for matching
                var mainTree = BuildBoneTree(_model.Skeleton);
                var animTree = BuildBoneTree(animModel.Skeleton);
                // Adjust for potential extra root in main (e.g., "Armature")
                int mainRoot = mainTree.Keys.FirstOrDefault(k => !mainTree.Values.Any(children => children.Contains(k)));
                int animRoot = animTree.Keys.FirstOrDefault(k => !animTree.Values.Any(children => children.Contains(k)));
                if (animRoot == -1)
                {
                    return;
                }
                // Match hierarchies
                var boneMap = MatchBoneHierarchies(mainTree, animTree, mainRoot, animRoot, animModel);
                if (boneMap.Count < _model.Skeleton.Bones.Count * 0.8f)
                {
                    return;
                }
                // Log bone mapping
                Console.WriteLine("Bone map:");
                foreach (var kv in boneMap)
                {
                    string animName = animModel.Skeleton.Bones[kv.Key].Name;
                    string mainName = _model.Skeleton.Bones[kv.Value].Name;
                    Console.WriteLine($"Anim bone {kv.Key} ({animName}) maps to main bone {kv.Value} ({mainName})");
                }
                // Compute transformation to align coordinate systems if different
                Matrix4x4 trans = Matrix4x4.Identity;
                Matrix4x4 invTrans = Matrix4x4.Identity;
                bool axisMismatch = !_model.SourceToTarget.SequenceEqual(animModel.SourceToTarget) ||
                                    !_model.Signs.SequenceEqual(animModel.Signs);
                if (axisMismatch)
                {
                    trans = _model.P4 * animModel.InvP4;
                    invTrans = animModel.P4 * _model.InvP4;
                }
                float scaleFactor = _model.ModelScale / animModel.ModelScale;
                bool scaleMismatch = Math.Abs(scaleFactor - 1f) > 1e-6f;
                // Remap keyframes using hierarchy-matched indices
                HashSet<int> mappedBones = new HashSet<int>();
                foreach (var kf in anim.Keyframes)
                {
                    var anim_locals = kf.BoneTransforms.ToArray();
                    // Adjust for axis if mismatch
                    if (axisMismatch)
                    {
                        for (int j = 0; j < anim_locals.Length; j++)
                        {
                            anim_locals[j] = trans * anim_locals[j] * invTrans;
                        }
                    }
                    // Scale translation if scaleMismatch
                    if (scaleMismatch)
                    {
                        for (int j = 0; j < anim_locals.Length; j++)
                        {
                            if (Matrix4x4.Decompose(anim_locals[j], out Vector3 s, out Quaternion r, out Vector3 t))
                            {
                                t *= scaleFactor;
                                anim_locals[j] = Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
                            }
                        }
                    }
                    // Now, compute anim_globals
                    var anim_globals = animModel.Skeleton.ComputeGlobalTransforms(anim_locals);
                    // Compute adjusted globals = anim_global * invA * T for each bone
                    var anim_rest_globals = animModel.Skeleton.ComputeGlobalTransforms(animModel.Skeleton.Bones.Select(b => b.LocalRest).ToArray());
                    var main_rest_globals = _model.Skeleton.ComputeGlobalTransforms(_model.Skeleton.Bones.Select(b => b.LocalRest).ToArray());
                    var adjusted_globals = new Matrix4x4[_model.Skeleton.Bones.Count];
                    for (int animI = 0; animI < animModel.Skeleton.Bones.Count; animI++)
                    {
                        if (boneMap.TryGetValue(animI, out int mainI))
                        {
                            Matrix4x4 invA;
                            Matrix4x4.Invert(anim_rest_globals[animI], out invA);
                            adjusted_globals[mainI] = anim_globals[animI] * invA * main_rest_globals[mainI];
                            mappedBones.Add(mainI);
                        }
                    }
                    // For unmapped bones, set to main rest global
                    for (int mainI = 0; mainI < _model.Skeleton.Bones.Count; mainI++)
                    {
                        if (adjusted_globals[mainI] == default)
                        {
                            adjusted_globals[mainI] = main_rest_globals[mainI];
                        }
                    }
                    // Compute new_locals from adjusted_globals
                    var new_locals = _model.Skeleton.ComputeLocalsFromGlobals(adjusted_globals);
                    kf.BoneTransforms = new_locals.ToList();
                }
                if (mappedBones.Count < (int)(_model.Skeleton.Bones.Count * 0.8f))
                {
                    return;
                }
                _model.Animations.Add(anim);
                _currentAnimation = anim.Name;
                _duration = anim.Keyframes.Count > 0 ? anim.Keyframes.Last().Time : 0f;
                _time = 0f;
                _playing = false;
                SetTransformsFromTime(0f);
                // Copy weights if main model has unweighted vertices
                if (_model.HasUnweightedVertices())
                {
                    // Remap bone IDs in weights using boneMap
                    var idMap = new Dictionary<int, int>(); // anim bone idx to main bone idx
                    for (int animI = 0; animI < animModel.Skeleton.Bones.Count; animI++)
                    {
                        if (boneMap.TryGetValue(animI, out int mainI))
                        {
                            idMap[animI] = mainI;
                        }
                    }
                    if (idMap.Count < animModel.Skeleton.Bones.Count * 0.8f)
                    {
                    }
                    else
                    {
                        for (int mi = 0; mi < _model.Meshes.Count && mi < animModel.Meshes.Count; mi++)
                        {
                            var mainMesh = _model.Meshes[mi];
                            var animMesh = animModel.Meshes[mi];
                            if (mainMesh.Vertices.Count != animMesh.Vertices.Count)
                            {
                                continue;
                            }
                            for (int vi = 0; vi < mainMesh.Vertices.Count; vi++)
                            {
                                var animV = animMesh.Vertices[vi];
                                var mainV = mainMesh.Vertices[vi];
                                mainV.BoneID0 = idMap.GetValueOrDefault(animV.BoneID0, -1);
                                mainV.BoneID1 = idMap.GetValueOrDefault(animV.BoneID1, -1);
                                mainV.BoneID2 = idMap.GetValueOrDefault(animV.BoneID2, -1);
                                mainV.BoneID3 = idMap.GetValueOrDefault(animV.BoneID3, -1);
                                mainV.Weight0 = animV.Weight0;
                                mainV.Weight1 = animV.Weight1;
                                mainV.Weight2 = animV.Weight2;
                                mainV.Weight3 = animV.Weight3;
                                float sum = mainV.Weight0 + mainV.Weight1 + mainV.Weight2 + mainV.Weight3;
                                if (sum > 0)
                                {
                                    mainV.Weight0 /= sum;
                                    mainV.Weight1 /= sum;
                                    mainV.Weight2 /= sum;
                                    mainV.Weight3 /= sum;
                                }
                                mainMesh.Vertices[vi] = mainV;
                            }
                        }
                        // Update VBOs with new vertex data (weights updated)
                        UpdateModelBuffers();
                        _model.HasSkin = true;
                    }
                }
                // Switch to animation shader if skin present
                if (_model.HasSkin)
                {
                    _assetShader = new ShaderProgram(_renderContext, AnimationShader.VertexShaderSource, AnimationShader.FragmentShaderSource);
                }
                _skeletonBuffer.UpdateCustom(new List<Vertex>(), new List<uint>());
            }
        }
        private void UpdateTransformsFromFrame(int frame)
        {
            if (_model.Skeleton == null || _model.Animations.Count == 0) return;
            var animation = _model.Animations.Find(a => a.Name == _currentAnimation);
            if (animation == null) return;
            frame = Math.Clamp(frame, 0, animation.Keyframes.Count - 1);
            _currentFrameIndex = frame;
            var boneTransforms = animation.Keyframes[frame].BoneTransforms.ToArray();
            var globalTransforms = _model.Skeleton.ComputeGlobalTransforms(boneTransforms);
            _currentGlobalTransforms = globalTransforms;
            var finalTransforms = _model.Skeleton.ComputeFinalTransforms(globalTransforms);
            var normalTransforms = new Matrix3x3[finalTransforms.Length];
            for (int i = 0; i < finalTransforms.Length; i++)
            {
                Matrix4x4 mat = finalTransforms[i];
                if (!Matrix4x4.Invert(mat, out Matrix4x4 invMat))
                {
                    normalTransforms[i] = new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);
                    continue;
                }
                normalTransforms[i] = Matrix3x3.Transpose(
                    invMat.M11, invMat.M12, invMat.M13,
                    invMat.M21, invMat.M22, invMat.M23,
                    invMat.M31, invMat.M32, invMat.M33
                );
            }
            _currentNormalTransforms = normalTransforms;
            _model.Skeleton.UpdateTransforms(finalTransforms);
            UpdateSkeletonVisualization();
        }
        private void UpdateModelData()
        {
            if (_model == null) return;
            var modelManager = new ModelManager(renderContext: _renderContext);
            string key = "temp";
            modelManager.AddModel(key, _model);
            _modelData = modelManager.SetupModelData(_model, Path.GetDirectoryName(_meshPath), new FBXFileForest());
            if (_model.HasSkin)
            {
                _assetShader = new ShaderProgram(_renderContext, AnimationShader.VertexShaderSource, AnimationShader.FragmentShaderSource);
            }
            else
            {
                _assetShader = new ShaderProgram(_renderContext, AssetShader.VertexShaderSource, AssetShader.FragmentShaderSource);
            }
            _model.ComputeBindPoses();
            _skeletonBuffer.UpdateCustom(new List<Vertex>(), new List<uint>());
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
                    minBounds = Vector3.Min(minBounds, new Vector3(v.X, v.Y, v.Z));
                    maxBounds = Vector3.Max(maxBounds, new Vector3(v.X, v.Y, v.Z));
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
        private void DiscoverAnimationFiles()
        {
            string fbmDir = Path.Combine(Path.GetDirectoryName(_meshPath), Path.GetFileNameWithoutExtension(_meshPath) + ".fbm");
            if (Directory.Exists(fbmDir))
            {
                _animationFiles = Directory.GetFiles(fbmDir, "*.fbx").ToList();
            }
        }
        private void UpdateModelBuffers()
        {
            if (_model == null || _modelData == null || _modelData.MeshRenders.Count != _model.Meshes.Count)
            {
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
                uint stride = 20 * (uint)sizeof(float);
                _renderContext.EnableVertexAttribArray(0); // Position
                _renderContext.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false, stride, (void*)0);
                _renderContext.EnableVertexAttribArray(3); // Normal
                _renderContext.VertexAttribPointer(3, 3, _renderContext.Enums.Float, false, stride, (void*)(3 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(2); // UV
                _renderContext.VertexAttribPointer(2, 2, _renderContext.Enums.Float, false, stride, (void*)(6 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(4); // MaterialIndex
                _renderContext.VertexAttribPointer(4, 1, _renderContext.Enums.Float, false, stride, (void*)(8 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(5); // Tangent
                _renderContext.VertexAttribPointer(5, 3, _renderContext.Enums.Float, false, stride, (void*)(9 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(6); // BoneIDs (as float)
                _renderContext.VertexAttribPointer(6, 4, _renderContext.Enums.Float, false, stride, (void*)(12 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(7); // BoneWeights
                _renderContext.VertexAttribPointer(7, 4, _renderContext.Enums.Float, false, stride, (void*)(16 * sizeof(float)));
                _renderContext.BindVertexArray(0);
            }
            _model.HasSkin = true;
        }
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
                        var normalTransforms = new Matrix3x3[finalTransforms.Length];
                        for (int i = 0; i < finalTransforms.Length; i++)
                        {
                            Matrix4x4 mat = finalTransforms[i];
                            if (!Matrix4x4.Invert(mat, out Matrix4x4 invMat))
                            {
                                normalTransforms[i] = new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);
                                continue;
                            }
                            normalTransforms[i] = Matrix3x3.Transpose(
                                invMat.M11, invMat.M12, invMat.M13,
                                invMat.M21, invMat.M22, invMat.M23,
                                invMat.M31, invMat.M32, invMat.M33
                            );
                        }
                        _currentNormalTransforms = normalTransforms;
                        _model.Skeleton.UpdateTransforms(finalTransforms);
                    }
                }
            }
            if (_controlContext.GetKey(_window, Key.Space) == InputAction.Press)
            {
                _playing = !_playing;
            }
            UpdateSkeletonVisualization();
        }
        //private void SetRestPose()
        //{
        //    if (_model == null || _model.Skeleton == null) return;
        //    var restLocals = _model.Skeleton.Bones.Select(b => b.LocalRest).ToArray();
        //    var globalTransforms = _model.Skeleton.ComputeGlobalTransforms(restLocals);
        //    _currentGlobalTransforms = globalTransforms;
        //    var finalTransforms = _model.Skeleton.ComputeFinalTransforms(globalTransforms);
        //    var normalTransforms = new Matrix3x3[finalTransforms.Length];
        //    for (int i = 0; i < finalTransforms.Length; i++)
        //    {
        //        var mat = finalTransforms[i];
        //        if (!Matrix4x4.Invert(mat, out var invMat))
        //        {
        //            normalTransforms[i] = new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);
        //            continue;
        //        }
        //        normalTransforms[i] = new Matrix3x3(
        //            invMat.M11, invMat.M12, invMat.M13,
        //            invMat.M21, invMat.M22, invMat.M23,
        //            invMat.M31, invMat.M32, invMat.M33
        //        ).Transpose();
        //    }
        //    _currentNormalTransforms = normalTransforms;
        //    _model.Skeleton.UpdateTransforms(finalTransforms);
        //    UpdateSkeletonVisualization();
        //}
        private void SetTransformsFromTime(float time)
        {
            if (_model.Skeleton == null || _model.Animations.Count == 0) return;
            var animation = _model.Animations.Find(a => a.Name == _currentAnimation);
            if (animation == null) return;
            var localTransforms = animation.GetBoneTransforms(time);
            var globalTransforms = _model.Skeleton.ComputeGlobalTransforms(localTransforms);
            _currentGlobalTransforms = globalTransforms;
            var finalTransforms = _model.Skeleton.ComputeFinalTransforms(globalTransforms);
            var normalTransforms = new Matrix3x3[finalTransforms.Length];
            for (int i = 0; i < finalTransforms.Length; i++)
            {
                var mat = finalTransforms[i];
                if (!Matrix4x4.Invert(mat, out var invMat))
                {
                    normalTransforms[i] = new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);
                    continue;
                }
                normalTransforms[i] = Matrix3x3.Transpose(
                    invMat.M11, invMat.M12, invMat.M13,
                    invMat.M21, invMat.M22, invMat.M23,
                    invMat.M31, invMat.M32, invMat.M33
                );
            }
            _currentNormalTransforms = normalTransforms;
            _model.Skeleton.UpdateTransforms(finalTransforms);
            UpdateSkeletonVisualization();
        }
        private void UpdateSkeletonVisualization()
        {
            if (_model?.Skeleton == null || _currentGlobalTransforms == null || _currentGlobalTransforms.Length != _model.Skeleton.Bones.Count) return;
            var vertices = new List<Vertex>();
            var indices = new List<uint>();
            uint idx = 0;
            var positions = new Vector3[_model.Skeleton.Bones.Count];
            for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
            {
                positions[i] = _currentGlobalTransforms[i].Translation;
            }
            for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
            {
                int parentIdx = _model.Skeleton.Bones[i].ParentIndex;
                if (parentIdx >= 0)
                {
                    Vector3 parentPos = positions[parentIdx];
                    Vector3 childPos = positions[i];
                    vertices.Add(new Vertex(parentPos.X, parentPos.Y, parentPos.Z, 0, 1, 0, 1));
                    indices.Add(idx++);
                    vertices.Add(new Vertex(childPos.X, childPos.Y, childPos.Z, 0, 1, 0, 1));
                    indices.Add(idx++);
                }
            }
            _skeletonBuffer.UpdateCustom(vertices, indices);
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
            _assetShader.SetUniform("uHasBones", _model.HasSkin ? 1 : 0);
            if (_model.HasSkin && _model.Skeleton != null && _currentNormalTransforms != null)
            {
                var transforms = _model.Skeleton.GetTransforms();
                _assetShader.SetMatrix4Array("uBoneTransforms", transforms);
                _assetShader.SetMatrix3Array("uNormalBoneTransforms", _currentNormalTransforms);
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
            }
            // Render skeleton
            _pointShader.Use();
            _pointShader.SetMatrix4("uView", view);
            _pointShader.SetMatrix4("uProjection", projection);
            _pointShader.SetUniform("uPointSize", 5f);
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
            // Render time info
            string timeInfo = "Time: " + _time.ToString("F2") + " / " + _duration.ToString("F2");
            _textRenderer.RenderText(timeInfo, 10, TitleHeight + 10, (int)Size.X, (int)Size.Y, 12f);
            // Render bone info text
            float currentY = TitleHeight + 25;
            if (_model?.Skeleton != null && _currentGlobalTransforms != null && _currentGlobalTransforms.Length == _model.Skeleton.Bones.Count)
            {
                for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
                {
                    if (currentY > Size.Y - 20) break; // Prevent overflow
                    Matrix4x4.Decompose(_currentGlobalTransforms[i], out _, out Quaternion rot, out Vector3 pos);
                    Vector3 euler = _model.Skeleton.Bones[i].ToEuler(rot);
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
        private Dictionary<int, List<int>> BuildBoneTree(Skeleton skeleton)
        {
            var tree = new Dictionary<int, List<int>>();
            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                tree[i] = new List<int>();
            }
            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                int parent = skeleton.Bones[i].ParentIndex;
                if (parent != -1)
                {
                    tree[parent].Add(i);
                }
            }
            return tree;
        }
        private Dictionary<int, int> MatchBoneHierarchies(Dictionary<int, List<int>> mainTree, Dictionary<int, List<int>> animTree, int mainRoot, int animRoot, FBXModel animModel)
        {
            var boneMap = new Dictionary<int, int>();
            string mainRootName = _model.Skeleton.Bones[mainRoot].Name;
            string animRootName = animModel.Skeleton.Bones[animRoot].Name;
            if (mainRootName != animRootName)
            {
                var mainRootChildren = mainTree[mainRoot];
                if (mainRootChildren.Count == 1)
                {
                    int mainEffectiveRoot = mainRootChildren[0];
                    if (MatchStructures(mainTree, animTree, mainEffectiveRoot, animRoot, animModel))
                    {
                        mainRoot = mainEffectiveRoot;
                    }
                    else
                    {
                        return boneMap;
                    }
                }
                else
                {
                    return boneMap;
                }
            }
            // Recursive match from roots
            MatchBoneSubtree(mainRoot, animRoot, mainTree, animTree, _model.Skeleton, animModel.Skeleton, boneMap);
            return boneMap;
        }
        private bool MatchStructures(Dictionary<int, List<int>> mainTree, Dictionary<int, List<int>> animTree, int mainIdx, int animIdx, FBXModel animModel)
        {
            var mainChildren = mainTree[mainIdx];
            var animChildren = animTree[animIdx];
            if (mainChildren.Count != animChildren.Count) return false;
            var sortedMainChildren = mainChildren.OrderBy(c => _model.Skeleton.Bones[c].Name).ToList();
            var sortedAnimChildren = animChildren.OrderBy(c => animModel.Skeleton.Bones[c].Name).ToList();
            for (int i = 0; i < sortedMainChildren.Count; i++)
            {
                if (!MatchStructures(mainTree, animTree, sortedMainChildren[i], sortedAnimChildren[i], animModel)) return false;
            }
            return true;
        }
        private void MatchBoneSubtree(int mainIdx, int animIdx, Dictionary<int, List<int>> mainTree, Dictionary<int, List<int>> animTree, Skeleton mainSkeleton, Skeleton animSkeleton, Dictionary<int, int> boneMap)
        {
            string mainName = mainSkeleton.Bones[mainIdx].Name;
            string animName = animSkeleton.Bones[animIdx].Name;
            boneMap[animIdx] = mainIdx;
            var mainChildren = mainTree[mainIdx];
            var animChildren = animTree[animIdx];
            if (mainChildren.Count == animChildren.Count)
            {
                var sortedMainChildren = mainChildren.OrderBy(c => mainSkeleton.Bones[c].Name).ToList();
                var sortedAnimChildren = animChildren.OrderBy(c => animSkeleton.Bones[c].Name).ToList();
                for (int i = 0; i < sortedMainChildren.Count; i++)
                {
                    MatchBoneSubtree(sortedMainChildren[i], sortedAnimChildren[i], mainTree, animTree, mainSkeleton, animSkeleton, boneMap);
                }
            }
        }
        private void SetRestPose()
        {
            if (_model == null || _model.Skeleton == null) return;
            var restLocals = _model.Skeleton.Bones.Select(b => b.LocalRest).ToArray();
            var globalTransforms = _model.Skeleton.ComputeGlobalTransforms(restLocals);
            _currentGlobalTransforms = globalTransforms;
            var finalTransforms = _model.Skeleton.ComputeFinalTransforms(globalTransforms);
            var normalTransforms = new Matrix3x3[finalTransforms.Length];
            for (int i = 0; i < finalTransforms.Length; i++)
            {
                var mat = finalTransforms[i];
                if (!Matrix4x4.Invert(mat, out var invMat))
                {
                    normalTransforms[i] = new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);
                    continue;
                }
                normalTransforms[i] = Matrix3x3.Transpose(
                    invMat.M11, invMat.M12, invMat.M13,
                    invMat.M21, invMat.M22, invMat.M23,
                    invMat.M31, invMat.M32, invMat.M33
                );
            }
            _currentNormalTransforms = normalTransforms;
            _model.Skeleton.UpdateTransforms(finalTransforms);
            UpdateSkeletonVisualization();
        }
    }
}