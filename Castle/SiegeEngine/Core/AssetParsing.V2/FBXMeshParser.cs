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
                        ParseSkin(meshData, deformers, objectsById, conns, boneIndexById, sourceToTarget, signs, modelScale, P4, invP4, model);
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
            int numVerts = vertsD.Length / 3;
            var perVertBones = new List<List<(int, float)>>(Enumerable.Repeat(new List<(int, float)>(), numVerts));
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
        private static Matrix4x4 ParseGeometricTransform(long geomId, List<(string, long, long, string)> conns, Dictionary<long, BaseNode> objectsById, int[] sourceToTarget, int[] signs, float modelScale)
        {
            // Stub, return identity
            return Matrix4x4.Identity;
        }
        private static void ParseSkin(MeshData meshData, List<BaseNode> deformers, Dictionary<long, BaseNode> objectsById, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, int> boneIndexById, int[] sourceToTarget, int[] signs, float modelScale, Matrix4x4 P4, Matrix4x4 invP4, FBXModel model)
        {
            int numVerts = meshData.Vertices.Count;
            var perVertBones = Enumerable.Range(0, numVerts).Select(_ => new List<(int, float)>()).ToList();
            int totalClusters = 0;
            long totalIndexes = 0;
            long totalWeights = 0;
            foreach (var deformer in deformers.Where(d => d.properties.Count > 2 && d.properties[2].Value.ToString() == "Skin"))
            {
                var clusterConns = conns.Where(c => c.type == "OO" && c.parent == (long)deformer.properties[0].Value && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "Deformer" && objectsById[c.child].properties.Count > 2 && objectsById[c.child].properties[2].Value.ToString() == "Cluster").ToList();
                foreach (var clusterConn in clusterConns)
                {
                    var clusterNode = objectsById[clusterConn.child];
                    var boneConn = conns.FirstOrDefault(c => c.type == "OO" && (c.child == clusterConn.child || c.parent == clusterConn.child) && objectsById.ContainsKey(c.parent == clusterConn.child ? c.child : c.parent) && objectsById[c.parent == clusterConn.child ? c.child : c.parent].Name == "Model");
                    if (boneConn.type == null) continue;
                    long boneId = (boneConn.child == clusterConn.child) ? boneConn.parent : boneConn.child;
                    if (!boneIndexById.TryGetValue(boneId, out int boneIdx)) continue;
                    var indexesNode = clusterNode.children.FirstOrDefault(c => c.Name == "Indexes");
                    int[] indexes = null;
                    if (indexesNode != null)
                    {
                        var prop = indexesNode.properties[0];
                        char typeCode = prop.TypeCode;
                        if (typeCode == 'i')
                        {
                            indexes = (int[])prop.Value;
                        }
                        else if (typeCode == 'R')
                        {
                            byte[] raw = (byte[])prop.Value;
                            indexes = new int[raw.Length / 4];
                            Buffer.BlockCopy(raw, 0, indexes, 0, raw.Length);
                        }
                        else
                        {
                            FBXParserBase.Log($"Unexpected type for Indexes: {typeCode}");
                            indexes = Array.Empty<int>();
                        }
                    }
                    else
                    {
                        indexes = Array.Empty<int>();
                    }
                    var weightsNode = clusterNode.children.FirstOrDefault(c => c.Name == "Weights");
                    double[] weights = null;
                    if (weightsNode != null)
                    {
                        var prop = weightsNode.properties[0];
                        char typeCode = prop.TypeCode;
                        if (typeCode == 'd')
                        {
                            weights = (double[])prop.Value;
                        }
                        else if (typeCode == 'f')
                        {
                            float[] fvals = (float[])prop.Value;
                            weights = new double[fvals.Length];
                            for (int wi = 0; wi < fvals.Length; wi++) weights[wi] = fvals[wi];
                        }
                        else if (typeCode == 'R')
                        {
                            byte[] raw = (byte[])prop.Value;
                            weights = new double[raw.Length / 8];
                            Buffer.BlockCopy(raw, 0, weights, 0, raw.Length);
                        }
                        else
                        {
                            FBXParserBase.Log($"Unexpected type for Weights: {typeCode}");
                            weights = Array.Empty<double>();
                        }
                    }
                    else
                    {
                        weights = Array.Empty<double>();
                    }
                    var transformLinkNode = clusterNode.children.FirstOrDefault(c => c.Name == "TransformLink");
                    double[] tl = transformLinkNode != null && transformLinkNode.properties.Count > 0 && transformLinkNode.properties[0].TypeCode == 'd' ? (double[])transformLinkNode.properties[0].Value : null;
                    var transformNode = clusterNode.children.FirstOrDefault(c => c.Name == "Transform");
                    double[] tr = transformNode != null && transformNode.properties.Count > 0 && transformNode.properties[0].TypeCode == 'd' ? (double[])transformNode.properties[0].Value : null;
                    Matrix4x4 tlMat = tl != null && tl.Length == 16 ? CreateMatrixFromArray(tl) : Matrix4x4.Identity;
                    Matrix4x4 tMat = tr != null && tr.Length == 16 ? CreateMatrixFromArray(tr) : Matrix4x4.Identity;
                    tlMat = FBXCoordinateUtils.RemapMatrix(tlMat, sourceToTarget, signs);
                    tlMat = new Matrix4x4(tlMat.M11, tlMat.M12, tlMat.M13, tlMat.M14,
                                          tlMat.M21, tlMat.M22, tlMat.M23, tlMat.M24,
                                          tlMat.M31, tlMat.M32, tlMat.M33, tlMat.M34,
                                          tlMat.M41 * modelScale, tlMat.M42 * modelScale, tlMat.M43 * modelScale, tlMat.M44);
                    tMat = FBXCoordinateUtils.RemapMatrix(tMat, sourceToTarget, signs);
                    tMat = new Matrix4x4(tMat.M11, tMat.M12, tMat.M13, tMat.M14,
                                         tMat.M21, tMat.M22, tMat.M23, tMat.M24,
                                         tMat.M31, tMat.M32, tMat.M33, tMat.M34,
                                         tMat.M41 * modelScale, tMat.M42 * modelScale, tMat.M43 * modelScale, tMat.M44);
                    Matrix4x4 geom = model.Skeleton.Bones[boneIdx].GeometricTransform;
                    if (Matrix4x4.Invert(tlMat, out Matrix4x4 invTl))
                    {
                        Matrix4x4 invBind = invTl * tMat * geom;
                        model.Skeleton.Bones[boneIdx].BindPose = invBind;
                    }
                    else
                    {
                        FBXParserBase.Log($"Failed to invert tlMat for bone {boneIdx}, using identity");
                        model.Skeleton.Bones[boneIdx].BindPose = Matrix4x4.Identity;
                    }
                    for (int i = 0; i < Math.Min(indexes?.Length ?? 0, weights?.Length ?? 0); i++)
                    {
                        int vertIdx = indexes[i];
                        float w = (float)weights[i];
                        if (w > 0 && vertIdx >= 0 && vertIdx < numVerts)
                        {
                            perVertBones[vertIdx].Add((boneIdx, w));
                        }
                    }
                    totalClusters++;
                    totalIndexes += indexes?.Length ?? 0;
                    totalWeights += weights?.Length ?? 0;
                }
            }
            if (totalClusters > 0)
            {
                FBXParserBase.Log($"Total clusters parsed: {totalClusters}, Total indexes: {totalIndexes}, Total weights: {totalWeights}");
                NormalizeWeights(perVertBones);
                AssignBoneDataToVertices(meshData, perVertBones);
            }
        }
        private static void NormalizeWeights(List<List<(int, float)>> perVertBones)
        {
            foreach (var bw in perVertBones)
            {
                if (bw.Count > 4)
                {
                    bw.Sort((a, b) => b.Item2.CompareTo(a.Item2));
                    bw.RemoveRange(4, bw.Count - 4);
                }
                float sum = bw.Sum(x => x.Item2);
                if (sum > 0)
                {
                    for (int j = 0; j < bw.Count; j++)
                    {
                        bw[j] = (bw[j].Item1, bw[j].Item2 / sum);
                    }
                }
            }
        }
        private static void AssignBoneDataToVertices(MeshData meshData, List<List<(int, float)>> perVertBones)
        {
            int weightedCount = 0;
            for (int v = 0; v < meshData.Vertices.Count; v++)
            {
                var vertex = meshData.Vertices[v];
                var bw = perVertBones[v];
                vertex.BoneIDs = new Vector4(bw.Count > 0 ? bw[0].Item1 : -1, bw.Count > 1 ? bw[1].Item1 : -1, bw.Count > 2 ? bw[2].Item1 : -1, bw.Count > 3 ? bw[3].Item1 : -1);
                vertex.Weights = new Vector4(bw.Count > 0 ? bw[0].Item2 : 0, bw.Count > 1 ? bw[1].Item2 : 0, bw.Count > 2 ? bw[2].Item2 : 0, bw.Count > 3 ? bw[3].Item2 : 0);
                if (vertex.Weights.X > 0 || vertex.Weights.Y > 0 || vertex.Weights.Z > 0 || vertex.Weights.W > 0) weightedCount++;
                meshData.Vertices[v] = vertex;
            }
            FBXParserBase.Log($"FBXMeshParser: {weightedCount} weighted vertices out of {meshData.Vertices.Count}");
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
        private static (List<FBXVertex> expandedVertices, List<uint> newIndices) BuildExpandedVerticesAndIndices(int[] pviArray, double[] vertsD, int[] sourceToTarget, int[] signs, float modelScale, double[] norms, int[] normIdx, string normMapping, string normRef, double[] uvs, int[] uvIdx, string uvMapping, string uvRef, int[] matIndices, string matMapping, List<List<(int, float)>> perVertBones, int numVerts)
        {
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
                        Vector3 pos = FBXCoordinateUtils.RemapVector(new Vector3(x, y, z), sourceToTarget, signs) * modelScale;
                        int pvIdx = tempPolyPvIdx[k];
                        Vector3 normal = GetNormal(norms, normIdx, normMapping, normRef, vertIdx, pvIdx, sourceToTarget, signs);
                        Vector2 uv = GetUV(uvs, uvIdx, uvMapping, uvRef, vertIdx, pvIdx);
                        var bw = perVertBones[vertIdx];
                        Vector4 boneIDs = new Vector4(bw.Count > 0 ? bw[0].Item1 : -1, bw.Count > 1 ? bw[1].Item1 : -1, bw.Count > 2 ? bw[2].Item1 : -1, bw.Count > 3 ? bw[3].Item1 : -1);
                        Vector4 weights = new Vector4(bw.Count > 0 ? bw[0].Item2 : 0, bw.Count > 1 ? bw[1].Item2 : 0, bw.Count > 2 ? bw[2].Item2 : 0, bw.Count > 3 ? bw[3].Item2 : 0);
                        expandedVertices.Add(new FBXVertex { Position = pos, Normal = normal, TexCoord = new Vector2(uv.X, 1f - uv.Y), Tangent = Vector3.Zero, BoneIDs = boneIDs, Weights = weights, MatIdx = matId });
                    }
                    currentIndex += tempPoly.Count;
                    tempPoly.Clear();
                    tempPolyPvIdx.Clear();
                    polyIndex++;
                }
            }
            //// Debug logs for first 3 vertices and first triangle indices
            //if (expandedVertices.Count >= 3)
            //{
            //    for (int dbg = 0; dbg < 3; dbg++)
            //    {
            //        var v = expandedVertices[dbg];
            //        FBXParserBase.Log($"Debug Vertex {dbg}: Pos=({v.Position.X:F3},{v.Position.Y:F3},{v.Position.Z:F3}), Normal=({v.Normal.X:F3},{v.Normal.Y:F3},{v.Normal.Z:F3}), UV=({v.TexCoord.X:F3},{v.TexCoord.Y:F3}), MatIdx={v.MatIdx}");
            //    }
            //}
            //if (newIndices.Count >= 3)
            //{
            //    FBXParserBase.Log($"Debug First Triangle Indices: {newIndices[0]}, {newIndices[1]}, {newIndices[2]}");
            //}
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
        public static Matrix4x4 CreateMatrixFromArray(double[] vals)
        {
            return new Matrix4x4(
                (float)vals[0], (float)vals[4], (float)vals[8], (float)vals[12],
                (float)vals[1], (float)vals[5], (float)vals[9], (float)vals[13],
                (float)vals[2], (float)vals[6], (float)vals[10], (float)vals[14],
                (float)vals[3], (float)vals[7], (float)vals[11], (float)vals[15]);
        }
        public static void PrintMatrix(Matrix4x4 m)
        {
            FBXParserBase.Log($"({m.M11:F4}, {m.M12:F4}, {m.M13:F4}, {m.M14:F4})");
            FBXParserBase.Log($"({m.M21:F4}, {m.M22:F4}, {m.M23:F4}, {m.M24:F4})");
            FBXParserBase.Log($"({m.M31:F4}, {m.M32:F4}, {m.M33:F4}, {m.M34:F4})");
            FBXParserBase.Log($"({m.M41:F4}, {m.M42:F4}, {m.M43:F4}, {m.M44:F4})");
        }
    }
}