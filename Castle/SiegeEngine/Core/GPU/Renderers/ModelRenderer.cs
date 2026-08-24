// Folder: SiegeEngine/Core/Rendering
// File: ModelRenderer.cs
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Renderers
{
    public unsafe class ModelRenderer
    {
        private readonly IRenderContext _renderContext;
        private ShaderProgram _modelShader;
        private ShaderProgram _animationShader;

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
                RenderModel(fbxModel, modelData, view, projection, viewPos, modelMatrix, boneMatrices, normalMatrices);
            }
            else
            {
                // fallback for legacy entities (preserves everything)
                RenderModel(modelComp, physics, view, projection, viewPos, modelManager);
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
                RenderModel(fbxModel, modelData, view, projection, viewPos, modelMatrix, boneMatrices, normalMatrices);
            }

            _renderContext.Disable(_renderContext.Enums.CullFace);
        }

        public void RenderModel(FBXModel fbxModel, ModelManager.ModelData modelData, Matrix4x4 view, Matrix4x4 projection, Vector3 viewPos, Matrix4x4 modelMatrix = default, Matrix4x4[] boneMatrices = null, Matrix3x3[] normalMatrices = null)
        {
            if (modelData == null) return;
            if (modelMatrix == default) modelMatrix = Matrix4x4.Identity;

            bool hasBones = boneMatrices != null && boneMatrices.Length > 0 && fbxModel != null && fbxModel.HasSkin;
            ShaderProgram shader = hasBones ? _animationShader : _modelShader;
            shader.Use();
            shader.SetMatrix4("uModel", modelMatrix);
            shader.SetMatrix4("uView", view);
            shader.SetMatrix4("uProjection", projection);
            shader.SetUniform("uViewPos", viewPos.X, viewPos.Y, viewPos.Z);
            shader.SetUniform("uAmbientStrength", 0.3f);
            shader.SetUniform("uSpecularStrength", 0.05f);
            shader.SetUniform("uShininess", 4.0f);
            shader.SetUniform("uLightDir", -0.707f, -0.707f, 0.707f);
            shader.SetUniform("uLightColor", 1.0f, 1.0f, 1.0f);
            shader.SetUniform("uLightIntensity", 1.0f);
            shader.SetUniform("uHasWorldAligned", 0);

            if (hasBones)
            {
                shader.SetUniform("uHasBones", 1);
                shader.SetMatrix4Array("uBoneMatrices", boneMatrices);
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

            foreach (var mmr in modelData.MeshRenders)
            {
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

                _renderContext.BindVertexArray(mmr.Vao);
                _renderContext.DrawElements(_renderContext.Enums.Triangles, mmr.IndexCount, _renderContext.Enums.UnsignedInt, null);
                _renderContext.BindVertexArray(0);
            }

            _renderContext.Disable(_renderContext.Enums.DepthTest);
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

        public void Dispose()
        {
            _modelShader?.Dispose();
            _animationShader?.Dispose();
        }
    }
}