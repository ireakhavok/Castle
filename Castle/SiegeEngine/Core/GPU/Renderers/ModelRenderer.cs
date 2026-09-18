// Folder: SiegeEngine/Core/Rendering
// File: ModelRenderer.cs
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Lighting;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.GPU.Shaders.OpenGL;

namespace SiegeEngine.Core.GPU.Renderers
{
    public unsafe class ModelRenderer
    {
        private readonly IRenderContext _renderContext;
        private ShaderProgram _modelShader;
        private ShaderProgram _animationShader;
        private GpuHandle _modelPipeline;
        private GpuHandle _animationPipeline;
        private List<int> _hiddenMeshIndices;
        private List<MeshMaterialOption> _materialOptions;
        private FBXModel _opacityModel;
        private string _opacityModelKey;
        private ShaderProgram _viewLightingShader;
        private int _viewLightingSerial;
        private Matrix4x4 _viewLightingView;
        private Matrix4x4 _viewLightingProjection;
        private Vector3 _viewLightingPos;
        private static readonly Dictionary<string, uint> _opacityTextures = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        public const int OpacityTextureUnit = 15;
        public static string ProjectTexturesDirectory { get; set; }
        public static string LastImportedOpacityAbsolute { get; set; }

        public string OpacityModelKey
        {
            get => _opacityModelKey;
            set => _opacityModelKey = value;
        }

        // No-op. Opacity maps are imported into project/Textures; we load that handle only.
        public static void RegisterTextureSearchRoot(string root) { }

        /// <summary>
        /// Remember the just-imported file so the first BindOpacityOption this frame
        /// can resolve ../../Textures/filename before fbxDir is bound.
        /// </summary>
        public static void PreloadOpacity(string stored, string modelKey = null)
        {
            if (string.IsNullOrWhiteSpace(stored)) return;
            string resolved = ResolveStoredOpacityPath(stored, modelKey);
            if (string.IsNullOrEmpty(resolved))
            {
                string fileName = System.IO.Path.GetFileName(stored.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (!string.IsNullOrEmpty(ProjectTexturesDirectory) && !string.IsNullOrEmpty(fileName))
                {
                    string candidate = System.IO.Path.Combine(ProjectTexturesDirectory, fileName);
                    if (System.IO.File.Exists(candidate))
                        resolved = System.IO.Path.GetFullPath(candidate);
                }
            }
            if (!string.IsNullOrEmpty(resolved) && System.IO.File.Exists(resolved))
                LastImportedOpacityAbsolute = resolved;
        }

        public ModelRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext;
        }

        public void Initialize()
        {
            _modelShader = new ShaderProgram(_renderContext, ModelShader.VertexShaderSource, ModelShader.FragmentShaderSource);
            _animationShader = new ShaderProgram(_renderContext, AnimationShader.VertexShaderSource, AnimationShader.FragmentShaderSource);
            _modelPipeline = _renderContext.CreatePipeline(ShaderCatalog.Describe(ShaderId.Model, _renderContext));
            _animationPipeline = _renderContext.CreatePipeline(ShaderCatalog.Describe(ShaderId.Animation, _renderContext));
        }

