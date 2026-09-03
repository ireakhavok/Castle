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

namespace SiegeEngine.Core.GPU.Renderers
{
    public unsafe class ModelRenderer
    {
        private readonly IRenderContext _renderContext;
        private ShaderProgram _modelShader;
        private ShaderProgram _animationShader;
        private List<int> _hiddenMeshIndices;
        private List<MeshMaterialOption> _materialOptions;
        private FBXModel _opacityModel;
        private string _opacityModelKey;
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
        }

        // === SINGLE CANONICAL PATH — all scenes and panels now call this ===
        public void RenderEntityFully(ModelComponent modelComp, PhysicsComponent physics, Matrix4x4 view, Matrix4x4 projection, Vector3 viewPos)
        {
            if (modelComp == null || physics == null) return;

            var modelManager = ModelManager.Instance ?? new ModelManager(_renderContext);
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
                Matrix4x4 modelMatrix =
                    Matrix4x4.CreateScale(unitScale * physics.Scale) *
                    Matrix4x4.CreateTranslation(-physics.LocalCentreOfMass) *
                    Matrix4x4.CreateFromQuaternion(physics.Rotation) *
                    Matrix4x4.CreateTranslation(physics.WorldCentreOfMass);

                Matrix4x4[] boneMatrices = modelComp.BoneMatrices;
                Matrix3x3[] normalMatrices = modelComp.NormalBoneTransforms;
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
            Matrix4x4 modelMatrix =
                Matrix4x4.CreateScale(unitScale * physics.Scale) *
                Matrix4x4.CreateTranslation(-physics.LocalCentreOfMass) *
                Matrix4x4.CreateFromQuaternion(physics.Rotation) *
                Matrix4x4.CreateTranslation(physics.WorldCentreOfMass);

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

        public void RenderModel(FBXModel fbxModel, ModelManager.ModelData modelData, Matrix4x4 view, Matrix4x4 projection, Vector3 viewPos, Matrix4x4 modelMatrix, Matrix4x4[] boneMatrices, Matrix3x3[] normalMatrices, bool receiveShadows, ICollection<int> hiddenMeshIndices = null, IList<MeshMaterialOption> materialOptions = null)
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
            shader.Use();
            shader.SetMatrix4("uModel", modelMatrix);
            shader.SetMatrix4("uNormalMatrix", BuildNormalMatrix(modelMatrix));
            shader.SetMatrix4("uView", view);
            shader.SetMatrix4("uProjection", projection);
            shader.SetUniform("uViewPos", viewPos.X, viewPos.Y, viewPos.Z);
            shader.SetUniform("uAmbientStrength", 0.3f);
            shader.SetUniform("uSpecularStrength", 0.05f);
            shader.SetUniform("uShininess", 4.0f);
            shader.SetUniform("uLightDir", LightingFrame.DefaultSunDirection.X, LightingFrame.DefaultSunDirection.Y, LightingFrame.DefaultSunDirection.Z);
            shader.SetUniform("uLightColor", 1.0f, 1.0f, 1.0f);
            shader.SetUniform("uLightIntensity", 0.0f);
            shader.SetUniform("uHasWorldAligned", 0);
            LightingFrame.Current?.ApplyTo(shader, _renderContext);
            shader.SetUniform("uReceiveShadows", receiveShadows ? 1 : 0);

            if (hasBones)
            {
                shader.SetUniform("uHasBones", 1);
                shader.SetMatrix4Array("uBoneMatrices", boneMatrices);
                shader.SetMatrix4Array("uBoneTransforms", boneMatrices);
                if (normalMatrices != null) shader.SetMatrix3Array("uNormalMatrices", normalMatrices);
            }
            else
            {
                shader.SetUniform("uHasBones", 0);
            }

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
                if (_hiddenMeshIndices != null && _hiddenMeshIndices.Contains(gpuIndex))
                    continue;

                try
                {
                    for (int i = 0; i < Math.Min(mmr.AlbedoTextures.Length, 4); i++)
                    {
                        _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + i);
                        _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.AlbedoTextures[i]);
                        shader.SetUniform($"uAlbedoMap[{i}]", i);
                    }
                    for (int i = 0; i < Math.Min(mmr.NormalTextures.Length, 4); i++)
                    {
                        _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 4 + i);
                        _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.NormalTextures[i]);
                        shader.SetUniform($"uNormalMap[{i}]", 4 + i);
                    }
                    for (int i = 0; i < Math.Min(mmr.MetallicTextures.Length, 4); i++)
                    {
                        _renderContext.ActiveTexture(_renderContext.Enums.Texture0 + 8 + i);
                        _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.MetallicTextures[i]);
                        shader.SetUniform($"uMetallicMap[{i}]", 8 + i);
                    }
                }
                catch
                {
                    if (mmr.AlbedoTextures.Length > 0)
                    {
                        _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                        _renderContext.BindTexture(_renderContext.Enums.Texture2D, mmr.AlbedoTextures[0]);
                        shader.SetUniform("uAlbedoMap[0]", 0);
                    }
                }

                BindOpacityOption(shader, gpuIndex);
                BindShadowMaps(shader);

                _renderContext.BindVertexArray(mmr.Vao);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                _renderContext.BindVertexArray(0);
            }

            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _hiddenMeshIndices = prevHidden;
            _materialOptions = prevOpts;
            _opacityModel = prevOpacityModel;
        }

        public void RenderSkeletonDebug(VertexBuffer skeletonBuffer, ShaderProgram pointShader, Matrix4x4 view, Matrix4x4 projection)
        {
            pointShader.Use();
            pointShader.SetMatrix4("uModel", Matrix4x4.Identity);
            pointShader.SetMatrix4("uView", view);
            pointShader.SetMatrix4("uProjection", projection);
            _renderContext.BindVertexArray(skeletonBuffer.Vao);
            _renderContext.DrawElements(_renderContext.Enums.Lines, skeletonBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            _renderContext.BindVertexArray(0);
        }

        public void RenderTerrain(VertexBuffer buffer, ShaderProgram shader, Matrix4x4 view, Matrix4x4 projection, bool hasTexture, uint textureId)
        {
            if (buffer == null) return;
            buffer.Bind();
            uint stride = 9 * sizeof(float);
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 3, _renderContext.Enums.Float, false, stride, (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 4, _renderContext.Enums.Float, false, stride, (void*)(3 * sizeof(float)));
            _renderContext.EnableVertexAttribArray(2);
            _renderContext.VertexAttribPointer(2, 2, _renderContext.Enums.Float, false, stride, (void*)(7 * sizeof(float)));
            shader.Use();
            shader.SetMatrix4("uView", view);
            shader.SetMatrix4("uProjection", projection);
            shader.SetMatrix4("uModel", Matrix4x4.Identity);
            LightingFrame.Current?.ApplyTo(shader, _renderContext);
            if (hasTexture && textureId != 0)
            {
                _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, textureId);
                shader.SetUniform("uHasTexture", 1);
                shader.SetUniform("uTexture", 0);
            }
            else
            {
                shader.SetUniform("uHasTexture", 0);
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

        private void BindShadowMaps(ShaderProgram shader)
        {
            LightingFrame frame = LightingFrame.Current;
            if (frame == null || frame.ShadowAtlas == 0 || !frame.ShadowsReady)
                frame = LightingFrame.LastReady;
            IRenderContext rc = _renderContext;
            int u0 = rc.Enums.Texture0;
            uint atlas = ShadowMapRenderer.WrittenSunAtlas != 0
                ? ShadowMapRenderer.WrittenSunAtlas
                : (frame != null ? frame.ShadowAtlas : 0);
            rc.ActiveTexture(u0 + LightingFrame.ShadowAtlasUnit);
            rc.BindTexture(rc.Enums.Texture2D, atlas);
            shader.SetUniform("uShadowAtlas", LightingFrame.ShadowAtlasUnit);
            if (frame != null)
            {
                rc.ActiveTexture(u0 + LightingFrame.PointShadowUnit);
                rc.BindTexture(rc.Enums.TextureCubeMap, frame.PointShadowCube);
                shader.SetUniform("uPointShadowCube", LightingFrame.PointShadowUnit);
                rc.ActiveTexture(u0 + LightingFrame.SpotShadowUnit);
                rc.BindTexture(rc.Enums.Texture2D, frame.SpotShadowMap);
                shader.SetUniform("uSpotShadowMap", LightingFrame.SpotShadowUnit);
            }
            rc.ActiveTexture(u0);
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
            rc.ActiveTexture(rc.Enums.Texture0 + unit);
            rc.BindTexture(rc.Enums.Texture2D, tex);
            shader.SetUniform("uOpacityMap", unit);
            shader.SetUniform("uOpacitySlots", slots);
            shader.SetUniform("uHasOpacity", 1);
            rc.ActiveTexture(rc.Enums.Texture0);
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

        public void Dispose()
        {
            _modelShader?.Dispose();
            _animationShader?.Dispose();
        }
    }
}
