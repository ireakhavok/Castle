//SiegeEngine.Managers/ModelManager.cs
using SiegeEngine.Definitions;
using SiegeEngine.AssetObjects;
using SiegeEngine.AssetParsing;
using SiegeEngine.Rendering;
using SiegeEngine.UnityAssetLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SiegeEngine.ContextManagement;

namespace SiegeEngine.Managers
{
    public class ModelManager
    {
        private readonly Dictionary<string, FBXModel> _models = new();
        private readonly Dictionary<string, ModelData> _modelData = new();
        private readonly string _primaryPath;
        private readonly string _fallbackPath;
        private readonly ModManager _modManager;
        private readonly IRenderContext _renderContext;
        private int _nextEntityId = 1;
        private readonly MetaFileParser _metaParser;
        private readonly PrefabFileReader _prefabReader;

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

        public ModelManager(string primaryPath = "Mods/Models", string fallbackPath = "Assets/Models", ModManager modManager = null, IRenderContext renderContext = null)
        {
            _primaryPath = primaryPath;
            _fallbackPath = fallbackPath;
            _modManager = modManager;
            _renderContext = renderContext;
            _metaParser = new MetaFileParser();
            _prefabReader = new PrefabFileReader();
            Directory.CreateDirectory(_primaryPath);
            Directory.CreateDirectory(_fallbackPath);
            string charactersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters");
            Directory.CreateDirectory(charactersPath);
            ScanDirectory(_primaryPath);
            ScanDirectory(_fallbackPath);
            ScanDirectory(charactersPath);
        }

        public void LoadCharacters()
        {
            string charactersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters");
            if (!Directory.Exists(charactersPath))
            {
                Console.WriteLine($"ModelManager: Error: Character assets directory not found at {charactersPath}");
                return;
            }
            string fbxPath = Path.Combine(charactersPath, "Man_Mesh.fbx");
            if (File.Exists(fbxPath))
            {
                Console.WriteLine($"ModelManager: Found Man_Mesh.fbx at {fbxPath}");
                LoadModel(fbxPath, new HashSet<string>(), new Dictionary<string, string>());
                _models["player"] = _models["man_mesh"];
            }
            else
            {
                Console.WriteLine($"ModelManager: Error: Man_Mesh.fbx not found at {fbxPath}");
            }
        }