        // === SINGLE CANONICAL PATH — all scenes and panels now call this ===
        public void RenderEntityFully(ModelComponent modelComp, PhysicsComponent physics, Matrix4x4 view, Matrix4x4 projection, Vector3 viewPos)
        {
            if (modelComp == null || physics == null) return;

            var modelManager = ModelManager.Instance;
            if (modelManager == null) return;
            string modelKey = modelComp.Key?.ToLower() ?? "man_mesh_pack";
            FBXModel fbxModel = null;
            ModelManager.ModelData modelData = null;

            if (modelManager.TryGetModel(modelKey, out fbxModel) && modelManager.TryGetModelData(modelKey, out modelData))
            {
                modelComp.Model = fbxModel;

                // Build model matrix so the visual mesh rotates around the physics CoM
                // (not the FBX origin). This is the fix for the "spinning around a point
                // outside the model" bug.
                float unitScale = fbxModel != null ? fbxModel.UnitToMeters : 0.01f;
                Matrix4x4 modelMatrix = physics.BuildRenderModelMatrix(unitScale);

                Matrix4x4[] boneMatrices = modelComp.BoneMatrices;
                Matrix3x3[] normalMatrices = modelComp.NormalBoneTransforms;
                if (!IsVisibleInView(view * projection, modelMatrix, fbxModel, physics))
                    return;
                bool receiveShadows = modelComp.ReceiveShadows && (modelComp.Material == null || modelComp.Material.ReceiveShadows);
                _hiddenMeshIndices = modelComp.HiddenMeshIndices;
                _materialOptions = modelComp.MaterialOptions;
                _opacityModelKey = modelKey;
                try
                {
                    RenderModel(fbxModel, modelData, view, projection, viewPos, modelMatrix, boneMatrices, normalMatrices, receiveShadows);
                }
                finally
                {
                    _hiddenMeshIndices = null;
                    _materialOptions = null;
                    _opacityModelKey = null;
                }
            }
            else
            {
                // fallback for legacy entities (preserves everything)
                _hiddenMeshIndices = modelComp.HiddenMeshIndices;
                _materialOptions = modelComp.MaterialOptions;
                try
                {
                    RenderModel(modelComp, physics, view, projection, viewPos, modelManager);
                }
                finally
                {
                    _hiddenMeshIndices = null;
                    _materialOptions = null;
                }
            }
        }

        public void RenderModel(ModelComponent modelComp, PhysicsComponent physics, Matrix4x4 view, Matrix4x4 projection, Vector3 viewPos, ModelManager modelManager)
        {
            if (modelComp == null || physics == null) return;
            modelManager ??= ModelManager.Instance;
            string modelKey = modelComp.Key?.ToLower() ?? "man_mesh";
            if (!modelManager.TryGetModelData(modelKey, out var modelData)) return;

            FBXModel fbxModel = modelComp.Model;
            if (fbxModel == null && modelManager.TryGetModel(modelKey, out fbxModel))
            {
                modelComp.Model = fbxModel;
            }

            float unitScale = fbxModel != null ? fbxModel.UnitToMeters : 0.01f;
            Matrix4x4 modelMatrix = physics.BuildRenderModelMatrix(unitScale);

            _renderContext.Enable(_renderContext.Enums.CullFace);
            _renderContext.CullFace(_renderContext.Enums.Back);
            _renderContext.FrontFace(_renderContext.Enums.CounterClockwise);

            if (fbxModel != null && modelData != null)
            {
                Matrix4x4[] boneMatrices = modelComp.BoneMatrices;
                Matrix3x3[] normalMatrices = modelComp.NormalBoneTransforms;
                bool receiveShadows = modelComp.ReceiveShadows && (modelComp.Material == null || modelComp.Material.ReceiveShadows);
                RenderModel(fbxModel, modelData, view, projection, viewPos, modelMatrix, boneMatrices, normalMatrices, receiveShadows);
            }

            _renderContext.Disable(_renderContext.Enums.CullFace);
        }

        public void RenderModel(FBXModel fbxModel, ModelManager.ModelData modelData, Matrix4x4 view, Matrix4x4 projection, Vector3 viewPos, Matrix4x4 modelMatrix = default, Matrix4x4[] boneMatrices = null, Matrix3x3[] normalMatrices = null)
        {
            RenderModel(fbxModel, modelData, view, projection, viewPos, modelMatrix, boneMatrices, normalMatrices, receiveShadows: true);
        }

