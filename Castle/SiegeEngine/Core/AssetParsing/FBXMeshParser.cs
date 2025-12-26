// Folder: SiegeEngine.Core
// File: AssetParsing/FBXMeshParser.cs
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing
{
    public static class FBXMeshParser
    {
        public static void ParseMeshes(FBXModel model, BaseNode objectsNode, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, int[] sourceToTarget, int[] signs, float modelScale, bool reverseWinding, Dictionary<long, int> boneIndexById, Matrix4x4 rootRot, List<int> rootIndices, Matrix4x4 P4, Matrix4x4 invP4, FBXFileForest forest)
        {
            var geomNodes = objectsNode.children.Where(n => n.Name == "Geometry" && n.properties.Count >= 3 && (string)n.properties[2].Value == "Mesh").ToList();
            if (geomNodes.Count == 0 && !objectsNode.children.Any(n => n.Name == "AnimationStack"))
            {
                Console.WriteLine("BuildModelFromForest: No Geometry::Mesh or AnimationStack nodes found");
                return;
            }
            foreach (var geom in geomNodes)
            {
                long geomId = (long)geom.properties[0].Value;
                var vertsD = ParseVertices(geom);
                var pviArray = ParsePolygonVertexIndices(geom);
                if (vertsD == null || pviArray == null)
                {
                    Console.WriteLine($"BuildModelFromForest: Skipping invalid mesh");
                    continue;
                }

                var (norms, normIdx, normMapping, normRef) = ParseNormals(geom);
                var (uvs, uvIdx, uvMapping, uvRef) = ParseUVs(geom);
                var (matIndices, matMapping) = ParseMaterials(geom);

                var geoMat = ParseGeometricTransform(geomId, conns, objectsById, sourceToTarget, signs, modelScale);

                int numVerts = vertsD.Length / 3;
                var perVertBones = ParseSkin(geomId, conns, objectsById, boneIndexById, model, rootRot, rootIndices, P4, invP4, modelScale, numVerts);

                NormalizeWeights(perVertBones);

                var (expandedVertices, newIndices) = BuildExpandedVerticesAndIndices(pviArray, vertsD, geoMat, sourceToTarget, signs, modelScale, norms, normIdx, normMapping, normRef, uvs, uvIdx, uvMapping, uvRef, matIndices, matMapping, perVertBones, reverseWinding, numVerts);

                MeshData mesh = new MeshData
                {
                    Vertices = expandedVertices,
                    Indices = newIndices,
                    Materials = ExtractMaterials(geomId, objectsNode, conns, objectsById, forest)
                };

                mesh.Bounds = CalculateBounds(expandedVertices);

                model.Meshes.Add(mesh);
            }
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
                if (normalsNode != null)
                {
                    var prop = normalsNode.properties[0];
                    if (prop.TypeCode == 'd')
                    {
                        norms = (double[])prop.Value;
                    }
                    else if (prop.TypeCode == 'f')
                    {
                        float[] fvals = (float[])prop.Value;
                        norms = new double[fvals.Length];
                        for (int ni = 0; ni < fvals.Length; ni++) norms[ni] = fvals[ni];
                    }
                }
                var normalsIndexNode = layerNorm.children.FirstOrDefault(c => c.Name == "NormalsIndex");
                if (normalsIndexNode != null && normalsIndexNode.properties[0].TypeCode == 'i')
                {
                    normIdx = (int[])normalsIndexNode.properties[0].Value;
                }
            }
            return (norms, normIdx, normMapping, normRef);
        }

        private static (double[] uvs, int[] uvIdx, string uvMapping, string uvRef) ParseUVs(BaseNode geom)
        {
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
                if (uvNode != null)
                {
                    var prop = uvNode.properties[0];
                    if (prop.TypeCode == 'd')
                    {
                        uvs = (double[])prop.Value;
                    }
                    else if (prop.TypeCode == 'f')
                    {
                        float[] fvals = (float[])prop.Value;
                        uvs = new double[fvals.Length];
                        for (int ui = 0; ui < fvals.Length; ui++) uvs[ui] = fvals[ui];
                    }
                }
                var uvIndexNode = layerUV.children.FirstOrDefault(c => c.Name == "UVIndex");
                if (uvIndexNode != null && uvIndexNode.properties[0].TypeCode == 'i')
                {
                    uvIdx = (int[])uvIndexNode.properties[0].Value;
                }
            }
            return (uvs, uvIdx, uvMapping, uvRef);
        }

        private static (int[] matIndices, string matMapping) ParseMaterials(BaseNode geom)
        {
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
            return (matIndices, matMapping);
        }

        private static Matrix4x4 ParseGeometricTransform(long geomId, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, int[] sourceToTarget, int[] signs, float modelScale)
        {
            Matrix4x4 geoMat = Matrix4x4.Identity;
            var modelConnsGeom = conns.Where(c => c.type == "OO" && c.child == geomId).ToList();
            if (modelConnsGeom.Count > 0)
            {
                long modelId = modelConnsGeom[0].parent;
                var modelNode = objectsById[modelId];
                Vector3 geoT = Vector3.Zero;
                Vector3 geoR = Vector3.Zero;
                Vector3 geoS = Vector3.One;
                var props70 = modelNode.children.FirstOrDefault(c => c.Name == "Properties70");
                if (props70 != null)
                {
                    foreach (var p in props70.children)
                    {
                        if (p.Name == "P" && p.properties.Count >= 7)
                        {
                            string pname = (string)p.properties[0].Value;
                            if (pname == "GeometricTranslation")
                            {
                                float gtx = Convert.ToSingle(p.properties[4].Value);
                                float gty = Convert.ToSingle(p.properties[5].Value);
                                float gtz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 gt_source = new Vector3(gtx, gty, gtz);
                                geoT = FBXCoordinateUtils.RemapVector(gt_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "GeometricRotation")
                            {
                                float grx = Convert.ToSingle(p.properties[4].Value);
                                float gry = Convert.ToSingle(p.properties[5].Value);
                                float grz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 gr_source = new Vector3(grx, gry, grz);
                                geoR = FBXCoordinateUtils.RemapRotation(gr_source, sourceToTarget, signs);
                            }
                            else if (pname == "GeometricScaling")
                            {
                                float gsx = Convert.ToSingle(p.properties[4].Value);
                                float gsy = Convert.ToSingle(p.properties[5].Value);
                                float gsz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 gs_source = new Vector3(gsx, gsy, gsz);
                                geoS = FBXCoordinateUtils.RemapScale(gs_source, sourceToTarget, signs);
                            }
                        }
                    }
                }
                float rx = geoR.X * MathF.PI / 180f;
                float ry = geoR.Y * MathF.PI / 180f;
                float rz = geoR.Z * MathF.PI / 180f;
                Matrix4x4 mx = Matrix4x4.CreateRotationX(rx);
                Matrix4x4 my = Matrix4x4.CreateRotationY(ry);
                Matrix4x4 mz = Matrix4x4.CreateRotationZ(rz);
                Matrix4x4 R = mx * my * mz;
                Matrix4x4 S = Matrix4x4.CreateScale(geoS);
                Matrix4x4 T = Matrix4x4.CreateTranslation(geoT);
                geoMat = S * R * T;
            }
            return geoMat;
        }

        private static List<List<(int boneIdx, float weight)>> ParseSkin(long geomId, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, Dictionary<long, int> boneIndexById, FBXModel model, Matrix4x4 rootRot, List<int> rootIndices, Matrix4x4 P4, Matrix4x4 invP4, float modelScale, int numVerts)
        {
            var perVertBones = Enumerable.Range(0, numVerts).Select(_ => new List<(int, float)>()).ToList();
            var skinConns = conns.Where(c => c.type == "OO" && c.parent == geomId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "Deformer" && (string)objectsById[c.child].properties[2].Value == "Skin").ToList();
            if (skinConns.Any())
            {
                model.HasSkin = true;
                int totalClusters = 0;
                long totalIndexes = 0;
                long totalWeights = 0;
                foreach (var skinConn in skinConns)
                {
                    var skinNode = objectsById[skinConn.child];
                    var clusterConns = conns.Where(c => c.type == "OO" && c.parent == skinConn.child && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "Deformer" && (string)objectsById[c.child].properties[2].Value == "Cluster").ToList();
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
                                indexes = FBXParserUtils.ParseRawArrayAsInt(prop.Value as byte[]);
                            }
                            else
                            {
                                Console.WriteLine($"Unexpected type for Indexes: {typeCode}");
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
                                weights = FBXParserUtils.ParseRawArrayAsDouble(prop.Value as byte[]);
                            }
                            else
                            {
                                Console.WriteLine($"Unexpected type for Weights: {typeCode}");
                                weights = Array.Empty<double>();
                            }
                        }
                        else
                        {
                            weights = Array.Empty<double>();
                        }
                        var transformLinkNode = clusterNode.children.FirstOrDefault(c => c.Name == "TransformLink");
                        double[] tl = transformLinkNode != null && transformLinkNode.properties[0].TypeCode == 'd' ? (double[])transformLinkNode.properties[0].Value : null;
                        var transformNode = clusterNode.children.FirstOrDefault(c => c.Name == "Transform");
                        double[] tr = transformNode != null && transformNode.properties[0].TypeCode == 'd' ? (double[])transformNode.properties[0].Value : null;
                        Matrix4x4 tlMat = Matrix4x4.Identity;
                        if (tl != null && tl.Length == 16)
                        {
                            tlMat = new Matrix4x4((float)tl[0], (float)tl[4], (float)tl[8], (float)tl[12],
                                                  (float)tl[1], (float)tl[5], (float)tl[9], (float)tl[13],
                                                  (float)tl[2], (float)tl[6], (float)tl[10], (float)tl[14],
                                                  (float)tl[3], (float)tl[7], (float)tl[11], (float)tl[15]);
                        }
                        Matrix4x4 tMat = Matrix4x4.Identity;
                        if (tr != null && tr.Length == 16)
                        {
                            tMat = new Matrix4x4((float)tr[0], (float)tr[4], (float)tr[8], (float)tr[12],
                                                 (float)tr[1], (float)tr[5], (float)tr[9], (float)tr[13],
                                                 (float)tr[2], (float)tr[6], (float)tr[10], (float)tr[14],
                                                 (float)tr[3], (float)tr[7], (float)tr[11], (float)tr[15]);
                        }
                        // Remap matrices
                        Matrix4x4 tl_remap = P4 * tlMat * invP4;
                        tl_remap = new Matrix4x4(tl_remap.M11, tl_remap.M12, tl_remap.M13, tl_remap.M14,
                                                 tl_remap.M21, tl_remap.M22, tl_remap.M23, tl_remap.M24,
                                                 tl_remap.M31, tl_remap.M32, tl_remap.M33, tl_remap.M34,
                                                 tl_remap.M41 * modelScale, tl_remap.M42 * modelScale, tl_remap.M43 * modelScale, tl_remap.M44);
                        Matrix4x4 t_remap = P4 * tMat * invP4;
                        t_remap = new Matrix4x4(t_remap.M11, t_remap.M12, t_remap.M13, t_remap.M14,
                                                t_remap.M21, t_remap.M22, t_remap.M23, t_remap.M24,
                                                t_remap.M31, t_remap.M32, t_remap.M33, t_remap.M34,
                                                t_remap.M41 * modelScale, t_remap.M42 * modelScale, t_remap.M43 * modelScale, t_remap.M44);
                        // Apply rootRot if root
                        bool isRoot = model.Skeleton.Bones[boneIdx].ParentIndex == -1;
                        if (isRoot)
                        {
                            tl_remap = rootRot * tl_remap;
                            t_remap = rootRot * t_remap;
                        }
                        // Compute invBind
                        if (Matrix4x4.Invert(tl_remap, out var invTl))
                        {
                            Matrix4x4 invBind = invTl * t_remap;
                            model.Skeleton.Bones[boneIdx].BindPose = invBind;
                        }
                        else
                        {
                            Console.WriteLine($"BuildModelFromForest: Failed to invert tl_remap for bone {boneIdx}, using identity");
                            model.Skeleton.Bones[boneIdx].BindPose = Matrix4x4.Identity;
                        }
                        for (int i = 0; i < Math.Min(indexes.Length, weights.Length); i++)
                        {
                            int vertIdx = indexes[i];
                            if (vertIdx < 0 || vertIdx >= numVerts)
                            {
                                Console.WriteLine($"BuildModelFromForest: Invalid vertIdx {vertIdx} in cluster, skipping");
                                continue;
                            }
                            float w = (float)weights[i];
                            if (w > 0)
                            {
                                perVertBones[vertIdx].Add((boneIdx, w));
                            }
                        }
                        totalClusters++;
                        totalIndexes += indexes.Length;
                        totalWeights += weights.Length;
                    }
                }
                Console.WriteLine($"Total clusters parsed: {totalClusters}, Total indexes: {totalIndexes}, Total weights: {totalWeights}");
            }
            return perVertBones;
        }

        private static void NormalizeWeights(List<List<(int boneIdx, float weight)>> perVertBones)
        {
            for (int v = 0; v < perVertBones.Count; v++)
            {
                var bw = perVertBones[v];
                if (bw.Count > 4)
                {
                    bw = bw.OrderByDescending(b => b.weight).Take(4).ToList();
                }
                float sumW = bw.Sum(b => b.weight);
                if (sumW > 0)
                {
                    for (int j = 0; j < bw.Count; j++)
                    {
                        bw[j] = (bw[j].boneIdx, bw[j].weight / sumW);
                    }
                }
                perVertBones[v] = bw.OrderByDescending(b => b.weight).ToList(); // Sort descending weight
            }
        }

        private static (List<FBXVertex> expandedVertices, List<uint> newIndices) BuildExpandedVerticesAndIndices(int[] pviArray, double[] vertsD, Matrix4x4 geoMat, int[] sourceToTarget, int[] signs, float modelScale, double[] norms, int[] normIdx, string normMapping, string normRef, double[] uvs, int[] uvIdx, string uvMapping, string uvRef, int[] matIndices, string matMapping, List<List<(int boneIdx, float weight)>> perVertBones, bool reverseWinding, int numVerts)
        {
            List<FBXVertex> expandedVertices = new List<FBXVertex>();
            List<uint> newIndices = new List<uint>();
            int currentIndex = 0;
            List<int> tempPoly = new List<int>();
            int polyIndex = 0;
            for (int i = 0; i < pviArray.Length; i++)
            {
                int pv = pviArray[i];
                bool end = pv < 0;
                int vId = end ? -pv - 1 : pv;
                if (vId < 0 || vId >= numVerts)
                {
                    Console.WriteLine($"BuildModelFromForest: Invalid vId {vId} at i={i}, skipping polygon");
                    tempPoly.Clear();
                    continue;
                }
                tempPoly.Add(vId);
                if (end)
                {
                    // Triangulate polygon
                    for (int j = 1; j < tempPoly.Count - 1; j++)
                    {
                        if (reverseWinding)
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
                        Vector3 pos_source = new Vector3(x, y, z);
                        pos_source = Vector3.Transform(pos_source, geoMat);
                        Vector3 pos = FBXCoordinateUtils.RemapVector(pos_source, sourceToTarget, signs) * modelScale;
                        // Normal
                        Vector3 normal_source = new Vector3(0f, 0f, 1f); // Default
                        if (norms != null)
                        {
                            int nIdx;
                            if (normMapping == "ByPolygonVertex")
                            {
                                if (normRef == "IndexToDirect" && normIdx != null)
                                {
                                    int polyVertIdx = i - tempPoly.Count + 1 + k;
                                    if (polyVertIdx < 0 || polyVertIdx >= normIdx.Length)
                                    {
                                        Console.WriteLine($"BuildModelFromForest: Invalid normIdx access at polyVertIdx={polyVertIdx}, normIdx.Length={normIdx.Length}, using default normal");
                                        nIdx = 0;
                                    }
                                    else
                                    {
                                        nIdx = normIdx[polyVertIdx];
                                    }
                                }
                                else // Direct
                                {
                                    nIdx = i - tempPoly.Count + 1 + k;
                                }
                            }
                            else // ByVertice
                            {
                                nIdx = vertIdx;
                            }
                            if (nIdx < 0 || nIdx * 3 >= norms.Length)
                            {
                                Console.WriteLine($"BuildModelFromForest: Invalid nIdx {nIdx}, norms.Length={norms.Length / 3}, using default normal");
                            }
                            else
                            {
                                float nx = (float)norms[nIdx * 3];
                                float ny = (float)norms[nIdx * 3 + 1];
                                float nz = (float)norms[nIdx * 3 + 2];
                                normal_source = new Vector3(nx, ny, nz);
                            }
                        }
                        normal_source = Vector3.TransformNormal(normal_source, geoMat);
                        Vector3 normal = FBXCoordinateUtils.RemapVector(normal_source, sourceToTarget, signs);
                        if (normal.LengthSquared() > 0)
                            normal = Vector3.Normalize(normal);
                        // UV
                        float u = 0f, v = 0f;
                        if (uvs != null)
                        {
                            int uIdx;
                            if (uvMapping == "ByPolygonVertex")
                            {
                                if (uvRef == "IndexToDirect" && uvIdx != null)
                                {
                                    int polyVertIdx = i - tempPoly.Count + 1 + k;
                                    if (polyVertIdx < 0 || polyVertIdx >= uvIdx.Length)
                                    {
                                        Console.WriteLine($"BuildModelFromForest: Invalid uvIdx access at polyVertIdx={polyVertIdx}, uvIdx.Length={uvIdx.Length}, using default UV");
                                        uIdx = 0;
                                    }
                                    else
                                    {
                                        uIdx = uvIdx[polyVertIdx];
                                    }
                                }
                                else // Direct
                                {
                                    uIdx = i - tempPoly.Count + 1 + k;
                                }
                            }
                            else // ByVertice
                            {
                                uIdx = vertIdx;
                            }
                            if (uIdx < 0 || uIdx * 2 >= uvs.Length)
                            {
                                Console.WriteLine($"BuildModelFromForest: Invalid uIdx {uIdx}, uvs.Length={uvs.Length / 2}, using default UV");
                            }
                            else
                            {
                                u = (float)uvs[uIdx * 2];
                                v = 1f - (float)uvs[uIdx * 2 + 1]; // Flip V
                            }
                        }
                        // Material
                        float matId = 0f;
                        if (matIndices != null)
                        {
                            if (matMapping == "AllSame")
                            {
                                matId = matIndices[0];
                            }
                            else if (matMapping == "ByPolygon")
                            {
                                if (polyIndex < 0 || polyIndex >= matIndices.Length)
                                {
                                    Console.WriteLine($"BuildModelFromForest: Invalid polyIndex {polyIndex}, matIndices.Length={matIndices.Length}, using matId 0");
                                }
                                else
                                {
                                    matId = matIndices[polyIndex];
                                }
                            }
                        }
                        // Bones
                        var bw = perVertBones[vertIdx];
                        int b0 = bw.Count > 0 ? bw[0].boneIdx : -1;
                        float w0 = bw.Count > 0 ? bw[0].weight : 0f;
                        int b1 = bw.Count > 1 ? bw[1].boneIdx : -1;
                        float w1 = bw.Count > 1 ? bw[1].weight : 0f;
                        int b2 = bw.Count > 2 ? bw[2].boneIdx : -1;
                        float w2 = bw.Count > 2 ? bw[2].weight : 0f;
                        int b3 = bw.Count > 3 ? bw[3].boneIdx : -1;
                        float w3 = bw.Count > 3 ? bw[3].weight : 0f;
                        expandedVertices.Add(new FBXVertex(pos.X, pos.Y, pos.Z, normal.X, normal.Y, normal.Z, u, v, matId, 0, 0, 0, b0, b1, b2, b3, w0, w1, w2, w3));
                    }
                    currentIndex += tempPoly.Count;
                    tempPoly.Clear();
                    polyIndex++;
                }
            }
            return (expandedVertices, newIndices);
        }

        private static List<Material> ExtractMaterials(long geomId, BaseNode objectsNode, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, FBXFileForest forest)
        {
            List<Material> materials = new List<Material>();
            long geomIdMesh = geomId;
            var modelConns = conns.Where(c => c.type == "OO" && c.child == geomIdMesh).ToList();
            if (modelConns.Count > 0)
            {
                long modelId = modelConns[0].parent;
                var modelNode = objectsById[modelId];
                var matConns = conns.Where(c => c.type == "OO" && c.parent == modelId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "Material").ToList();
                foreach (var matConn in matConns)
                {
                    var matNode = objectsById[matConn.child];
                    string fullMatName = ((string)matNode.properties[1].Value).Split('\0')[0];
                    string[] matNameParts = fullMatName.Split("::");
                    string matName = matNameParts.Length > 1 ? matNameParts[1] : matNameParts[0];
                    Material mat = new Material { Name = matName };
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
                        if (!objectsById.ContainsKey(texConn.child)) continue;
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
                                    string fullVidName = ((string)videoNode.properties[1].Value).Split('\0')[0];
                                    string[] vidNameParts = fullVidName.Split("::");
                                    string vidName = vidNameParts.Length > 1 ? vidNameParts[1] : vidNameParts[0];
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
                                    string fullVidName = ((string)videoNode.properties[1].Value).Split('\0')[0];
                                    string[] vidNameParts = fullVidName.Split("::");
                                    string vidName = vidNameParts.Length > 1 ? vidNameParts[1] : vidNameParts[0];
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
                    materials.Add(mat);
                }
            }
            return materials;
        }

        private static Vector3 CalculateBounds(List<FBXVertex> vertices)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var v in vertices)
            {
                minX = Math.Min(minX, v.X);
                minY = Math.Min(minY, v.Y);
                minZ = Math.Min(minZ, v.Z);
                maxX = Math.Max(maxX, v.X);
                maxY = Math.Max(maxY, v.Y);
                maxZ = Math.Max(maxZ, v.Z);
            }
            return new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
        }
    }
}