        public void ScanDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"ModelManager: Error: Directory not found at {path}");
                return;
            }
            foreach (var file in Directory.GetFiles(path, "*.fbx", SearchOption.AllDirectories))
            {
                string key = Path.GetFileNameWithoutExtension(file).ToLower();
                Console.WriteLine($"ModelManager: Found FBX file: {file}, key: {key}");
                if (!_models.ContainsKey(key))
                {
                    _models[key] = null;
                }
            }
        }

        public void LoadPlayerFromPrefab(string prefabPath)
        {
            string fbxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters", "Man_Mesh.fbx");
            Console.WriteLine($"ModelManager: Loading Man_Mesh.fbx from {fbxPath} as player model");
            LoadModel(fbxPath, new HashSet<string>(), new Dictionary<string, string>());
            _models["player"] = _models["man_mesh"];
        }

        private HashSet<string> ParsePrefabReferences(string prefabPath)
        {
            return new HashSet<string>();
        }

        private Dictionary<string, string> BuildGuidToPathMap(string basePath)
        {
            return new Dictionary<string, string>();
        }

        private (uint TextureId, byte PixelDepth) LoadEmbeddedTexture(byte[] textureData, string textureName, int wrapS, int wrapT)
        {
            if (textureData == null || textureData.Length < 18)
            {
                Console.WriteLine($"ModelManager: Warning: Empty or invalid texture data for {textureName}");
                return (0, 0);
            }
            try
            {
                (uint textureId, byte pixelDepth) = TextureLoader.LoadEmbeddedTexture(_renderContext, textureData, textureName, 1, wrapS, wrapT);
                if (textureId != 0)
                {
                    Console.WriteLine($"ModelManager: Loaded embedded texture '{textureName}' with {textureData.Length} bytes, assumed PixelDepth={pixelDepth}");
                }
                else
                {
                    Console.WriteLine($"ModelManager: Error loading embedded texture '{textureName}', skipping");
                    textureId = 0;
                }
                return (textureId, pixelDepth);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ModelManager: Error loading embedded texture '{textureName}': {ex.Message}, skipping");
                return (0, 0);
            }
        }

        private (uint, byte) LoadExternalTexture(string texturePath, string fbxDir, int wrapS, int wrapT)
        {
            if (string.IsNullOrEmpty(texturePath))
            {
                Console.WriteLine($"ModelManager: Warning: Empty texture path, skipping");
                return (0, 0);
            }
            texturePath = Path.Combine(fbxDir, texturePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
            string tgaPath = texturePath;
            string pngPath = Path.ChangeExtension(texturePath, ".png");
            byte pixelDepth;
            try
            {
                uint textureId = 0;
                if (File.Exists(tgaPath))
                {
                    (textureId, pixelDepth) = TextureLoader.LoadTgaTexture(_renderContext, tgaPath, wrapS, wrapT);
                    if (textureId != 0)
                    {
                        Console.WriteLine($"ModelManager: Loaded external TGA texture from {tgaPath}");
                        return (textureId, pixelDepth);
                    }
                }
                if (File.Exists(pngPath))
                {
                    (textureId, pixelDepth) = TextureLoader.LoadTexture(_renderContext, pngPath, 1, wrapS, wrapT);
                    if (textureId != 0)
                    {
                        Console.WriteLine($"ModelManager: Loaded external PNG texture from {pngPath}");
                        return (textureId, pixelDepth);
                    }
                }
                string dir = Path.GetDirectoryName(texturePath);
                if (Directory.Exists(dir))
                {
                    var pngFiles = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly);
                    foreach (var fallbackPng in pngFiles.OrderBy(p => Path.GetFileName(p)))
                    {
                        if (Path.GetFileName(fallbackPng).Equals(Path.GetFileName(pngPath), StringComparison.OrdinalIgnoreCase))
                        {
                            (textureId, pixelDepth) = TextureLoader.LoadTexture(_renderContext, fallbackPng, 1, wrapS, wrapT);
                            if (textureId != 0)
                            {
                                Console.WriteLine($"ModelManager: Loaded exact match PNG texture from {fallbackPng}");
                                return (textureId, pixelDepth);
                            }
                        }
                    }
                    foreach (var fallbackPng in pngFiles.OrderBy(p => Path.GetFileName(p)))
                    {
                        (textureId, pixelDepth) = TextureLoader.LoadTexture(_renderContext, fallbackPng, 1, wrapS, wrapT);
                        if (textureId != 0)
                        {
                            Console.WriteLine($"ModelManager: Loaded fallback PNG texture from {fallbackPng}");
                            return (textureId, pixelDepth);
                        }
                    }
                }
                Console.WriteLine($"ModelManager: Warning: Texture file not found at {tgaPath} or {pngPath}, and no valid .png fallback found, skipping");
                return (0, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ModelManager: Error loading external texture {tgaPath} or {pngPath}: {ex.Message}, skipping");
                return (0, 0);
            }
        }

        public unsafe void LoadModel(string filePath, HashSet<string> referencedGuids, Dictionary<string, string> guidToPath)
        {
            string key = Path.GetFileNameWithoutExtension(filePath).ToLower();
            string fbxDir = Path.GetDirectoryName(filePath);
            try
            {
                FBXFileForest forest = FBXParser.Load(filePath);
                FBXModel model = BuildModelFromForest(forest);
                // Smooth normals for the model
                SmoothNormals(model);
                // Apply transformations
                Matrix4x4 rotation = Matrix4x4.CreateRotationZ(MathF.PI); // Flip around X axis
                Matrix4x4 scale = Matrix4x4.CreateScale(0.1f);
                Matrix4x4 transformation = rotation * scale; // Apply rotation first, then scale
                ModelTransformer.ApplyTransformation(model, transformation);
                foreach (var mesh in model.Meshes)
                {
                    ComputeTangents(mesh);
                }
                if (model == null || model.Meshes.Count == 0 || model.Meshes.Sum(m => m.Vertices.Count) < 3 || model.Meshes.Sum(m => m.Indices.Count) < 3)
                {
                    throw new InvalidOperationException($"ModelManager: Error: Invalid model {key}, total vertices: {model?.Meshes.Sum(m => m.Vertices.Count) ?? 0}, total indices: {model?.Meshes.Sum(m => m.Indices.Count) ?? 0}");
                }
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
                        byte depth;
                        if (albedoInfo != null)
                        {
                            int glWrapU = albedoInfo.WrapU == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                            int glWrapV = albedoInfo.WrapV == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                            if (albedoInfo.Path?.StartsWith("embedded_") == true)
                            {
                                string embName = albedoInfo.Path.Substring(9);
                                var data = forest.EmbeddedTextures.FirstOrDefault(t => t.Name == embName).Data;
                                if (data != null)
                                {
                                    (albedo, depth) = LoadEmbeddedTexture(data, embName, glWrapU, glWrapV);
                                }
                            }
                            else
                            {
                                (albedo, depth) = LoadExternalTexture(albedoInfo.Path ?? "", fbxDir, glWrapU, glWrapV);
                            }
                        }
                        albedos.Add(albedo);
                        var normalInfo = mat.Textures.GetValueOrDefault("normal");
                        uint normalTex = 0;
                        if (normalInfo != null)
                        {
                            int glNormalWrapU = normalInfo.WrapU == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                            int glNormalWrapV = normalInfo.WrapV == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                            if (normalInfo.Path?.StartsWith("embedded_") == true)
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
                                (normalTex, depth) = LoadExternalTexture(normalInfo.Path ?? "", fbxDir, glNormalWrapU, glNormalWrapV);
                            }
                        }
                        normals.Add(normalTex);
                        var metallicInfo = mat.Textures.GetValueOrDefault("metallic");
                        uint metallicTex = 0;
                        if (metallicInfo != null)
                        {
                            int glMetallicWrapU = metallicInfo.WrapU == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                            int glMetallicWrapV = metallicInfo.WrapV == 0 ? _renderContext.Enums.Repeat : _renderContext.Enums.ClampToEdge;
                            if (metallicInfo.Path?.StartsWith("embedded_") == true)
                            {
                                string embName = metallicInfo.Path.Substring(9);
                                var data = forest.EmbeddedTextures.FirstOrDefault(t => t.Name == embName).Data;
                                if (data != null)
                                {
                                    (metallicTex, _) = LoadEmbeddedTexture(data, embName, glMetallicWrapU, glMetallicWrapV);
                                }
                            }
                            else
                            {
                                (metallicTex, depth) = LoadExternalTexture(metallicInfo.Path ?? "", fbxDir, glMetallicWrapU, glMetallicWrapV);
                            }
                        }
                        metallics.Add(metallicTex);
                    }
                    if (albedos.Count > 4)
                    {
                        Console.WriteLine($"ModelManager: Warning: {albedos.Count} materials for mesh {meshIndex} in {key}, limiting to 4");
                        albedos = albedos.Take(4).ToList();
                        normals = normals.Take(4).ToList();
                        metallics = metallics.Take(4).ToList();
                    }
                    float[] vertexData = new float[mesh.Vertices.Count * 12];
                    int defaultNormalCount = 0;
                    int zeroNormalCount = 0;
                    Dictionary<float, int> materialIndexCounts = new Dictionary<float, int>();
                    float minX = float.MaxValue, maxX = float.MinValue;
                    float minY = float.MaxValue, maxY = float.MinValue;
                    float minZ = float.MaxValue, maxZ = float.MinValue;
                    for (int i = 0; i < mesh.Vertices.Count; i++)
                    {
                        var vertex = mesh.Vertices[i];
                        vertexData[i * 12 + 0] = vertex.X;
                        vertexData[i * 12 + 1] = vertex.Y;
                        vertexData[i * 12 + 2] = vertex.Z;
                        vertexData[i * 12 + 3] = vertex.Nx;
                        vertexData[i * 12 + 4] = vertex.Ny;
                        vertexData[i * 12 + 5] = vertex.Nz;
                        vertexData[i * 12 + 6] = vertex.U;
                        vertexData[i * 12 + 7] = vertex.V;
                        float materialIndex = vertex.MatIdx;
                        vertexData[i * 12 + 8] = materialIndex;
                        vertexData[i * 12 + 9] = vertex.Tx;
                        vertexData[i * 12 + 10] = vertex.Ty;
                        vertexData[i * 12 + 11] = vertex.Tz;
                        materialIndexCounts.TryGetValue(materialIndex, out int count);
                        materialIndexCounts[materialIndex] = count + 1;
                        minX = Math.Min(minX, vertex.X);
                        maxX = Math.Max(maxX, vertex.X);
                        minY = Math.Min(minY, vertex.Y);
                        maxY = Math.Max(maxY, vertex.Y);
                        minZ = Math.Min(minZ, vertex.Z);
                        maxZ = Math.Max(maxZ, vertex.Z);
                        if (vertex.Nx == 0f && vertex.Ny == 0f && vertex.Nz == 1f)
                            defaultNormalCount++;
                        if (vertex.Nx == 0f && vertex.Ny == 0f && vertex.Nz == 0f)
                            zeroNormalCount++;
                    }
                    Console.WriteLine($"ModelManager: Loaded mesh {meshIndex} for {key} with {mesh.Materials.Count} materials");
                    Console.WriteLine($"ModelManager: Vertex ranges: X=({minX}, {maxX}), Y=({minY}, {maxY}), Z=({minZ}, {maxZ})");
                    Console.WriteLine($"ModelManager: Bounds: Width={maxX - minX:F2}, Height={maxY - minY:F2}, Depth={maxZ - minZ:F2}");
                    Console.WriteLine($"ModelManager: {defaultNormalCount} of {mesh.Vertices.Count} vertices have default normals (0, 0, 1)");
                    Console.WriteLine($"ModelManager: {zeroNormalCount} of {mesh.Vertices.Count} vertices have zero normals (0, 0, 0)");
                    Console.WriteLine($"ModelManager: Material index distribution: {string.Join(", ", materialIndexCounts.Select(kv => $"Index {kv.Key}: {kv.Value} vertices"))}");
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
                    uint stride = 12 * sizeof(float);
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
                _models[key] = model;
                _modelData[key] = modelData;
                Console.WriteLine($"ModelManager: Loaded {key} with {modelData.MeshRenders.Count} meshes, total vertices: {model.Meshes.Sum(m => m.Vertices.Count)}, total triangles: {model.Meshes.Sum(m => m.Indices.Count / 3)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ModelManager: Error: Failed to load {filePath}: {ex.Message}");
                throw;
            }
        }

        private static FBXModel BuildModelFromForest(FBXFileForest forest)
        {
            FBXModel model = new FBXModel();
            var objectsNode = forest.TreeList.FirstOrDefault(n => n.Name == "Objects");
            if (objectsNode == null)
            {
                Console.WriteLine("BuildModelFromForest: No Objects node found");
                return FBXParserBase.CreateDefaultCubeModel();
            }
            var objectsById = new Dictionary<long, BaseNode>();
            foreach (var child in objectsNode.children)
            {
                if (child.properties.Count >= 1 && child.properties[0].TypeCode == 'L')
                {
                    long id = (long)child.properties[0].Value;
                    objectsById[id] = child;
                }
            }
            var connectionsNode = forest.TreeList.FirstOrDefault(n => n.Name == "Connections");
            var conns = new List<(string type, long child, long parent, string prop)>();
            if (connectionsNode != null)
            {
                foreach (var conn in connectionsNode.children)
                {
                    if (conn.Name == "C" && conn.properties.Count >= 3)
                    {
                        string type = (string)conn.properties[0].Value;
                        long child = (long)conn.properties[1].Value;
                        long parent = (long)conn.properties[2].Value;
                        string prop = conn.properties.Count > 3 ? (string)conn.properties[3].Value : null;
                        conns.Add((type, child, parent, prop));
                    }
                }
            }
            var geomNodes = objectsNode.children.Where(n => n.Name == "Geometry" && n.properties.Count >= 3 && (string)n.properties[2].Value == "Mesh").ToList();
            if (geomNodes.Count == 0)
            {
                Console.WriteLine("BuildModelFromForest: No Geometry::Mesh nodes found");
                return FBXParserBase.CreateDefaultCubeModel();
            }
            foreach (var geom in geomNodes)
            {
                MeshData mesh = new MeshData();
                // Get vertices (unique positions)
                var vertsNode = geom.children.FirstOrDefault(c => c.Name == "Vertices");
                double[] vertsD = null;
                if (vertsNode != null && vertsNode.properties.Count > 0 && vertsNode.properties[0].TypeCode == 'd')
                {
                    vertsD = (double[])vertsNode.properties[0].Value;
                }
                // Get polygon vertex indices
                var indicesNode = geom.children.FirstOrDefault(c => c.Name == "PolygonVertexIndex");
                int[] pviArray = null;
                if (indicesNode != null && indicesNode.properties.Count > 0 && indicesNode.properties[0].TypeCode == 'i')
                {
                    pviArray = (int[])indicesNode.properties[0].Value;
                }
                if (vertsD == null || pviArray == null)
                {
                    continue; // Skip invalid mesh
                }
                // Get normals layer
                var layerNorm = geom.children.FirstOrDefault(c => c.Name == "LayerElementNormal");
                double[] norms = null;
                int[] normIdx = null;
                string normMapping = "";
                string normRef = "";
                if (layerNorm != null)
                {
                    var mappingNode = layerNorm.children.FirstOrDefault(c => c.Name == "MappingInformationType");
                    normMapping = mappingNode != null ? (string)mappingNode.properties[0].Value : "";
                    var refNode = layerNorm.children.FirstOrDefault(c => c.Name == "ReferenceInformationType");
                    normRef = refNode != null ? (string)refNode.properties[0].Value : "";
                    var normalsNode = layerNorm.children.FirstOrDefault(c => c.Name == "Normals");
                    if (normalsNode != null && normalsNode.properties[0].TypeCode == 'd')
                    {
                        norms = (double[])normalsNode.properties[0].Value;
                    }
                    var normalsIndexNode = layerNorm.children.FirstOrDefault(c => c.Name == "NormalsIndex");
                    if (normalsIndexNode != null && normalsIndexNode.properties[0].TypeCode == 'i')
                    {
                        normIdx = (int[])normalsIndexNode.properties[0].Value;
                    }
                }
                // Get UV layer
                var layerUV = geom.children.FirstOrDefault(c => c.Name == "LayerElementUV");
                double[] uvs = null;
                int[] uvIdx = null;
                string uvMapping = "";
                string uvRef = "";
                if (layerUV != null)
                {
                    var mappingNode = layerUV.children.FirstOrDefault(c => c.Name == "MappingInformationType");
                    uvMapping = mappingNode != null ? (string)mappingNode.properties[0].Value : "";
                    var refNode = layerUV.children.FirstOrDefault(c => c.Name == "ReferenceInformationType");
                    uvRef = refNode != null ? (string)refNode.properties[0].Value : "";
                    var uvNode = layerUV.children.FirstOrDefault(c => c.Name == "UV");
                    if (uvNode != null && uvNode.properties[0].TypeCode == 'd')
                    {
                        uvs = (double[])uvNode.properties[0].Value;
                    }
                    var uvIndexNode = layerUV.children.FirstOrDefault(c => c.Name == "UVIndex");
                    if (uvIndexNode != null && uvIndexNode.properties[0].TypeCode == 'i')
                    {
                        uvIdx = (int[])uvIndexNode.properties[0].Value;
                    }
                }
                // Get material layer
                var layerMat = geom.children.FirstOrDefault(c => c.Name == "LayerElementMaterial");
                int[] matIndices = null;
                string matMapping = "";
                if (layerMat != null)
                {
                    var matNode = layerMat.children.FirstOrDefault(c => c.Name == "Materials");
                    if (matNode != null && matNode.properties[0].TypeCode == 'i')
                    {
                        matIndices = (int[])matNode.properties[0].Value;
                    }
                    var mappingNode = layerMat.children.FirstOrDefault(c => c.Name == "MappingInformationType");
                    matMapping = mappingNode != null ? (string)mappingNode.properties[0].Value : "";
                }
                // Build expanded vertices for ByPolygonVertex
                List<FBXVertex> expandedVertices = new List<FBXVertex>();
                List<uint> newIndices = new List<uint>();
                int currentIndex = 0;
                List<int> tempPoly = new List<int>();
                for (int i = 0; i < pviArray.Length; i++)
                {
                    int pv = pviArray[i];
                    bool end = pv < 0;
                    int vId = end ? -pv - 1 : pv;
                    tempPoly.Add(vId);
                    if (end)
                    {
                        // Triangulate polygon
                        for (int j = 1; j < tempPoly.Count - 1; j++)
                        {
                            newIndices.Add((uint)currentIndex);
                            newIndices.Add((uint)(currentIndex + j));
                            newIndices.Add((uint)(currentIndex + j + 1));
                        }
                        // Add vertices for the polygon
                        for (int k = 0; k < tempPoly.Count; k++)
                        {
                            int vertIdx = tempPoly[k];
                            float x = (float)vertsD[vertIdx * 3];
                            float y = (float)vertsD[vertIdx * 3 + 1];
                            float z = (float)vertsD[vertIdx * 3 + 2];
                            // Normal
                            float nx = 0f, ny = 0f, nz = 1f; // Default
                            if (norms != null)
                            {
                                int nIdx;
                                if (normMapping == "ByPolygonVertex")
                                {
                                    if (normRef == "IndexToDirect" && normIdx != null)
                                    {
                                        nIdx = normIdx[i - tempPoly.Count + 1 + k];
                                    }
                                    else // Direct
                                    {
                                        nIdx = i - tempPoly.Count + 1 + k;
                                    }
                                }
                                else // ByVertex or similar
                                {
                                    nIdx = vertIdx;
                                }
                                nx = (float)norms[nIdx * 3];
                                ny = (float)norms[nIdx * 3 + 1];
                                nz = (float)norms[nIdx * 3 + 2];
                            }
                            // UV
                            float u = 0f, v = 0f;
                            if (uvs != null)
                            {
                                int uIdx;
                                if (uvMapping == "ByPolygonVertex")
                                {
                                    if (uvRef == "IndexToDirect" && uvIdx != null)
                                    {
                                        uIdx = uvIdx[i - tempPoly.Count + 1 + k];
                                    }
                                    else // Direct
                                    {
                                        uIdx = i - tempPoly.Count + 1 + k;
                                    }
                                }
                                else // ByVertex
                                {
                                    uIdx = vertIdx;
                                }
                                u = (float)uvs[uIdx * 2];
                                v = 1f - (float)uvs[uIdx * 2 + 1];
                            }
                            // Material
                            float matId = 0f;
                            if (matIndices != null)
                            {
                                int polyIdx = (i - tempPoly.Count + 1) / tempPoly.Count; // Approximate poly index
                                if (matMapping == "AllSame")
                                {
                                    matId = matIndices[0];
                                }
                                else if (matMapping == "ByPolygon")
                                {
                                    matId = matIndices[polyIdx];
                                }
                            }
                            expandedVertices.Add(new FBXVertex(x, y, z, nx, ny, nz, u, v, matId));
                        }
                        currentIndex += tempPoly.Count;
                        tempPoly.Clear();
                    }
                }
                mesh.Vertices = expandedVertices;
                mesh.Indices = newIndices;
                // Extract materials (unchanged)
                long geomId = (long)geom.properties[0].Value;
                var modelConns = conns.Where(c => c.type == "OO" && c.child == geomId).ToList();
                if (modelConns.Count > 0)
                {
                    long modelId = modelConns[0].parent;
                    var modelNode = objectsById[modelId];
                    var matConns = conns.Where(c => c.type == "OO" && c.parent == modelId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "Material").ToList();
                    foreach (var matConn in matConns)
                    {
                        var matNode = objectsById[matConn.child];
                        Material mat = new Material { Name = ((string)matNode.properties[1].Value).Split('\0')[0] };
                        var props70 = matNode.children.FirstOrDefault(c => c.Name == "Properties70");
                        if (props70 != null)
                        {
                            foreach (var p in props70.children)
                            {
                                if (p.Name == "P" && p.properties.Count >= 5)
                                {
                                    string pname = (string)p.properties[0].Value;
                                    mat.Properties[pname] = p.properties[4].Value;
                                }
                            }
                        }
                        // Find textures
                        var texConns = conns.Where(c => c.type == "OP" && c.parent == matConn.child).ToList();
                        foreach (var texConn in texConns)
                        {
                            var texNode = objectsById[texConn.child];
                            string texKey;
                            switch (texConn.prop)
                            {
                                case "DiffuseColor": texKey = "albedo"; break;
                                case "NormalMap": texKey = "normal"; break;
                                case "SpecularColor": texKey = "metallic"; break;
                                default: continue;
                            }
                            TextureInfo texInfo = new TextureInfo();
                            string fileName = texNode.children.FirstOrDefault(c => c.Name == "FileName")?.properties[0].Value as string ?? "";
                            string relFile = texNode.children.FirstOrDefault(c => c.Name == "RelativeFilename")?.properties[0].Value as string ?? "";
                            relFile = relFile.Replace('\\', '/');
                            var texProps70 = texNode.children.FirstOrDefault(c => c.Name == "Properties70");
                            if (texProps70 != null)
                            {
                                var wrapUP = texProps70.children.FirstOrDefault(p => p.Name == "P" && (string)p.properties[0].Value == "WrapModeU");
                                if (wrapUP != null && wrapUP.properties.Count >= 5)
                                {
                                    texInfo.WrapU = (int)wrapUP.properties[4].Value;
                                }
                                var wrapVP = texProps70.children.FirstOrDefault(p => p.Name == "P" && (string)p.properties[0].Value == "WrapModeV");
                                if (wrapVP != null && wrapVP.properties.Count >= 5)
                                {
                                    texInfo.WrapV = (int)wrapVP.properties[4].Value;
                                }
                            }
                            bool isEmbedded = false;
                            // Check for Media property
                            var mediaP = texProps70?.children.FirstOrDefault(p => p.Name == "P" && (string)p.properties[0].Value == "Media");
                            if (mediaP != null && mediaP.properties.Count >= 5)
                            {
                                string mediaName = (string)mediaP.properties[4].Value;
                                var videoNode = objectsNode.children.FirstOrDefault(v => v.Name == "Video" && (string)v.properties[1].Value == mediaName);
                                if (videoNode != null)
                                {
                                    var contentNode = videoNode.children.FirstOrDefault(c => c.Name == "Content");
                                    if (contentNode != null && contentNode.properties.Count > 0 && contentNode.properties[0].TypeCode == 'R')
                                    {
                                        byte[] content = (byte[])contentNode.properties[0].Value;
                                        string vidName = ((string)videoNode.properties[1].Value).Split("::")[1];
                                        forest.EmbeddedTextures.Add((vidName, content));
                                        texInfo.Path = "embedded_" + vidName;
                                        isEmbedded = true;
                                        Console.WriteLine($"ModelManager: Found embedded texture via Media property: {vidName}");
                                    }
                                }
                            }
                            // Fallback to connection if not found via Media
                            if (!isEmbedded && relFile.Contains(".fbm/"))
                            {
                                var videoConns = conns.Where(c => c.type == "OO" && c.parent == texConn.child).ToList();
                                if (videoConns.Count > 0)
                                {
                                    long videoId = videoConns[0].child;
                                    var videoNode = objectsById[videoId];
                                    var contentNode = videoNode.children.FirstOrDefault(c => c.Name == "Content");
                                    if (contentNode != null && contentNode.properties[0].TypeCode == 'R')
                                    {
                                        byte[] content = (byte[])contentNode.properties[0].Value;
                                        string vidName = ((string)videoNode.properties[1].Value).Split('\0')[0];
                                        forest.EmbeddedTextures.Add((vidName, content));
                                        texInfo.Path = "embedded_" + vidName;
                                        isEmbedded = true;
                                        Console.WriteLine($"ModelManager: Found embedded texture via connection: {vidName}");
                                    }
                                }
                            }
                            if (!isEmbedded)
                            {
                                texInfo.Path = relFile != "" ? relFile : fileName;
                            }
                            mat.Textures[texKey] = texInfo;
                        }
                        mesh.Materials.Add(mat);
                    }
                }
                // Calculate bounds
                float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
                foreach (var v in mesh.Vertices)
                {
                    minX = Math.Min(minX, v.X);
                    minY = Math.Min(minY, v.Y);
                    minZ = Math.Min(minZ, v.Z);
                    maxX = Math.Max(maxX, v.X);
                    maxY = Math.Max(maxY, v.Y);
                    maxZ = Math.Max(maxZ, v.Z);
                }
                mesh.Bounds = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
                model.Meshes.Add(mesh);
            }
            return model;
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
                        var p1 = new Vector3(mesh.Vertices[(int)iv1].X, mesh.Vertices[(int)iv1].Y, mesh.Vertices[(int)iv1].Z);
                        var p2 = new Vector3(mesh.Vertices[(int)iv2].X, mesh.Vertices[(int)iv2].Y, mesh.Vertices[(int)iv2].Z);
                        var p3 = new Vector3(mesh.Vertices[(int)iv3].X, mesh.Vertices[(int)iv3].Y, mesh.Vertices[(int)iv3].Z);
                        Vector3 normal = Vector3.Normalize(Vector3.Cross(p2 - p1, p3 - p1));
                        sum += normal;
                    }
                    if (vertexToFaces[v].Count > 0)
                    {
                        Vector3 avgNormal = Vector3.Normalize(sum / vertexToFaces[v].Count);
                        mesh.Vertices[v].Nx = avgNormal.X;
                        mesh.Vertices[v].Ny = avgNormal.Y;
                        mesh.Vertices[v].Nz = avgNormal.Z;
                    }
                }
            }
        }
        private static void ComputeTangents(MeshData mesh)
        {
            List<Vector3> tangents = new List<Vector3>(new Vector3[mesh.Vertices.Count]);
            List<Vector3> bitangents = new List<Vector3>(new Vector3[mesh.Vertices.Count]);
            for (int i = 0; i < mesh.Indices.Count; i += 3)
            {
                uint i1 = mesh.Indices[i];
                uint i2 = mesh.Indices[i + 1];
                uint i3 = mesh.Indices[i + 2];
                var v1 = mesh.Vertices[(int)i1];
                var v2 = mesh.Vertices[(int)i2];
                var v3 = mesh.Vertices[(int)i3];
                Vector3 p1 = new Vector3(v1.X, v1.Y, v1.Z);
                Vector3 p2 = new Vector3(v2.X, v2.Y, v2.Z);
                Vector3 p3 = new Vector3(v3.X, v3.Y, v3.Z);
                Vector2 uv1 = new Vector2(v1.U, v1.V);
                Vector2 uv2 = new Vector2(v2.U, v2.V);
                Vector2 uv3 = new Vector2(v3.U, v3.V);
                Vector3 edge1 = p2 - p1;
                Vector3 edge2 = p3 - p1;
                Vector2 deltaUV1 = uv2 - uv1;
                Vector2 deltaUV2 = uv3 - uv1;
                float denom = deltaUV1.X * deltaUV2.Y - deltaUV2.X * deltaUV1.Y;
                float f = denom != 0 ? 1.0f / denom : 0;
                Vector3 tangent = f * (deltaUV2.Y * edge1 - deltaUV1.Y * edge2);
                Vector3 bitangent = f * (deltaUV1.X * edge2 - deltaUV2.X * edge1);
                tangents[(int)i1] += tangent;
                tangents[(int)i2] += tangent;
                tangents[(int)i3] += tangent;
                bitangents[(int)i1] += bitangent;
                bitangents[(int)i2] += bitangent;
                bitangents[(int)i3] += bitangent;
            }
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                var vertex = mesh.Vertices[i];
                Vector3 n = new Vector3(vertex.Nx, vertex.Ny, vertex.Nz);
                Vector3 t = tangents[i];
                Vector3 b = bitangents[i];
                if (t.LengthSquared() > 0) t = Vector3.Normalize(t);
                if (b.LengthSquared() > 0) b = Vector3.Normalize(b);
                // Gram-Schmidt orthogonalization
                t = Vector3.Normalize(t - n * Vector3.Dot(n, t));
                // Handedness check
                if (Vector3.Dot(Vector3.Cross(n, t), b) < 0)
                {
                    t = -t;
                }
                mesh.Vertices[i] = new FBXVertex(
                    vertex.X, vertex.Y, vertex.Z,
                    vertex.Nx, vertex.Ny, vertex.Nz,
                    vertex.U, vertex.V,
                    vertex.MatIdx,
                    t.X, t.Y, t.Z
                );
            }
        }
        public bool TryGetModel(string key, out FBXModel model)
        {
            key = key.ToLower();
            if (_models.TryGetValue(key, out model) && model != null)
                return true;
            Console.WriteLine($"ModelManager: Error: Model {key} not found or not loaded");
            model = null;
            return false;
        }
        public bool TryGetModelData(string key, out ModelData modelData)
        {
            key = key.ToLower();
            if (_modelData.TryGetValue(key, out modelData))
                return true;
            Console.WriteLine($"ModelManager: Error: Model data {key} not found");
            modelData = null;
            return false;
        }
        public Entity CreateEntity(string key, Vector3 position)
        {
            key = key.ToLower();
            if (!TryGetModel(key, out var model))
            {
                Console.WriteLine($"ModelManager: Error: Model {key} not found or loaded");
                LoadModel(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Characters", "Man_Mesh.fbx"), new HashSet<string>(), new Dictionary<string, string>());
                key = "man_mesh";
                _models["player"] = _models["man_mesh"];
                model = _models[key];
            }
            var entity = new Entity { Id = _nextEntityId++, Type = key == "man_mesh" ? "Player" : "Model" };
            entity.AddComponent(new ModelComponent { Model = model, Key = key });
            var physics = new PhysicsComponent
            {
                Position = position,
                Size = CalculateModelBounds(model)
            };
            entity.AddComponent(physics);
            return entity;
        }
        private Vector3 CalculateModelBounds(FBXModel model)
        {
            if (model.Meshes.Count == 0 || model.Meshes.Sum(m => m.Vertices.Count) == 0)
                return new Vector3(1f, 1f, 1f);
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var mesh in model.Meshes)
            {
                foreach (var vertex in mesh.Vertices)
                {
                    minX = Math.Min(minX, vertex.X);
                    minY = Math.Min(minY, vertex.Y);
                    minZ = Math.Min(minZ, vertex.Z);
                    maxX = Math.Max(maxX, vertex.X);
                    maxY = Math.Max(maxY, vertex.Y);
                    maxZ = Math.Max(maxZ, vertex.Z);
                }
            }
            Vector3 bounds = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
            Console.WriteLine($"ModelManager: Calculated bounds for model: Width={bounds.X:F2}, Height={bounds.Y:F2}, Depth={bounds.Z:F2}");
            return bounds;
        }
    }
}