        public void RenderModel(FBXModel fbxModel, ModelManager.ModelData modelData, Matrix4x4 view, Matrix4x4 projection, Vector3 viewPos, Matrix4x4 modelMatrix, Matrix4x4[] boneMatrices, Matrix3x3[] normalMatrices, bool receiveShadows, ICollection<int> hiddenMeshIndices = null, IList<MeshMaterialOption> materialOptions = null, bool applyLod = true)
        {
            if (modelData == null) return;
            if (modelMatrix == default) modelMatrix = Matrix4x4.Identity;
            List<int> prevHidden = _hiddenMeshIndices;
            List<MeshMaterialOption> prevOpts = _materialOptions;
            FBXModel prevOpacityModel = _opacityModel;
            _opacityModel = fbxModel;
            if (hiddenMeshIndices != null)
                _hiddenMeshIndices = hiddenMeshIndices as List<int> ?? new List<int>(hiddenMeshIndices);
            if (materialOptions != null)
                _materialOptions = materialOptions as List<MeshMaterialOption> ?? new List<MeshMaterialOption>(materialOptions);

            bool hasBones = boneMatrices != null && boneMatrices.Length > 0 && fbxModel != null && fbxModel.HasSkin;
            ShaderProgram shader = hasBones ? _animationShader : _modelShader;
            FrameCB frame = new FrameCB { View = view, Projection = projection, ViewPos = new Vector4(viewPos, 1f) };
            ObjectCB obj = new ObjectCB { Model = modelMatrix, NormalMatrix = BuildNormalMatrix(modelMatrix), HasBones = hasBones ? 1 : 0, ReceiveShadows = receiveShadows ? 1 : 0 };
            _renderContext.SetConstants(ConstantSlot.Frame, frame);
            _renderContext.SetConstants(ConstantSlot.Object, obj);
            if (hasBones)
                UploadSkin(boneMatrices);
            LightingFrame.Current?.ApplyConstants(_renderContext);
            shader.Use();
            BindViewLighting(shader, view, projection, viewPos);

            // Own complete GL state so result is independent of prior TerrainRenderer / skybox / UI state.
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.DepthMask(true);
            _renderContext.Disable(_renderContext.Enums.Blend);
            _renderContext.Disable(_renderContext.Enums.CullFace);
            _renderContext.FrontFace(_renderContext.Enums.CounterClockwise);

            int renderIndex = 0;
            foreach (var mmr in modelData.MeshRenders)
            {
                int gpuIndex = renderIndex;
                renderIndex++;
                if (applyLod)
                {
                    float lodDist = Vector3.Distance(viewPos, modelMatrix.Translation);
                    float lodSize = EstimateRadius(fbxModel, modelData, Vector3.One) * 2f;
                    if (IsMeshSkipped(_hiddenMeshIndices, fbxModel?.Meshes, gpuIndex, lodDist, lodSize))
                        continue;
                }
                else if (_hiddenMeshIndices != null && _hiddenMeshIndices.Contains(gpuIndex))
                    continue;

                try
                {
                    for (int i = 0; i < Math.Min(mmr.AlbedoTextures.Length, 4); i++)
                    {
                        _renderContext.BindTextureSlot(i, _renderContext.ImportTexture(mmr.AlbedoTextures[i], _renderContext.Enums.Texture2D));
                        shader.SetUniform($"uAlbedoMap[{i}]", i);
                    }
                    for (int i = 0; i < Math.Min(mmr.NormalTextures.Length, 4); i++)
                    {
                        _renderContext.BindTextureSlot(4 + i, _renderContext.ImportTexture(mmr.NormalTextures[i], _renderContext.Enums.Texture2D));
                        shader.SetUniform($"uNormalMap[{i}]", 4 + i);
                    }
                    for (int i = 0; i < Math.Min(mmr.MetallicTextures.Length, 4); i++)
                    {
                        _renderContext.BindTextureSlot(8 + i, _renderContext.ImportTexture(mmr.MetallicTextures[i], _renderContext.Enums.Texture2D));
                        shader.SetUniform($"uMetallicMap[{i}]", 8 + i);
                    }
                }
                catch
                {
                    if (mmr.AlbedoTextures.Length > 0)
                    {
                        _renderContext.BindTextureSlot(0, _renderContext.ImportTexture(mmr.AlbedoTextures[0], _renderContext.Enums.Texture2D));
                        shader.SetUniform("uAlbedoMap[0]", 0);
                    }
                }

                BindOpacityOption(shader, gpuIndex);

                Gl.Of(_renderContext).BindVertexArray(mmr.Vao);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
            }

            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _hiddenMeshIndices = prevHidden;
            _materialOptions = prevOpts;
            _opacityModel = prevOpacityModel;
        }

