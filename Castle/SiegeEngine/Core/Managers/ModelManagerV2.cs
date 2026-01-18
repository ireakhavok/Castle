// Folder: SiegeEngine.Core
// File: Managers/ModelManagerV2.cs
using SiegeEngine.Core.Definitions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.AssetParsing.V2.Model;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.V2;

namespace SiegeEngine.Core.Managers
{
    public class ModelManagerV2
    {
        private readonly Dictionary<string, FBXModel> _models = new();
        private readonly Dictionary<string, ModelData> _modelData = new();
        private readonly IRenderContext _renderContext;
        public class ModelData
        {
            public List<ModelMeshRender> MeshRenders { get; set; } = new List<ModelMeshRender>();
        }
        public class ModelMeshRender
        {
            public uint Vao { get; set; }
            public uint Vbo { get; set; }
            public uint Ebo { get; set; }
            public uint[] AlbedoTextures { get; set; }
            public uint[] NormalTextures { get; set; }
            public uint[] MetallicTextures { get; set; }
            public uint IndexCount { get; set; }
        }
        public ModelManagerV2(IRenderContext renderContext = null)
        {
            _renderContext = renderContext;
        }
        public void LoadModel(string filePath, FBXFileForest forest = null)
        {
            string key = Path.GetFileNameWithoutExtension(filePath).ToLower();
            string fbxDir = Path.GetDirectoryName(filePath);
            try
            {
                if (forest == null)
                {
                    forest = FBXParser.Load(filePath);
                }
                FBXModel model = FBXParser.BuildModelFromForest(forest);
                ModelData modelData = SetupModelData(model, fbxDir, forest);
                _models[key] = model;
                _modelData[key] = modelData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ModelManagerV2: Error: Failed to load {filePath}: {ex.Message}");
                throw;
            }
        }
        private unsafe ModelData SetupModelData(FBXModel model, string fbxDir, FBXFileForest forest)
        {
            var modelData = new ModelData();
            int meshIndex = 0;
            foreach (var mesh in model.Meshes.Where(m => m.Indices.Count > 0))
            {
                var mmr = new ModelMeshRender();
                List<uint> albedos = new List<uint>();
                List<uint> normals = new List<uint>();
                List<uint> metallics = new List<uint>();
                foreach (var mat in mesh.Materials)
                {
                    var albedoInfo = mat.Textures.GetValueOrDefault("DiffuseColor");
                    uint albedo = 0;
                    if (albedoInfo != null)
                    {
                        int glWrapU = albedoInfo.WrapU == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                        int glWrapV = albedoInfo.WrapV == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                        if (albedoInfo.Path?.StartsWith("embedded:") == true)
                        {
                            string embName = albedoInfo.Path.Substring(9);
                            var data = forest.EmbeddedTextures.FirstOrDefault(t => t.Name == embName).Data;
                            if (data != null)
                            {
                                (albedo, _) = LoadEmbeddedTexture(data, embName, glWrapU, glWrapV);
                            }
                        }
                        else
                        {
                            (albedo, _) = LoadExternalTexture(albedoInfo.Path ?? "", fbxDir, glWrapU, glWrapV);
                        }
                    }
                    albedos.Add(albedo);
                    // Similar for normal and metallic
                    uint normalTex = 0;
                    var normalInfo = mat.Textures.GetValueOrDefault("Bump");
                    if (normalInfo != null)
                    {
                        int glNormalWrapU = normalInfo.WrapU == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                        int glNormalWrapV = normalInfo.WrapV == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                        if (normalInfo.Path?.StartsWith("embedded:") == true)
                        {
                            string embName = normalInfo.Path.Substring(9);
                            var data = forest.EmbeddedTextures.FirstOrDefault(t => t.Name == embName).Data;
                            if (data != null)
                            {
                                (normalTex, _) = LoadEmbeddedTexture(data, embName, glNormalWrapU, glNormalWrapV);
                            }
                        }
                        else
                        {
                            (normalTex, _) = LoadExternalTexture(normalInfo.Path ?? "", fbxDir, glNormalWrapU, glNormalWrapV);
                        }
                    }
                    normals.Add(normalTex);
                    uint metallic = 0;
                    var metallicInfo = mat.Textures.GetValueOrDefault("SpecularColor");
                    if (metallicInfo != null)
                    {
                        int glMetallicWrapU = metallicInfo.WrapU == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                        int glMetallicWrapV = metallicInfo.WrapV == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                        if (metallicInfo.Path?.StartsWith("embedded:") == true)
                        {
                            string embName = metallicInfo.Path.Substring(9);
                            var data = forest.EmbeddedTextures.FirstOrDefault(t => t.Name == embName).Data;
                            if (data != null)
                            {
                                (metallic, _) = LoadEmbeddedTexture(data, embName, glMetallicWrapU, glMetallicWrapV);
                            }
                        }
                        else
                        {
                            (metallic, _) = LoadExternalTexture(metallicInfo.Path ?? "", fbxDir, glMetallicWrapU, glMetallicWrapV);
                        }
                    }
                    metallics.Add(metallic);
                }
                if (albedos.Count > 4)
                {
                    albedos = albedos.Take(4).ToList();
                    normals = normals.Take(4).ToList();
                    metallics = metallics.Take(4).ToList();
                }
                float[] vertexData = new float[mesh.Vertices.Count * 20];
                for (int i = 0; i < mesh.Vertices.Count; i++)
                {
                    var vertex = mesh.Vertices[i];
                    int offset = i * 20;
                    vertexData[offset + 0] = vertex.Position.X;
                    vertexData[offset + 1] = vertex.Position.Y;
                    vertexData[offset + 2] = vertex.Position.Z;
                    vertexData[offset + 3] = vertex.Normal.X;
                    vertexData[offset + 4] = vertex.Normal.Y;
                    vertexData[offset + 5] = vertex.Normal.Z;
                    vertexData[offset + 6] = vertex.TexCoord.X;
                    vertexData[offset + 7] = vertex.TexCoord.Y;
                    vertexData[offset + 8] = vertex.MatIdx;
                    vertexData[offset + 9] = vertex.Tangent.X;
                    vertexData[offset + 10] = vertex.Tangent.Y;
                    vertexData[offset + 11] = vertex.Tangent.Z;
                    vertexData[offset + 12] = vertex.BoneIDs.X;
                    vertexData[offset + 13] = vertex.BoneIDs.Y;
                    vertexData[offset + 14] = vertex.BoneIDs.Z;
                    vertexData[offset + 15] = vertex.BoneIDs.W;
                    vertexData[offset + 16] = vertex.Weights.X;
                    vertexData[offset + 17] = vertex.Weights.Y;
                    vertexData[offset + 18] = vertex.Weights.Z;
                    vertexData[offset + 19] = vertex.Weights.W;
                }
                uint vao = _renderContext.GenVertexArray();
                uint vbo = _renderContext.GenBuffer();
                uint ebo = _renderContext.GenBuffer();
                _renderContext.BindVertexArray(vao);
                fixed (float* ptr = vertexData)
                {
                    _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, vbo);
                    _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(vertexData.Length * sizeof(float)), ptr, _renderContext.Enums.StaticDraw);
                }
                fixed (uint* ptr = mesh.Indices.ToArray())
                {
                    _renderContext.BindBuffer(_renderContext.Enums.ElementArrayBuffer, ebo);
                    _renderContext.BufferData(_renderContext.Enums.ElementArrayBuffer, (uint)(mesh.Indices.Count * sizeof(uint)), ptr, _renderContext.Enums.StaticDraw);
                }
                uint stride = 20 * sizeof(float);
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
                _renderContext.EnableVertexAttribArray(6); // BoneIDs
                _renderContext.VertexAttribIPointer(6, 4, _renderContext.Enums.Int, stride, (void*)(12 * sizeof(float)));
                _renderContext.EnableVertexAttribArray(7); // BoneWeights
                _renderContext.VertexAttribPointer(7, 4, _renderContext.Enums.Float, false, stride, (void*)(16 * sizeof(float)));
                _renderContext.BindVertexArray(0);
                mmr.Vao = vao;
                mmr.Vbo = vbo;
                mmr.Ebo = ebo;
                mmr.IndexCount = (uint)mesh.Indices.Count;
                mmr.AlbedoTextures = albedos.ToArray();
                mmr.NormalTextures = normals.ToArray();
                mmr.MetallicTextures = metallics.ToArray();
                modelData.MeshRenders.Add(mmr);
                meshIndex++;
            }
            return modelData;
        }
        private (uint, byte) LoadEmbeddedTexture(byte[] textureData, string textureName, int wrapS, int wrapT)
        {
            return TextureLoader.LoadEmbeddedTexture(_renderContext, textureData, textureName, 1, wrapS, wrapT);
        }
        private (uint, byte) LoadExternalTexture(string texturePath, string fbxDir, int wrapS, int wrapT)
        {
            if (string.IsNullOrEmpty(texturePath)) return (0, 0);
            string fullPath = Path.Combine(fbxDir, texturePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"ModelManagerV2: Texture file not found at {fullPath}");
                return (0, 0);
            }
            return TextureLoader.LoadTexture(_renderContext, fullPath, 1, wrapS, wrapT);
        }
        public bool TryGetModel(string key, out FBXModel model)
        {
            key = key.ToLower();
            return _models.TryGetValue(key, out model);
        }
        public bool TryGetModelData(string key, out ModelData modelData)
        {
            key = key.ToLower();
            return _modelData.TryGetValue(key, out modelData);
        }
    }
}