// Folder: SiegeEngine.AssetParsing
// File: FBXParser.cs
using SiegeEngine.AssetObjects;
using SiegeEngine.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Numerics;
namespace SiegeEngine.AssetParsing
{
    public static class FBXParser
    {
        public static FBXFileForest Load(string path)
        {
            if (!File.Exists(path))
            {
                FBXParserBase.Log($"FBXParser: File not found at {path}");
                return new FBXFileForest(); //, new ParsingContext();
            }
            var context = new FBXFileForest();
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
                {
                    byte[] headerBytes = reader.ReadBytes(21);
                    string header = Encoding.ASCII.GetString(headerBytes);
                    if (!header.StartsWith("Kaydara FBX Binary"))
                    {
                        reader.BaseStream.Position = 0;
                        string firstLine = new StreamReader(reader.BaseStream, Encoding.ASCII).ReadLine();
                        if (firstLine?.StartsWith("; FBX") == true)
                        {
                            FBXParserBase.Log($"FBXParser: ASCII FBX file detected at {path}, not supported. Please convert to binary using Autodesk FBX Converter.");
                            return new FBXFileForest();
                        }
                        FBXParserBase.Log($"FBXParser: Invalid FBX file format at {path}");
                        return new FBXFileForest();
                    }
                    reader.ReadBytes(2); // Padding
                    uint version = reader.ReadUInt32();
                    FBXParserBase.Log($"FBXParser: FBX version {version}");
                    if (version < 7000)
                    {
                        FBXParserBase.Log($"FBXParser: Unsupported FBX version {version} at {path}, requires version 7000 or higher.");
                        return new FBXFileForest();
                    }
                    long fileLength = stream.Length;
                    NodeParser.ParseNodes(reader, //model, context,
                        version,// 0,
                        fileLength, fileLength, //nodeContext,
                        null, context);
                    MetaDataExporter.ExportMetadata(context, $"{path}._metadata.json");
                }
            }
            catch (Exception ex)
            {
                FBXParserBase.Log($"FBXParser: Error loading {path}: {ex.Message}");
                return new FBXFileForest();
            }
            return context;
        }
        public static FBXModel BuildModelFromForest(FBXFileForest forest)
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
    }
}