        public void RenderSkeletonDebug(VertexBuffer skeletonBuffer, ShaderProgram pointShader, Matrix4x4 view, Matrix4x4 projection)
        {
            pointShader.Use();
            _renderContext.SetConstants(ConstantSlot.Frame, new FrameCB { View = view, Projection = projection });
            _renderContext.SetConstants(ConstantSlot.Object, new ObjectCB { Model = Matrix4x4.Identity, NormalMatrix = Matrix4x4.Identity });
            skeletonBuffer.Bind();
            _renderContext.DrawElements(_renderContext.Enums.Lines, skeletonBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
        }

        public void RenderTerrain(VertexBuffer buffer, ShaderProgram shader, Matrix4x4 view, Matrix4x4 projection, bool hasTexture, uint textureId)
        {
            if (buffer == null) return;
            shader.Use();
            buffer.Bind();
            _renderContext.BindCamera(view, projection, Matrix4x4.Identity);
            LightingFrame.Current?.ApplyConstants(_renderContext);
            LightingFrame.Current?.ApplyTo(shader, _renderContext);
            if (!_renderContext.TryGetConstants(ConstantSlot.Frame, out FrameCB frame))
                frame = default;
            if (hasTexture && textureId != 0)
            {
                frame.HasTexture = 1;
                _renderContext.SetConstants(ConstantSlot.Frame, frame);
                _renderContext.BindTextureSlot(Shaders.TextureSlot.Color, _renderContext.ImportTexture(textureId, _renderContext.Enums.Texture2D));
            }
            else
            {
                frame.HasTexture = 0;
                _renderContext.SetConstants(ConstantSlot.Frame, frame);
            }
            uint idxCount = buffer.GetIndexCount();
            _renderContext.DrawElements(_renderContext.Enums.Triangles, idxCount, _renderContext.Enums.UnsignedInt, null);
        }

        public void RenderModelForEntity(ModelComponent modelComp, PhysicsComponent physics, Matrix4x4 view, Matrix4x4 projection)
        {
            RenderEntityFully(modelComp, physics, view, projection, physics.Position);
        }

        /// <summary>
        /// Inverse-transpose of the linear part of the model matrix. Drops the
        /// mid-chain CoM translations so normals follow entity rotation/scale only.
        /// </summary>
        private static Matrix4x4 BuildNormalMatrix(Matrix4x4 model)
        {
            Matrix4x4 linear = model;
            linear.M14 = 0f;
            linear.M24 = 0f;
            linear.M34 = 0f;
            linear.M41 = 0f;
            linear.M42 = 0f;
            linear.M43 = 0f;
            linear.M44 = 1f;
            if (!Matrix4x4.Invert(linear, out Matrix4x4 inv))
                return linear;
            return Matrix4x4.Transpose(inv);
        }

        private void BindViewLighting(ShaderProgram shader, Matrix4x4 view, Matrix4x4 projection, Vector3 viewPos)
        {
            if (shader == _viewLightingShader
                && _viewLightingSerial == LightingFrame.UploadSerial
                && _viewLightingView == view
                && _viewLightingProjection == projection
                && _viewLightingPos == viewPos)
                return;

            LightingFrame.Current?.ApplyConstants(_renderContext);
            BindShadowMaps(shader);

            _viewLightingShader = shader;
            _viewLightingSerial = LightingFrame.UploadSerial;
            _viewLightingView = view;
            _viewLightingProjection = projection;
            _viewLightingPos = viewPos;
        }

        private void BindShadowMaps(ShaderProgram shader)
        {
            // 7c7a351 had no per-draw shadow binds. LastReady kept those binds
            // alive after the user turned shadows off.
            if (LightingSettings.ResolveShadowQuality() == ShadowQuality.Off)
                return;
            LightingFrame frame = LightingFrame.Current;
            if (frame == null || frame.ShadowAtlas == 0 || !frame.ShadowsReady)
                frame = LightingFrame.LastReady;
            uint atlas = ShadowMapRenderer.WrittenSunAtlas != 0
                ? ShadowMapRenderer.WrittenSunAtlas
                : (frame != null ? frame.ShadowAtlas : 0);
            if (atlas == 0 && (frame == null || frame.PointShadowCube == 0 && frame.SpotShadowMap == 0))
                return;
            LightingFrame.BindShadowTextures(_renderContext, atlas,
                frame != null ? frame.PointShadowCube : 0,
                frame != null ? frame.SpotShadowMap : 0);
        }

        private void BindOpacityOption(ShaderProgram shader, int meshIndex)
        {
            BindOpacityToShader(_renderContext, shader, meshIndex, _materialOptions, _opacityModelKey, OpacityTextureUnit);
        }

        public static bool CollectOpacitySlots(int meshIndex, IList<MeshMaterialOption> options, out string path, out int slots)
        {
            path = null;
            slots = 0;
            if (options == null) return false;
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                if (o == null || o.MeshIndex != meshIndex) continue;
                if (string.IsNullOrWhiteSpace(o.OpacityPath)) continue;
                int mat = o.MaterialIndex;
                if (mat < 0 || mat > 3) continue;
                if (path == null)
                    path = o.OpacityPath.Trim();
                if (string.Equals(path, o.OpacityPath.Trim(), StringComparison.OrdinalIgnoreCase))
                    slots |= (1 << mat);
            }
            return !string.IsNullOrEmpty(path) && slots != 0;
        }

