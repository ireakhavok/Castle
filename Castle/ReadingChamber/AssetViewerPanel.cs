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
using SiegeEngine.Core.AssetParsing.Model;
using System.Linq;
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
        private Matrix3x3[] _currentNormalTransforms;
        private Vector3 _cameraPosition = new Vector3(0, 500, 0);
        private Vector3 _cameraTarget = Vector3.Zero;
        private Vector3 _cameraUp = Vector3.UnitZ;
        private Quaternion _cameraRotation = Quaternion.Identity;
        private float _lastMouseX, _lastMouseY;
        private bool _firstMouse = true;
        private bool _isPanning = false;
        private string _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters", "man_mesh.fbx");
        private List<string> _animationFiles = new List<string>();
        private float _cameraDistance;
        private float _maxExtent;
        public AssetViewerPanel(IRenderContext renderContext, IControlContext controlContext, IntPtr window, EventBus eventBus) : base(renderContext, controlContext, window, eventBus)
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
                _maxExtent = Math.Max(maxBounds.X - minBounds.X, Math.Max(maxBounds.Y - minBounds.Y, maxBounds.Z - minBounds.Z)) / 2;
                _cameraDistance = Math.Max(_maxExtent * 3.5f, 0.1f);
                _cameraTarget = center;
                Vector3 initialFront = new Vector3(0, 1, 0);
                _cameraPosition = _cameraTarget + initialFront * _cameraDistance;
                _cameraUp = Vector3.UnitZ;
                Console.WriteLine($"AssetViewerPanel: Model center: {center}, maxExtent: {_maxExtent}, cameraDistance: {_cameraDistance}, cameraPosition: {_cameraPosition}");
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
                _currentNormalTransforms = null;
                _skeletonBuffer.UpdateCustom(new List<Vertex>(), new List<uint>());
                _assetShader = new ShaderProgram(_renderContext, AnimationShader.VertexShaderSource, AnimationShader.FragmentShaderSource);
                // Compute bind poses after loading the model
                _model.ComputeBindPoses();
                // Log bone info for debugging
                LogBoneHierarchy(_model.Skeleton, "Main Model");
                LogWeightsSummary();
            }
            else
            {
                Console.WriteLine("AssetViewerPanel: Failed to load selected model");
            }
        }
        private void LogBoneHierarchy(Skeleton skeleton, string label)
        {
            if (skeleton == null) return;
            Console.WriteLine($"{label} Bone Hierarchy (sorted by name for comparison):");
            var sortedBones = skeleton.Bones.OrderBy(b => b.Name.ToLowerInvariant()).ToList();
            for (int i = 0; i < sortedBones.Count; i++)
            {
                var bone = sortedBones[i];
                int originalIdx = skeleton.Bones.IndexOf(bone);
                Console.WriteLine($"Sorted {i} (orig {originalIdx}): {bone.Name}, Parent: {bone.ParentIndex}, Type: {bone.BoneType}, Size: {bone.Size}");
            }
        }
        private void LogWeightsSummary()
        {
            if (_model.Meshes.Count == 0) return;
            var mesh = _model.Meshes[0];
            int unweighted = mesh.Vertices.Count(v => v.Weight0 + v.Weight1 + v.Weight2 + v.Weight3 == 0);
            Console.WriteLine($"Weights Summary: Total verts {mesh.Vertices.Count}, Unweighted {unweighted}");
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
            var objectsNode = animForest.TreeList.FirstOrDefault(n => n.Name == "Objects");
            var objectsById = FBXParser.GatherObjectsById(objectsNode);
            var conns = FBXParser.GatherConnections(animForest);
            var animModel = FBXParser.BuildModelFromForest(animForest);
            LogBoneHierarchy(animModel.Skeleton, "Animation File");
            var validAnimations = animModel.Animations.Where(a => a.Keyframes.Count > 0).ToList();
            if (validAnimations.Count > 0)
            {
                var anim = validAnimations[0];
                anim.Name = Path.GetFileNameWithoutExtension(animPath);
                if (_model.Skeleton == null || _model.Skeleton.Bones.Count == 0)
                {
                    Console.WriteLine("Main model has no skeleton, skipping animation load to avoid mismatches");
                    return;
                }
                // Build hierarchy trees for matching
                var mainTree = BuildBoneTree(_model.Skeleton);
                var animTree = BuildBoneTree(animModel.Skeleton);
                // Adjust for potential extra root in main (e.g., "Armature")
                int mainRoot = mainTree.Keys.FirstOrDefault(k => !mainTree.Values.Any(children => children.Contains(k)));
                if (_model.Skeleton.Bones[mainRoot].Name.ToLowerInvariant() == "armature")
                {
                    // Assume next is effective root
                    if (mainTree[mainRoot].Count == 1)
                    {
                        mainRoot = mainTree[mainRoot][0];
                        Console.WriteLine("Detected extra 'Armature' root in main model, using child as effective root for matching");
                    }
                }
                int animRoot = animTree.Keys.FirstOrDefault(k => !animTree.Values.Any(children => children.Contains(k)));
                if (animRoot == -1)
                {
                    Console.WriteLine("No root found in anim hierarchy, skipping");
                    return;
                }
                // Match hierarchies
                var boneMap = MatchBoneHierarchies(mainTree, animTree, mainRoot, animRoot, animModel);
                if (boneMap.Count < _model.Skeleton.Bones.Count * 0.8f)
                {
                    Console.WriteLine($"Insufficient hierarchy matching ({boneMap.Count}/{_model.Skeleton.Bones.Count}), skipping animation");
                    return;
                }
                // Remap keyframes using hierarchy-matched indices
                HashSet<int> mappedBones = new HashSet<int>();
                foreach (var kf in anim.Keyframes)
                {
                    List<Matrix4x4> newTransforms = new List<Matrix4x4>(_model.Skeleton.Bones.Count);
                    for (int j = 0; j < _model.Skeleton.Bones.Count; j++)
                    {
                        newTransforms.Add(_model.Skeleton.Bones[j].LocalRest);
                    }
                    for (int i = 0; i < animModel.Skeleton.Bones.Count; i++)
                    {
                        if (boneMap.TryGetValue(i, out int targetIdx))
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
                                Console.WriteLine($"Failed to invert anim rest for bone index {i} ({animModel.Skeleton.Bones[i].Name}), using anim local directly");
                                newTransforms[targetIdx] = local;
                            }
                            mappedBones.Add(targetIdx);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Bone index {i} ({animModel.Skeleton.Bones[i].Name}) from animation not matched in main hierarchy");
                        }
                    }
                    kf.BoneTransforms = newTransforms;
                }
                Console.WriteLine($"Mapped {mappedBones.Count} unique bones for animation {anim.Name} via hierarchy matching");
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
                        Console.WriteLine("Insufficient bone ID mapping for weight copy, skipping");
                    }
                    else
                    {
                        for (int mi = 0; mi < _model.Meshes.Count && mi < animModel.Meshes.Count; mi++)
                        {
                            var mainMesh = _model.Meshes[mi];
                            var animMesh = animModel.Meshes[mi];
                            if (mainMesh.Vertices.Count != animMesh.Vertices.Count)
                            {
                                Console.WriteLine($"Vertex count mismatch for mesh {mi}, skipping weight copy");
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
                                mainMesh.Vertices[vi] = mainV;
                            }
                        }
                        Console.WriteLine($"Copied and remapped weights from animation model to main model for {anim.Name}");
                        // Update VBOs with new vertex data (weights updated)
                        UpdateModelBuffers();
                        _model.HasSkin = true;
                    }
                }
                else
                {
                    Console.WriteLine("No weight copy needed: Main model already weighted");
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
            // Check if roots match, if not try fallback
            string mainRootName = NormalizeBoneName(_model.Skeleton.Bones[mainRoot].Name);
            string animRootName = NormalizeBoneName(animModel.Skeleton.Bones[animRoot].Name);
            if (mainRootName != animRootName)
            {
                Console.WriteLine($"Root names don't match: Main {mainRootName} vs Anim {animRootName}. Attempting fallback matching.");
                // Fallback: Match main root's children to anim root's children if structures align
                var mainRootChildren = mainTree[mainRoot];
                if (mainRootChildren.Count == 1)
                {
                    int mainEffectiveRoot = mainRootChildren[0];
                    if (MatchStructures(mainTree, animTree, mainEffectiveRoot, animRoot, animModel))
                    {
                        mainRoot = mainEffectiveRoot;
                        Console.WriteLine("Fallback successful: Using main's effective root for matching.");
                    }
                    else
                    {
                        Console.WriteLine("Fallback failed: Structures don't align.");
                        return boneMap;
                    }
                }
                else
                {
                    Console.WriteLine("Fallback not possible: Main root has multiple children.");
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
            var sortedMainChildren = mainChildren.OrderBy(c => NormalizeBoneName(_model.Skeleton.Bones[c].Name)).ToList();
            var sortedAnimChildren = animChildren.OrderBy(c => NormalizeBoneName(animModel.Skeleton.Bones[c].Name)).ToList();
            for (int i = 0; i < sortedMainChildren.Count; i++)
            {
                if (!MatchStructures(mainTree, animTree, sortedMainChildren[i], sortedAnimChildren[i], animModel)) return false;
            }
            return true;
        }
        private void MatchBoneSubtree(int mainIdx, int animIdx, Dictionary<int, List<int>> mainTree, Dictionary<int, List<int>> animTree, Skeleton mainSkeleton, Skeleton animSkeleton, Dictionary<int, int> boneMap)
        {
            string mainName = NormalizeBoneName(mainSkeleton.Bones[mainIdx].Name);
            string animName = NormalizeBoneName(animSkeleton.Bones[animIdx].Name);
            Console.WriteLine($"Matching bone: Main {mainSkeleton.Bones[mainIdx].Name} (norm: {mainName}) vs Anim {animSkeleton.Bones[animIdx].Name} (norm: {animName})");
            boneMap[animIdx] = mainIdx;
            Console.WriteLine("Mapped by structure.");
            var mainChildren = mainTree[mainIdx];
            var animChildren = animTree[animIdx];
            Console.WriteLine($"Child count: Main {mainChildren.Count} vs Anim {animChildren.Count}");
            if (mainChildren.Count == animChildren.Count)
            {
                // Sort children by normalized name for order-independent matching
                var sortedMainChildren = mainChildren.OrderBy(c => NormalizeBoneName(mainSkeleton.Bones[c].Name)).ToList();
                var sortedAnimChildren = animChildren.OrderBy(c => NormalizeBoneName(animSkeleton.Bones[c].Name)).ToList();
                for (int i = 0; i < sortedMainChildren.Count; i++)
                {
                    MatchBoneSubtree(sortedMainChildren[i], sortedAnimChildren[i], mainTree, animTree, mainSkeleton, animSkeleton, boneMap);
                }
            }
            else
            {
                Console.WriteLine("Child count mismatch, skipping subtree matching.");
            }
        }
        private string NormalizeBoneName(string name)
        {
            return name.ToLowerInvariant().Replace("_", "");
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
                // Re-set vertex attribute pointers to ensure correct setup
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
                Console.WriteLine($"Updated VBO for mesh {mi} with new weights and reset attribute pointers");
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
            int insertIndex = baseHtml.IndexOf(""); //"<!-- Animation buttons will be added here dynamically -->");
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
                            normalTransforms[i] = new Matrix3x3(
                                mat.M11, mat.M12, mat.M13,
                                mat.M21, mat.M22, mat.M23,
                                mat.M31, mat.M32, mat.M33
                            ).Transpose().Inverse();
                        }
                        _currentNormalTransforms = normalTransforms;
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
                int parentIdx = _model.Skeleton.Bones[i].ParentIndex;
                Matrix4x4 parentGlobal = parentIdx >= 0 ? _currentGlobalTransforms[parentIdx] : Matrix4x4.Identity;
                Vector3 pivotPos = _model.Skeleton.Bones[i].GetRotationPivotGlobal(parentGlobal);
                vertices.Add(new Vertex(pivotPos.X, pivotPos.Y, pivotPos.Z, 0, 1, 0, 1)); // green for pivot
                indices.Add(idx++);
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
            _pointShader.SetUniform("uPointSize", 5f);
            _skeletonBuffer.Bind();
            _renderContext.DrawElements(_renderContext.Enums.Points, _skeletonBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
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
                Console.WriteLine($"New frame.");
                for (int i = 0; i < _model.Skeleton.Bones.Count; i++)
                {
                    if (currentY > Size.Y - 20) break; // Prevent overflow
                    Matrix4x4.Decompose(_currentGlobalTransforms[i], out _, out Quaternion rot, out Vector3 pos);
                    Vector3 euler = ToEuler(rot);
                    string info = $"{_model.Skeleton.Bones[i].Name}: Pos({pos.X:F2},{pos.Y:F2},{pos.Z:F2}) Rot({euler.X:F2},{euler.Y:F2},{euler.Z:F2})";
                    _textRenderer.RenderText(info, 10, currentY, (int)Size.X, (int)Size.Y, 12f);
                    currentY += 15;
                    Console.WriteLine(info);
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