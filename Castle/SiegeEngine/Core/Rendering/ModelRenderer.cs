// Folder: SiegeEngine.Core.Rendering
// File: ModelRenderer.cs
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Rendering.Shaders;
using System;
using System.Numerics;
namespace SiegeEngine.Core.Rendering
{
    public unsafe class ModelRenderer // marked unsafe to satisfy any potential fixed blocks in future or from copy-paste
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
        public void RenderModel(ModelComponent modelComp, PhysicsComponent physics, Matrix4x4 view, Matrix4x4 projection, Vector3 viewPos, ModelManager modelManager)
        {
            if (modelComp == null || physics == null) return;
            string modelKey = modelComp.Key?.ToLower() ?? "man_mesh";
            if (!modelManager.TryGetModelData(modelKey, out var modelData)) return;
            Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(physics.Rotation);
            Matrix4x4 translation = Matrix4x4.CreateTranslation(physics.Position);
            Matrix4x4 modelMatrix = rotation * translation;
            bool hasBones = modelComp.Model.Skeleton != null && modelComp.Model.Skeleton.Bones.Count > 0;
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
            if (hasBones)
            {
                var globals = modelComp.Model.Skeleton.ComputeGlobalTransforms();
                var boneMatrices = new Matrix4x4[globals.Length];
                var normalMatrices = new Matrix3x3[globals.Length];
                for (int i = 0; i < globals.Length; i++)
                {
                    boneMatrices[i] = modelComp.Model.Skeleton.Bones[i].BindPose * globals[i];
                    if (Matrix4x4.Invert(boneMatrices[i], out var inv))
                    {
                        var invT = Matrix4x4.Transpose(inv);
                        normalMatrices[i] = new Matrix3x3(invT.M11, invT.M12, invT.M13,
                                                         invT.M21, invT.M22, invT.M23,
                                                         invT.M31, invT.M32, invT.M33);
                    }
                    else
                    {
                        normalMatrices[i] = Matrix3x3.Identity;
                    }
                }
                shader.SetUniform("uHasBones", 1);
                shader.SetMatrix4Array("uBoneMatrices", boneMatrices);
                shader.SetMatrix3Array("uNormalMatrices", normalMatrices);
            }
            else
            {
                shader.SetUniform("uHasBones", 0);
            }
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
        }

        // New minimal overload for AnimationViewerPanel / ModelViewerScene (viewer context)
        // Uses identity transform by default, accepts pre-computed bone matrices from the viewer,
        // and re-uses the exact same shader setup + texture binding + draw logic.
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

            if (hasBones)
            {
                shader.SetUniform("uHasBones", 1);
                shader.SetMatrix4Array("uBoneMatrices", boneMatrices);
                if (normalMatrices != null)
                    shader.SetMatrix3Array("uNormalMatrices", normalMatrices);
            }
            else
            {
                shader.SetUniform("uHasBones", 0);
            }

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
        }

        public void Dispose()
        {
            _modelShader?.Dispose();
            _animationShader?.Dispose();
        }
    }
}