        public static bool MaterialHasOpacity(IList<MeshMaterialOption> options, int meshIndex, int materialIndex)
        {
            if (options == null) return false;
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                if (o == null || o.MeshIndex != meshIndex) continue;
                if (o.MaterialIndex != materialIndex) continue;
                if (!string.IsNullOrWhiteSpace(o.OpacityPath))
                    return true;
            }
            return false;
        }

        public static void BindOpacityToShader(IRenderContext rc, ShaderProgram shader, int meshIndex, IList<MeshMaterialOption> options, string modelKey, int unit = OpacityTextureUnit)
        {
            shader.SetUniform("uHasOpacity", 0);
            shader.SetUniform("uOpacitySlots", 0);
            if (rc == null || shader == null) return;
            if (!CollectOpacitySlots(meshIndex, options, out string path, out int slots))
                return;
            uint tex = LoadOpacityTexture(rc, path, modelKey);
            if (tex == 0)
                return;
            rc.BindTextureSlot(unit, rc.ImportTexture(tex, rc.Enums.Texture2D));
            shader.SetUniform("uOpacityMap", unit);
            shader.SetUniform("uOpacitySlots", slots);
            shader.SetUniform("uHasOpacity", 1);
        }

        private uint GetOrLoadOpacityTexture(string stored, string modelKey = null)
        {
            return LoadOpacityTexture(_renderContext, stored, modelKey);
        }

