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
            // Stub, return defaults
            return (null, null, "", "");
        }
        private static (double[] uvs, int[] uvIdx, string uvMapping, string uvRef) ParseUVs(BaseNode geom)
        {
            // Stub, return defaults
            return (null, null, "", "");
        }
        private static (int[] matIndices, string matMapping) ParseMaterials(BaseNode geom)
        {
            // Stub, return defaults
            return (null, "");
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
            // Stub for material parsing
        }
        private static (List<FBXVertex> expandedVertices, List<uint> newIndices) BuildExpandedVerticesAndIndices(int[] pviArray, double[] vertsD, int[] sourceToTarget, int[] signs, float modelScale, double[] norms, int[] normIdx, string normMapping, string normRef, double[] uvs, int[] uvIdx, string uvMapping, string uvRef, int[] matIndices, string matMapping, List<List<(int boneIdx, float weight)>> perVertBones, int numVerts)
        {
            List<FBXVertex> expandedVertices = new List<FBXVertex>();
            List<uint> newIndices = new List<uint>();
            int currentIndex = 0;
            List<int> tempPoly = new List<int>();
            for (int i = 0; i < pviArray.Length; i++)
            {
                int pv = pviArray[i];
                bool end = pv < 0;
                int vId = end ? ~(pv) : pv;
                if (vId < 0 || vId >= numVerts)
                {
                    FBXParserBase.Log($"Invalid vId {vId} at i={i}, skipping polygon");
                    tempPoly.Clear();
                    continue;
                }
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
                        Vector3 pos = FBXCoordinateUtils.RemapVector(new Vector3(x, y, z), sourceToTarget, signs) * modelScale;
                        // Stub for other attributes
                        expandedVertices.Add(new FBXVertex { Position = pos });
                    }
                    currentIndex += tempPoly.Count;
                    tempPoly.Clear();
                }
            }
            return (expandedVertices, newIndices);
        }
    }
}