// Folder: SiegeEngine.Core
// File: AssetParsing.V2/FBXMeshParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.V2.Model;

namespace SiegeEngine.Core.AssetParsing.V2
{
    public static class FBXMeshParser
    {
        public static void ParseMeshes(FBXModel model, BaseNode objectsNode, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, int[] sourceToTarget, int[] signs, float modelScale, Dictionary<long, int> boneIndexById, List<int> rootIndices, Matrix4x4 P4, Matrix4x4 invP4, FBXFileForest forest)
        {
            var meshNodes = objectsNode.children.Where(n => n.Name == "Geometry" && n.properties.Count > 2 && n.properties[2].Value.ToString().Contains("Mesh")).ToList();
            foreach (var meshNode in meshNodes)
            {
                long meshId = (long)meshNode.properties[0].Value;
                var connToModel = conns.FirstOrDefault(c => c.type == "OO" && c.child == meshId && objectsById.ContainsKey(c.parent) && objectsById[c.parent].Name == "Model");
                if (connToModel != default)
                {
                    long modelId = connToModel.parent;
                    var modelNode = objectsById[modelId];
                    var meshData = ParseMesh(meshNode, modelNode, sourceToTarget, signs, modelScale);
                    var deformers = conns.Where(c => c.type == "OO" && c.parent == meshId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "Deformer").Select(c => objectsById[c.child]).ToList();
                    if (deformers.Any())
                    {
                        ParseSkin(meshData, deformers, objectsById, conns, boneIndexById, sourceToTarget, signs, modelScale, P4, invP4);
                        model.HasSkin = true;
                    }
                    ParseMaterials(meshData, modelId, conns, objectsById, forest);
                    model.Meshes.Add(meshData);
                }
            }
            FBXParserBase.Log($"FBXMeshParser: Parsed {model.Meshes.Count} meshes");
        }
        private static MeshData ParseMesh(BaseNode meshNode, BaseNode modelNode, int[] sourceToTarget, int[] signs, float modelScale)
        {
            var meshData = new MeshData();
            var vertsD = ParseVertices(meshNode);
            var pviArray = ParsePolygonVertexIndices(meshNode);
            if (vertsD == null || vertsD.Length % 3 != 0 || pviArray == null)
            {
                FBXParserBase.Log("Invalid vertices or indices data");
                return meshData;
            }
            var (norms, normIdx, normMapping, normRef) = ParseNormals(meshNode);
            var (uvs, uvIdx, uvMapping, uvRef) = ParseUVs(meshNode);
            var (matIndices, matMapping) = ParseMaterials(meshNode);
            var geoMat = ParseGeometricTransform((long)meshNode.properties[0].Value, new List<(string, long, long, string)>(), new Dictionary<long, BaseNode>(), sourceToTarget, signs, modelScale);
            int numVerts = vertsD.Length / 3;
            var perVertBones = new List<List<(int, float)>>(Enumerable.Repeat(new List<(int, float)>(), numVerts));
            // Stub for skin
            var (expandedVertices, newIndices) = BuildExpandedVerticesAndIndices(pviArray, vertsD, sourceToTarget, signs, modelScale, norms, normIdx, normMapping, normRef, uvs, uvIdx, uvMapping, uvRef, matIndices, matMapping, perVertBones, numVerts);
            meshData.Vertices = expandedVertices;
            meshData.Indices = newIndices;
            return meshData;
        }
        private static double[] ParseVertices(BaseNode geom)
        {
            var vertsNode = geom.children.FirstOrDefault(c => c.Name == "Vertices");
            double[] vertsD = null;
            if (vertsNode != null && vertsNode.properties.Count > 0)
            {
                var prop = vertsNode.properties[0];
                if (prop.TypeCode == 'd')
                {
                    vertsD = (double[])prop.Value;
                }
                else if (prop.TypeCode == 'f')
                {
                    float[] fvals = (float[])prop.Value;
                    vertsD = new double[fvals.Length];
                    for (int vi = 0; vi < fvals.Length; vi++) vertsD[vi] = fvals[vi];
                }
            }
            return vertsD;
        }
        private static int[] ParsePolygonVertexIndices(BaseNode geom)
        {
            var indicesNode = geom.children.FirstOrDefault(c => c.Name == "PolygonVertexIndex");
            int[] pviArray = null;
            if (indicesNode != null && indicesNode.properties.Count > 0 && indicesNode.properties[0].TypeCode == 'i')
            {
                pviArray = (int[])indicesNode.properties[0].Value;
            }
            return pviArray;
        }
        private static (double[] norms, int[] normIdx, string normMapping, string normRef) ParseNormals(BaseNode geom)
        {
            var normNode = geom.children.FirstOrDefault(c => c.Name == "LayerElementNormal");
            if (normNode == null) return (null, null, "", "");
            var mappingNode = normNode.children.FirstOrDefault(c => c.Name == "MappingInformationType");
            string normMapping = mappingNode?.properties.Count > 0 ? mappingNode.properties[0].Value.ToString() : "";
            var refNode = normNode.children.FirstOrDefault(c => c.Name == "ReferenceInformationType");
            string normRef = refNode?.properties.Count > 0 ? refNode.properties[0].Value.ToString() : "";
            var normsNode = normNode.children.FirstOrDefault(c => c.Name == "Normals");
            double[] norms = null;
            if (normsNode != null && normsNode.properties.Count > 0)
            {
                var prop = normsNode.properties[0];
                if (prop.TypeCode == 'd')
                {
                    norms = (double[])prop.Value;
                }
                else if (prop.TypeCode == 'f')
                {
                    float[] fvals = (float[])prop.Value;
                    norms = new double[fvals.Length];
                    for (int vi = 0; vi < fvals.Length; vi++) norms[vi] = fvals[vi];
                }
            }
            var normIdxNode = normNode.children.FirstOrDefault(c => c.Name == "NormalsIndex");
            int[] normIdx = null;
            if (normIdxNode != null && normIdxNode.properties.Count > 0 && normIdxNode.properties[0].TypeCode == 'i')
            {
                normIdx = (int[])normIdxNode.properties[0].Value;
            }
            return (norms, normIdx, normMapping, normRef);
        }
        private static (double[] uvs, int[] uvIdx, string uvMapping, string uvRef) ParseUVs(BaseNode geom)
        {
            var uvNode = geom.children.FirstOrDefault(c => c.Name == "LayerElementUV");
            if (uvNode == null) return (null, null, "", "");
            var mappingNode = uvNode.children.FirstOrDefault(c => c.Name == "MappingInformationType");
            string uvMapping = mappingNode?.properties.Count > 0 ? mappingNode.properties[0].Value.ToString() : "";
            var refNode = uvNode.children.FirstOrDefault(c => c.Name == "ReferenceInformationType");
            string uvRef = refNode?.properties.Count > 0 ? refNode.properties[0].Value.ToString() : "";
            var uvsNode = uvNode.children.FirstOrDefault(c => c.Name == "UV");
            double[] uvs = null;
            if (uvsNode != null && uvsNode.properties.Count > 0)
            {
                var prop = uvsNode.properties[0];
                if (prop.TypeCode == 'd')
                {
                    uvs = (double[])prop.Value;
                }
                else if (prop.TypeCode == 'f')
                {
                    float[] fvals = (float[])prop.Value;
                    uvs = new double[fvals.Length];
                    for (int vi = 0; vi < fvals.Length; vi++) uvs[vi] = fvals[vi];
                }
            }
            var uvIdxNode = uvNode.children.FirstOrDefault(c => c.Name == "UVIndex");
            int[] uvIdx = null;
            if (uvIdxNode != null && uvIdxNode.properties.Count > 0 && uvIdxNode.properties[0].TypeCode == 'i')
            {
                uvIdx = (int[])uvIdxNode.properties[0].Value;
            }
            return (uvs, uvIdx, uvMapping, uvRef);
        }
        private static (int[] matIndices, string matMapping) ParseMaterials(BaseNode geom)
        {
            var matNode = geom.children.FirstOrDefault(c => c.Name == "LayerElementMaterial");
            if (matNode == null) return (null, "");
            var mappingNode = matNode.children.FirstOrDefault(c => c.Name == "MappingInformationType");
            string matMapping = mappingNode?.properties.Count > 0 ? mappingNode.properties[0].Value.ToString() : "";
            var refNode = matNode.children.FirstOrDefault(c => c.Name == "ReferenceInformationType");
            string matRef = refNode?.properties.Count > 0 ? refNode.properties[0].Value.ToString() : "";
            var matsNode = matNode.children.FirstOrDefault(c => c.Name == "Materials");
            int[] matIndices = null;
            if (matsNode != null && matsNode.properties.Count > 0 && matsNode.properties[0].TypeCode == 'i')
            {
                matIndices = (int[])matsNode.properties[0].Value;
            }
            return (matIndices, matMapping);
        }
        private static Matrix4x4 ParseGeometricTransform(long geomId, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, int[] sourceToTarget, int[] signs, float modelScale)
        {
            // Stub, return identity
            return Matrix4x4.Identity;
        }
        private static void ParseSkin(MeshData meshData, List<BaseNode> deformers, Dictionary<long, BaseNode> objectsById, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, int> boneIndexById, int[] sourceToTarget, int[] signs, float modelScale, Matrix4x4 P4, Matrix4x4 invP4)
        {
            // Stub for skin parsing
        }
        private static void ParseMaterials(MeshData meshData, long modelId, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, FBXFileForest forest)
        {
            var matConns = conns.Where(c => c.type == "OO" && c.parent == modelId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "Material").ToList();
            matConns.Sort((a, b) => a.child.CompareTo(b.child));
            foreach (var conn in matConns)
            {
                long matId = conn.child;
                var matNode = objectsById[matId];
                string name = matNode.properties.Count > 2 ? matNode.properties[2].Value.ToString() : "Material";
                var material = new Material { Name = name };
                var texConns = conns.Where(c => c.type.StartsWith("OP") && c.parent == matId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "Texture").ToList();
                foreach (var tconn in texConns)
                {
                    string prop = tconn.prop ?? "DiffuseColor";
                    long texId = tconn.child;
                    var texNode = objectsById[texId];
                    string path = "";
                    var relFileNode = texNode.children.FirstOrDefault(n => n.Name == "RelativeFilename");
                    if (relFileNode != null && relFileNode.properties.Count > 0)
                    {
                        path = relFileNode.properties[0].Value.ToString();
                    }
                    else
                    {
                        var fileNode = texNode.children.FirstOrDefault(n => n.Name == "FileName");
                        if (fileNode != null && fileNode.properties.Count > 0)
                        {
                            path = fileNode.properties[0].Value.ToString();
                        }
                    }
                    path = path.Replace("\\", "/");
                    var texInfo = new TextureInfo { Path = path };
                    var props70 = texNode.children.FirstOrDefault(n => n.Name == "Properties70");
                    if (props70 != null)
                    {
                        var wrapUP = props70.children.FirstOrDefault(p => p.Name == "P" && (p.properties[0].Value.ToString().Contains("WrapU") || p.properties[0].Value.ToString().Contains("UWarp")));
                        if (wrapUP != null && wrapUP.properties.Count > 4)
                        {
                            texInfo.WrapU = Convert.ToInt32(wrapUP.properties[4].Value);
                        }
                        var wrapVP = props70.children.FirstOrDefault(p => p.Name == "P" && (p.properties[0].Value.ToString().Contains("WrapV") || p.properties[0].Value.ToString().Contains("VWarp")));
                        if (wrapVP != null && wrapVP.properties.Count > 4)
                        {
                            texInfo.WrapV = Convert.ToInt32(wrapVP.properties[4].Value);
                        }
                    }
                    material.Textures[prop] = texInfo;
                    var videoConn = conns.FirstOrDefault(c => c.type == "OO" && c.parent == texId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "Video");
                    if (videoConn != default)
                    {
                        long videoId = videoConn.child;
                        var videoNode = objectsById[videoId];
                        var contentNode = videoNode.children.FirstOrDefault(n => n.Name == "Content");
                        if (contentNode != null && contentNode.properties.Count > 0 && contentNode.properties[0].TypeCode == 'R')
                        {
                            byte[] data = (byte[])contentNode.properties[0].Value;
                            string embName = System.IO.Path.GetFileName(path);
                            forest.EmbeddedTextures.Add((embName, data));
                            texInfo.Path = "embedded:" + embName;
                        }
                    }
                }
                meshData.Materials.Add(material);
            }
            FBXParserBase.Log($"FBXMeshParser: Parsed {meshData.Materials.Count} materials for model {modelId}");
        }
        private static (List<FBXVertex> expandedVertices, List<uint> newIndices) BuildExpandedVerticesAndIndices(int[] pviArray, double[] vertsD, int[] sourceToTarget, int[] signs, float modelScale, double[] norms, int[] normIdx, string normMapping, string normRef, double[] uvs, int[] uvIdx, string uvMapping, string uvRef, int[] matIndices, string matMapping, List<List<(int boneIdx, float weight)>> perVertBones, int numVerts)
        {
            int productSigns = signs[0] * signs[1] * signs[2];
            int signPerm = 1;
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                    if (sourceToTarget[i] > sourceToTarget[j]) signPerm = -signPerm;
            int overallSign = productSigns * signPerm;
            bool flipWinding = overallSign < 0;
            bool flipNormal = overallSign < 0;
            List<FBXVertex> expandedVertices = new List<FBXVertex>();
            List<uint> newIndices = new List<uint>();
            int currentIndex = 0;
            int polyIndex = 0;
            List<int> tempPoly = new List<int>();
            List<int> tempPolyPvIdx = new List<int>();
            for (int i = 0; i < pviArray.Length; i++)
            {
                int pv = pviArray[i];
                bool end = pv < 0;
                int vId = end ? ~(pv) : pv;
                if (vId < 0 || vId >= numVerts)
                {
                    FBXParserBase.Log($"Invalid vId {vId} at i={i}, skipping polygon");
                    tempPoly.Clear();
                    tempPolyPvIdx.Clear();
                    continue;
                }
                tempPoly.Add(vId);
                tempPolyPvIdx.Add(i);
                if (end)
                {
                    int matId = GetMatId(matMapping, matIndices, polyIndex);
                    // Triangulate polygon
                    for (int j = 1; j < tempPoly.Count - 1; j++)
                    {
                        if (flipWinding)
                        {
                            newIndices.Add((uint)currentIndex);
                            newIndices.Add((uint)(currentIndex + j + 1));
                            newIndices.Add((uint)(currentIndex + j));
                        }
                        else
                        {
                            newIndices.Add((uint)currentIndex);
                            newIndices.Add((uint)(currentIndex + j));
                            newIndices.Add((uint)(currentIndex + j + 1));
                        }
                    }
                    // Add vertices for the polygon
                    for (int k = 0; k < tempPoly.Count; k++)
                    {
                        int vertIdx = tempPoly[k];
                        float x = (float)vertsD[vertIdx * 3];
                        float y = (float)vertsD[vertIdx * 3 + 1];
                        float z = (float)vertsD[vertIdx * 3 + 2];
                        Vector3 pos = FBXCoordinateUtils.RemapVector(new Vector3(x, y, z), sourceToTarget, signs) * modelScale;
                        int pvIdx = tempPolyPvIdx[k];
                        Vector3 normal = GetNormal(norms, normIdx, normMapping, normRef, vertIdx, pvIdx, sourceToTarget, signs);
                        if (flipNormal) normal = -normal;
                        Vector2 uv = GetUV(uvs, uvIdx, uvMapping, uvRef, vertIdx, pvIdx);
                        // Stub for other attributes
                        expandedVertices.Add(new FBXVertex { Position = pos, Normal = normal, TexCoord = new Vector2(uv.X, 1f - uv.Y), MatIdx = matId });
                    }
                    currentIndex += tempPoly.Count;
                    tempPoly.Clear();
                    tempPolyPvIdx.Clear();
                    polyIndex++;
                }
            }
            return (expandedVertices, newIndices);
        }
        private static int GetMatId(string matMapping, int[] matIndices, int polyIndex)
        {
            if (matIndices == null) return 0;
            if (matMapping == "AllSame") return matIndices.Length > 0 ? matIndices[0] : 0;
            if (matMapping == "ByPolygon" || matMapping == "ByPolygone") return polyIndex < matIndices.Length ? matIndices[polyIndex] : 0;
            FBXParserBase.Log($"Unknown matMapping {matMapping}");
            return 0;
        }
        private static Vector3 GetNormal(double[] norms, int[] normIdx, string mapping, string refe, int vertIdx, int pvIdx, int[] sourceToTarget, int[] signs)
        {
            if (norms == null) return Vector3.Zero;
            int idx;
            if (mapping == "ByVertice" || mapping == "ByVertex")
            {
                idx = vertIdx;
            }
            else if (mapping == "ByPolygonVertex")
            {
                idx = pvIdx;
            }
            else
            {
                return Vector3.Zero;
            }
            if (refe == "IndexToDirect" && normIdx != null)
            {
                idx = normIdx[idx];
            }
            float nx = (float)norms[idx * 3];
            float ny = (float)norms[idx * 3 + 1];
            float nz = (float)norms[idx * 3 + 2];
            Vector3 normal = FBXCoordinateUtils.RemapVector(new Vector3(nx, ny, nz), sourceToTarget, signs);
            if (normal.LengthSquared() > 0)
                normal = Vector3.Normalize(normal);
            return normal;
        }
        private static Vector2 GetUV(double[] uvs, int[] uvIdx, string mapping, string refe, int vertIdx, int pvIdx)
        {
            if (uvs == null) return Vector2.Zero;
            int idx;
            if (mapping == "ByVertice" || mapping == "ByVertex")
            {
                idx = vertIdx;
            }
            else if (mapping == "ByPolygonVertex")
            {
                idx = pvIdx;
            }
            else
            {
                return Vector2.Zero;
            }
            if (refe == "IndexToDirect" && uvIdx != null)
            {
                idx = uvIdx[idx];
            }
            float u = (float)uvs[idx * 2];
            float v = (float)uvs[idx * 2 + 1];
            return new Vector2(u, v);
        }
    }
}