        public static uint LoadOpacityTexture(IRenderContext rc, string stored, string modelKey = null)
        {
            if (rc == null) return 0;
            string resolved = ResolveStoredOpacityPath(stored, modelKey);
            if (string.IsNullOrEmpty(resolved) || !System.IO.File.Exists(resolved))
                return 0;
            if (_opacityTextures.TryGetValue(resolved, out uint existing) && existing != 0)
                return existing;
            try
            {
                var loaded = TextureLoader.LoadTexture(rc, resolved);
                if (loaded.Item1 == 0)
                    return 0;
                _opacityTextures[resolved] = loaded.Item1;
                return loaded.Item1;
            }
            catch
            {
                return 0;
            }
        }

        public static string ResolveTexturePath(string stored)
        {
            return ResolveStoredOpacityPath(stored, null);
        }

        private static string ResolveStoredOpacityPath(string stored, string modelKey)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            stored = stored.Trim().Replace('/', System.IO.Path.DirectorySeparatorChar);
            if (System.IO.File.Exists(stored))
                return System.IO.Path.GetFullPath(stored);

            string fileName = System.IO.Path.GetFileName(stored);

            // Pack-relative handle: Combine(fbxDir, ../../Textures/file) — same as albedo.
            if (!string.IsNullOrEmpty(modelKey) && ModelManager.Instance != null
                && ModelManager.Instance.TryGetFbxDirectory(modelKey, out string fbxDir)
                && !string.IsNullOrEmpty(fbxDir))
            {
                try
                {
                    string combined = System.IO.Path.GetFullPath(System.IO.Path.Combine(fbxDir, stored));
                    if (System.IO.File.Exists(combined))
                        return combined;
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        string sibling = System.IO.Path.GetFullPath(System.IO.Path.Combine(fbxDir, "..", "..", "Textures", fileName));
                        if (System.IO.File.Exists(sibling))
                            return sibling;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(ProjectTexturesDirectory) && !string.IsNullOrEmpty(fileName))
            {
                string inProject = System.IO.Path.Combine(ProjectTexturesDirectory, fileName);
                if (System.IO.File.Exists(inProject))
                    return System.IO.Path.GetFullPath(inProject);
            }

            try
            {
                string full = System.IO.Path.GetFullPath(stored);
                if (System.IO.File.Exists(full))
                    return full;
            }
            catch { }

            if (!string.IsNullOrEmpty(LastImportedOpacityAbsolute)
                && System.IO.File.Exists(LastImportedOpacityAbsolute)
                && !string.IsNullOrEmpty(fileName)
                && string.Equals(System.IO.Path.GetFileName(LastImportedOpacityAbsolute), fileName, StringComparison.OrdinalIgnoreCase))
                return LastImportedOpacityAbsolute;

            return null;
        }

        public static string ToProjectRelative(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return "";
            try
            {
                if (!string.IsNullOrEmpty(ProjectTexturesDirectory))
                {
                    string fileName = System.IO.Path.GetFileName(fullPath);
                    return "../../Textures/" + fileName;
                }
                string root = AppDomain.CurrentDomain.BaseDirectory;
                string rel = System.IO.Path.GetRelativePath(root, fullPath);
                if (!string.IsNullOrEmpty(rel) && !rel.StartsWith(".."))
                    return rel.Replace('\\', '/');
            }
            catch
            {
            }
            return fullPath.Replace('\\', '/');
        }

        public static bool IsMeshSkipped(List<int> authoredHidden, IList<MeshData> meshes, int meshIndex, float distance, float size)
        {
            if (authoredHidden != null)
            {
                for (int i = 0; i < authoredHidden.Count; i++)
                {
                    if (authoredHidden[i] == meshIndex)
                        return true;
                }
            }
            return MeshData.ShouldSkipLod(meshes, meshIndex, distance, size);
        }

        public const float FrustumBoundsPad = 0.05f;

