// Folder: SiegeEngine.Core.AssetParsing.V2
// File: ModelManagerV2.cs
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
namespace SiegeEngine.Core.AssetParsing.V2
{
    public class ModelManagerV2
    {
        private readonly Dictionary<string, FBXModel> _models = new();
        private readonly Dictionary<string, ModelData> _modelData = new();
        private readonly Dictionary<string, Skeleton> _skeletons = new();
        private readonly Dictionary<string, List<Animation>> _animations = new();
        private readonly Dictionary<string, FBXFileForest> _forests = new();
        private readonly Dictionary<string, string> _fbxDirs = new();
        private readonly Dictionary<string, (uint, byte)> _textureCache = new();
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
        public void LoadModel(string filePath)
        {
            string key = Path.GetFileNameWithoutExtension(filePath).ToLower();
            if (_models.ContainsKey(key))
            {
                return;
            }
            string fbxDir = Path.GetDirectoryName(filePath);
            _fbxDirs[key] = fbxDir;
            FBXFileForest forest = FBXParser.Load(filePath);
            _forests[key] = forest;
            FBXModel model = FBXParser.BuildModelFromForest(forest);
            model.Skeleton.LogBoneHierarchy();
            SmoothNormals(model);
            ModelData modelData = SetupModelData(model, fbxDir, forest);
            _models[key] = model;
            _modelData[key] = modelData;
        }
        public void AttachSkeleton(string targetKey, string skeletonPath)
        {
            if (!_models.ContainsKey(targetKey))
            {
                throw new InvalidOperationException($"Target model {targetKey} not loaded");
            }
            string skeletonKey = Path.GetFileNameWithoutExtension(skeletonPath).ToLower();
            Skeleton skeleton;
            if (!_skeletons.TryGetValue(skeletonKey, out skeleton))
            {
                FBXFileForest forest = FBXParser.Load(skeletonPath);
                FBXModel parsedModel = FBXParser.BuildModelFromForest(forest);
                skeleton = parsedModel.Skeleton;
                _skeletons[skeletonKey] = skeleton;
            }
            // Stub for remapping
            _models[targetKey].Skeleton = skeleton;
            _models[targetKey].HasSkin = true;
            UpdateModelData(targetKey);
        }
        public void AttachAnimation(string targetKey, string animPath)
        {
            if (!_models.ContainsKey(targetKey))
            {
                throw new InvalidOperationException($"Target model {targetKey} not loaded");
            }
            string animKey = Path.GetFileNameWithoutExtension(animPath).ToLower();
            List<Animation> anims;
            if (!_animations.TryGetValue(animKey, out anims))
            {
                FBXFileForest forest = FBXParser.Load(animPath);
                FBXModel animModel = FBXParser.BuildModelFromForest(forest);
                anims = animModel.Animations.Where(a => a.Keyframes.Count > 0).ToList();
                _animations[animKey] = anims;
            }
            // Stub for remapping/alignment
            _models[targetKey].Animations.AddRange(anims);
            // No need to update ModelData for animations
        }
        private void UpdateModelData(string key)
        {
            if (!_models.ContainsKey(key) || !_fbxDirs.ContainsKey(key) || !_forests.ContainsKey(key))
            {
                return;
            }
            FBXModel model = _models[key];
            string fbxDir = _fbxDirs[key];
            FBXFileForest forest = _forests[key];
            SmoothNormals(model);
            ModelData modelData = SetupModelData(model, fbxDir, forest);
            _modelData[key] = modelData;
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
                    var albedoInfo = mat.Textures.GetValueOrDefault("albedo");
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
                    var normalInfo = mat.Textures.GetValueOrDefault("normal");
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
                    var metallicInfo = mat.Textures.GetValueOrDefault("metallic");
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
                ComputeTangents(mesh);
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
                    vertexData[offset + 6] = vertex.UV.X;
                    vertexData[offset + 7] = vertex.UV.Y;
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
            string cacheKey = "embedded:" + textureName.ToLowerInvariant();
            if (_textureCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
            var res = TextureLoader.LoadEmbeddedTexture(_renderContext, textureData, textureName, 1, wrapS, wrapT);
            if (res.Item1 != 0)
            {
                _textureCache[cacheKey] = res;
            }
            return res;
        }
        private (uint, byte) LoadExternalTexture(string texturePath, string fbxDir, int wrapS, int wrapT)
        {
            if (string.IsNullOrEmpty(texturePath)) return (0, 0);
            string fullPath = Path.Combine(fbxDir, texturePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            string cacheKey = fullPath.ToLowerInvariant() + ":" + wrapS + ":" + wrapT;
            if (_textureCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"ModelManagerV2: Texture file not found at {fullPath}");
                return (0, 0);
            }
            var res = TextureLoader.LoadTexture(_renderContext, fullPath, 1, wrapS, wrapT);
            if (res.Item1 != 0)
            {
                _textureCache[cacheKey] = res;
            }
            return res;
        }
        private static void SmoothNormals(FBXModel model)
        {
            foreach (var mesh in model.Meshes)
            {
                int vertexCount = mesh.Vertices.Count;
                var vertexToFaces = new List<List<int>>(vertexCount);
                for (int i = 0; i < vertexCount; i++)
                {
                    vertexToFaces.Add(new List<int>());
                }
                for (int i = 0; i < mesh.Indices.Count; i += 3)
                {
                    uint v1 = mesh.Indices[i];
                    uint v2 = mesh.Indices[i + 1];
                    uint v3 = mesh.Indices[i + 2];
                    int faceIdx = i / 3;
                    vertexToFaces[(int)v1].Add(faceIdx);
                    vertexToFaces[(int)v2].Add(faceIdx);
                    vertexToFaces[(int)v3].Add(faceIdx);
                }
                for (int v = 0; v < vertexCount; v++)
                {
                    Vector3 sum = Vector3.Zero;
                    foreach (int faceIdx in vertexToFaces[v])
                    {
                        uint iv1 = mesh.Indices[faceIdx * 3];
                        uint iv2 = mesh.Indices[faceIdx * 3 + 1];
                        uint iv3 = mesh.Indices[faceIdx * 3 + 2];
                        var p1 = mesh.Vertices[(int)iv1].Position;
                        var p2 = mesh.Vertices[(int)iv2].Position;
                        var p3 = mesh.Vertices[(int)iv3].Position;
                        Vector3 normal = Vector3.Normalize(Vector3.Cross(p2 - p1, p3 - p1));
                        sum += normal;
                    }
                    if (vertexToFaces[v].Count > 0)
                    {
                        Vector3 avgNormal = Vector3.Normalize(sum / vertexToFaces[v].Count);
                        var vertex = mesh.Vertices[v];
                        vertex.Normal = avgNormal;
                        mesh.Vertices[v] = vertex;
                    }
                }
            }
        }
        private static void ComputeTangents(MeshData mesh)
        {
            Vector3[] tangents = new Vector3[mesh.Vertices.Count];
            Vector3[] bitangents = new Vector3[mesh.Vertices.Count];
            for (int i = 0; i < mesh.Indices.Count; i += 3)
            {
                int i1 = (int)mesh.Indices[i];
                int i2 = (int)mesh.Indices[i + 1];
                int i3 = (int)mesh.Indices[i + 2];
                var v1 = mesh.Vertices[i1];
                var v2 = mesh.Vertices[i2];
                var v3 = mesh.Vertices[i3];
                Vector3 p1 = v1.Position;
                Vector3 p2 = v2.Position;
                Vector3 p3 = v3.Position;
                Vector2 uv1 = v1.UV;
                Vector2 uv2 = v2.UV;
                Vector2 uv3 = v3.UV;
                Vector3 edge1 = p2 - p1;
                Vector3 edge2 = p3 - p1;
                Vector2 deltaUV1 = uv2 - uv1;
                Vector2 deltaUV2 = uv3 - uv1;
                float denom = deltaUV1.X * deltaUV2.Y - deltaUV2.X * deltaUV1.Y;
                float f = denom != 0 ? 1.0f / denom : 0;
                Vector3 tangent = f * (deltaUV2.Y * edge1 - deltaUV1.Y * edge2);
                Vector3 bitangent = f * (deltaUV1.X * edge2 - deltaUV2.X * edge1);
                tangents[i1] += tangent;
                tangents[i2] += tangent;
                tangents[i3] += tangent;
                bitangents[i1] += bitangent;
                bitangents[i2] += bitangent;
                bitangents[i3] += bitangent;
            }
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                var vertex = mesh.Vertices[i];
                Vector3 n = vertex.Normal;
                Vector3 t = tangents[i];
                Vector3 b = bitangents[i];
                if (t.LengthSquared() > 0) t = Vector3.Normalize(t);
                if (b.LengthSquared() > 0) b = Vector3.Normalize(b);
                t = Vector3.Normalize(t - n * Vector3.Dot(n, t));
                if (Vector3.Dot(Vector3.Cross(n, t), b) < 0)
                {
                    t = -t;
                }
                var newVertex = new FBXVertex
                {
                    Position = vertex.Position,
                    Normal = vertex.Normal,
                    TexCoord = vertex.TexCoord,
                    Tangent = t,
                    BoneIDs = vertex.BoneIDs,
                    Weights = vertex.Weights,
                    MatIdx = vertex.MatIdx,
                    UV = vertex.UV
                };
                mesh.Vertices[i] = newVertex;
            }
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