        public static bool IsVisibleInView(Matrix4x4 viewProjection, Matrix4x4 modelMatrix, FBXModel model, PhysicsComponent physics)
        {
            if (!TryGetLocalVertexBounds(model, physics, out Vector3 localMin, out Vector3 localMax))
                return true;
            WorldAabbFromLocal(modelMatrix, localMin, localMax, FrustumBoundsPad, out Vector3 worldMin, out Vector3 worldMax);
            return AabbIntersectsView(viewProjection, worldMin, worldMax);
        }

        public static bool IsVisibleInView(Matrix4x4 viewProjection, Matrix4x4 modelMatrix, Vector3 localMin, Vector3 localMax)
        {
            WorldAabbFromLocal(modelMatrix, localMin, localMax, FrustumBoundsPad, out Vector3 worldMin, out Vector3 worldMax);
            return AabbIntersectsView(viewProjection, worldMin, worldMax);
        }

        public static bool TryGetLocalVertexBounds(FBXModel model, PhysicsComponent physics, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            bool any = false;
            if (model?.Meshes != null)
            {
                for (int i = 0; i < model.Meshes.Count; i++)
                {
                    MeshData mesh = model.Meshes[i];
                    if (mesh == null) continue;
                    if (mesh.Bounds.LengthSquared() <= 1e-12f)
                        continue;
                    // BoundsMin is written at parse. Default zero with a non-zero size
                    // means an older in-memory pack — do not assume the mesh starts at origin.
                    if (mesh.BoundsMin == Vector3.Zero)
                        continue;
                    Vector3 meshMin = mesh.BoundsMin;
                    Vector3 meshMax = mesh.BoundsMin + mesh.Bounds;
                    min = Vector3.Min(min, meshMin);
                    max = Vector3.Max(max, meshMax);
                    any = true;
                }
            }
            if (any)
                return true;

            float unit = model != null && model.UnitToMeters > 1e-8f ? model.UnitToMeters : 0.01f;
            if (physics != null && physics.LocalBoundsMinCm.X <= physics.LocalBoundsMaxCm.X
                && !float.IsInfinity(physics.LocalBoundsMinCm.X) && !float.IsInfinity(physics.LocalBoundsMaxCm.X))
            {
                min = physics.LocalBoundsMinCm / unit;
                max = physics.LocalBoundsMaxCm / unit;
                return min.X <= max.X;
            }
            if (model != null && model.LocalBoundsMinCm.X <= model.LocalBoundsMaxCm.X
                && !float.IsInfinity(model.LocalBoundsMinCm.X) && !float.IsInfinity(model.LocalBoundsMaxCm.X))
            {
                min = model.LocalBoundsMinCm / unit;
                max = model.LocalBoundsMaxCm / unit;
                return min.X <= max.X;
            }
            return false;
        }

        public static void WorldAabbFromLocal(Matrix4x4 modelMatrix, Vector3 localMin, Vector3 localMax, float pad, out Vector3 worldMin, out Vector3 worldMax)
        {
            worldMin = new Vector3(float.MaxValue);
            worldMax = new Vector3(float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 local = new Vector3(
                    (i & 1) == 0 ? localMin.X : localMax.X,
                    (i & 2) == 0 ? localMin.Y : localMax.Y,
                    (i & 4) == 0 ? localMin.Z : localMax.Z);
                Vector3 world = Vector3.Transform(local, modelMatrix);
                worldMin = Vector3.Min(worldMin, world);
                worldMax = Vector3.Max(worldMax, world);
            }
            Vector3 center = (worldMin + worldMax) * 0.5f;
            Vector3 extent = (worldMax - worldMin) * 0.5f;
            float inflate = 1f + (pad > 0f ? pad : 0f);
            extent *= inflate;
            worldMin = center - extent;
            worldMax = center + extent;
        }

        public static bool AabbIntersectsView(Matrix4x4 viewProjection, Vector3 worldMin, Vector3 worldMax)
        {
            Vector3 center = (worldMin + worldMax) * 0.5f;
            Vector3 extent = (worldMax - worldMin) * 0.5f;
            Vector4 c1 = new Vector4(viewProjection.M11, viewProjection.M21, viewProjection.M31, viewProjection.M41);
            Vector4 c2 = new Vector4(viewProjection.M12, viewProjection.M22, viewProjection.M32, viewProjection.M42);
            Vector4 c3 = new Vector4(viewProjection.M13, viewProjection.M23, viewProjection.M33, viewProjection.M43);
            Vector4 c4 = new Vector4(viewProjection.M14, viewProjection.M24, viewProjection.M34, viewProjection.M44);
            if (!AabbVsPlane(center, extent, c4 + c1)) return false;
            if (!AabbVsPlane(center, extent, c4 - c1)) return false;
            if (!AabbVsPlane(center, extent, c4 + c2)) return false;
            if (!AabbVsPlane(center, extent, c4 - c2)) return false;
            if (!AabbVsPlane(center, extent, c4 + c3)) return false;
            if (!AabbVsPlane(center, extent, c4 - c3)) return false;
            return true;
        }

        private static bool AabbVsPlane(Vector3 center, Vector3 extent, Vector4 plane)
        {
            float dist = plane.X * center.X + plane.Y * center.Y + plane.Z * center.Z + plane.W;
            float r = MathF.Abs(plane.X) * extent.X + MathF.Abs(plane.Y) * extent.Y + MathF.Abs(plane.Z) * extent.Z;
            return dist + r >= 0f;
        }

        public static float EstimateRadius(ModelManager.ModelData modelData, float scale)
        {
            return EstimateRadius(null, modelData, new Vector3(scale, scale, scale));
        }

        public static float EstimateRadius(FBXModel model, ModelManager.ModelData modelData, Vector3 scale)
        {
            float s = MathF.Max(MathF.Abs(scale.X), MathF.Max(MathF.Abs(scale.Y), MathF.Abs(scale.Z)));
            if (s < 1e-4f) s = 1f;
            if (TryGetLocalVertexBounds(model, null, out Vector3 min, out Vector3 max))
            {
                float unit = model != null ? model.UnitToMeters : 0.01f;
                Vector3 sizeM = (max - min) * unit;
                return 0.5f * sizeM.Length() * s;
            }
            return 0.5f * s;
        }


        unsafe void UploadSkin(Matrix4x4[] bones)
        {
            SkinCB skin = default;
            int n = bones == null ? 0 : System.Math.Min(bones.Length, 128);
            for (int i = 0; i < n; i++)
            {
                Matrix4x4 m = bones[i];
                int o = i * 16;
                skin.BoneTransforms[o + 0] = m.M11;
                skin.BoneTransforms[o + 1] = m.M12;
                skin.BoneTransforms[o + 2] = m.M13;
                skin.BoneTransforms[o + 3] = m.M14;
                skin.BoneTransforms[o + 4] = m.M21;
                skin.BoneTransforms[o + 5] = m.M22;
                skin.BoneTransforms[o + 6] = m.M23;
                skin.BoneTransforms[o + 7] = m.M24;
                skin.BoneTransforms[o + 8] = m.M31;
                skin.BoneTransforms[o + 9] = m.M32;
                skin.BoneTransforms[o + 10] = m.M33;
                skin.BoneTransforms[o + 11] = m.M34;
                skin.BoneTransforms[o + 12] = m.M41;
                skin.BoneTransforms[o + 13] = m.M42;
                skin.BoneTransforms[o + 14] = m.M43;
                skin.BoneTransforms[o + 15] = m.M44;
            }
            _renderContext.SetConstants(ConstantSlot.Skin, skin);
        }

        public void Dispose()
        {
            if (_modelPipeline.IsValid) _renderContext.Destroy(_modelPipeline);
            if (_animationPipeline.IsValid) _renderContext.Destroy(_animationPipeline);
            _modelShader?.Dispose();
            _animationShader?.Dispose();
        }
    }
}
