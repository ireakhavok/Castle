using SiegeEngine.Core.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using SiegeEngine.Core.AssetObjects;
namespace SiegeEngine.Core.AssetParsing
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
        public static FBXModel BuildModelFromForest(FBXFileForest forest, bool onlyAnimations = false)
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
            // Parse GlobalSettings
            var globalSettings = forest.TreeList.FirstOrDefault(n => n.Name == "GlobalSettings");
            int upAxis = 1; // Y
            int upAxisSign = 1;
            int frontAxis = 2; // Z
            int frontAxisSign = 1;
            int coordAxis = 0; // X
            int coordAxisSign = 1;
            float unitScaleFactor = 1f;
            float originalUnitScaleFactor = 1f;
            if (globalSettings != null)
            {
                var props70 = globalSettings.children.FirstOrDefault(c => c.Name == "Properties70");
                if (props70 != null)
                {
                    foreach (var p in props70.children)
                    {
                        if (p.Name == "P" && p.properties.Count >= 5)
                        {
                            string pname = (string)p.properties[0].Value;
                            if (pname == "UpAxis") upAxis = Convert.ToInt32(p.properties[4].Value);
                            else if (pname == "UpAxisSign") upAxisSign = Convert.ToInt32(p.properties[4].Value);
                            else if (pname == "FrontAxis") frontAxis = Convert.ToInt32(p.properties[4].Value);
                            else if (pname == "FrontAxisSign") frontAxisSign = Convert.ToInt32(p.properties[4].Value);
                            else if (pname == "CoordAxis") coordAxis = Convert.ToInt32(p.properties[4].Value);
                            else if (pname == "CoordAxisSign") coordAxisSign = Convert.ToInt32(p.properties[4].Value);
                            else if (pname == "UnitScaleFactor") unitScaleFactor = Convert.ToSingle(p.properties[4].Value);
                            else if (pname == "OriginalUnitScaleFactor") originalUnitScaleFactor = Convert.ToSingle(p.properties[4].Value);
                        }
                    }
                }
            }
            float modelScale = unitScaleFactor / 10f; // cm to m
            // Define axis remapping: source axis index to target axis index (0=X, 1=Y, 2=Z in target Y-up Z-forward)
            int[] sourceToTarget = new int[3];
            sourceToTarget[coordAxis] = 0; // Source coord -> target X
            sourceToTarget[upAxis] = 2; // Source up -> target Z
            sourceToTarget[frontAxis] = 1; // Source front -> target Y
            int[] signs = new int[3];
            signs[coordAxis] = coordAxisSign;
            signs[upAxis] = upAxisSign;
            signs[frontAxis] = -frontAxisSign;
            // Build P4
            Matrix4x4 P4 = Matrix4x4.Identity;
            float[,] p3 = new float[3, 3];
            for (int src = 0; src < 3; src++)
            {
                int tgt = sourceToTarget[src];
                p3[tgt, src] = signs[src];
            }
            P4 = new Matrix4x4(p3[0, 0], p3[0, 1], p3[0, 2], 0,
                               p3[1, 0], p3[1, 1], p3[1, 2], 0,
                               p3[2, 0], p3[2, 1], p3[2, 2], 0,
                               0, 0, 0, 1);
            Matrix4x4 invP4 = Matrix4x4.Transpose(P4);
            // Parse skeleton
            var modelNodes = objectsNode.children.Where(n => n.Name == "Model" && n.properties.Count >= 3 &&
                ((string)n.properties[2].Value == "LimbNode" || (string)n.properties[2].Value == "Root" || (string)n.properties[2].Value == "Null")).ToList();
            Dictionary<long, int> boneIndexById = new Dictionary<long, int>();
            int boneIndex = 0;
            foreach (var modelNode in modelNodes)
            {
                long id = (long)modelNode.properties[0].Value;
                string fullName = ((string)modelNode.properties[1].Value).Split('\0')[0];
                string[] nameParts = fullName.Split("::");
                string name = nameParts.Length > 1 ? nameParts[1] : nameParts[0];
                Bone bone = new Bone { Name = name, ParentIndex = -1, BindPose = Matrix4x4.Identity };
                // Parse properties
                var props70 = modelNode.children.FirstOrDefault(c => c.Name == "Properties70");
                if (props70 != null)
                {
                    foreach (var p in props70.children)
                    {
                        if (p.Name == "P" && p.properties.Count >= 7)
                        {
                            string pname = (string)p.properties[0].Value;
                            if (pname == "Lcl Translation")
                            {
                                float tx = Convert.ToSingle(p.properties[4].Value);
                                float ty = Convert.ToSingle(p.properties[5].Value);
                                float tz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 t_source = new Vector3(tx, ty, tz);
                                bone.LclTranslation = RemapVector(t_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "Lcl Rotation")
                            {
                                float rx = Convert.ToSingle(p.properties[4].Value);
                                float ry = Convert.ToSingle(p.properties[5].Value);
                                float rz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 r_source = new Vector3(rx, ry, rz);
                                bone.LclRotation = RemapRotation(r_source, sourceToTarget, signs);
                            }
                            else if (pname == "Lcl Scaling")
                            {
                                float sx = Convert.ToSingle(p.properties[4].Value);
                                float sy = Convert.ToSingle(p.properties[5].Value);
                                float sz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 s_source = new Vector3(sx, sy, sz);
                                bone.LclScaling = RemapScale(s_source, sourceToTarget, signs);
                            }
                            else if (pname == "PreRotation")
                            {
                                float prx = Convert.ToSingle(p.properties[4].Value);
                                float pry = Convert.ToSingle(p.properties[5].Value);
                                float prz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 pr_source = new Vector3(prx, pry, prz);
                                bone.PreRotation = RemapRotation(pr_source, sourceToTarget, signs);
                            }
                            else if (pname == "PostRotation")
                            {
                                float pox = Convert.ToSingle(p.properties[4].Value);
                                float poy = Convert.ToSingle(p.properties[5].Value);
                                float poz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 po_source = new Vector3(pox, poy, poz);
                                bone.PostRotation = RemapRotation(po_source, sourceToTarget, signs);
                            }
                            else if (pname == "RotationPivot")
                            {
                                float rpx = Convert.ToSingle(p.properties[4].Value);
                                float rpy = Convert.ToSingle(p.properties[5].Value);
                                float rpz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 rp_source = new Vector3(rpx, rpy, rpz);
                                bone.RotationPivot = RemapVector(rp_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "RotationOffset")
                            {
                                float rox = Convert.ToSingle(p.properties[4].Value);
                                float roy = Convert.ToSingle(p.properties[5].Value);
                                float roz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 ro_source = new Vector3(rox, roy, roz);
                                bone.RotationOffset = RemapVector(ro_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "ScalingPivot")
                            {
                                float spx = Convert.ToSingle(p.properties[4].Value);
                                float spy = Convert.ToSingle(p.properties[5].Value);
                                float spz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 sp_source = new Vector3(spx, spy, spz);
                                bone.ScalingPivot = RemapVector(sp_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "ScalingOffset")
                            {
                                float sox = Convert.ToSingle(p.properties[4].Value);
                                float soy = Convert.ToSingle(p.properties[5].Value);
                                float soz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 so_source = new Vector3(sox, soy, soz);
                                bone.ScalingOffset = RemapVector(so_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "RotationOrder" && p.properties.Count >= 5)
                            {
                                int order_source = Convert.ToInt32(p.properties[4].Value);
                                bone.RotationOrder = RemapRotationOrder(order_source, sourceToTarget);
                            }
                        }
                    }
                }
                bone.LocalRest = bone.ComputeLocal();
                model.Skeleton.Bones.Add(bone);
                boneIndexById[id] = boneIndex++;
            }
            // Build hierarchy
            foreach (var conn in conns)
            {
                if (conn.type == "OO" && boneIndexById.ContainsKey(conn.child) && boneIndexById.ContainsKey(conn.parent))
                {
                    int childIdx = boneIndexById[conn.child];
                    int parentIdx = boneIndexById[conn.parent];
                    model.Skeleton.Bones[childIdx].ParentIndex = parentIdx;
                }
            }
            // Apply root rotation if source is Z-up
            Matrix4x4 rootRot = Matrix4x4.Identity;
            if (upAxis == 2)
            {
                rootRot = Matrix4x4.CreateRotationX(MathF.PI / 2);
            }
            List<int> rootIndices = new List<int>();
            for (int i = 0; i < model.Skeleton.Bones.Count; i++)
            {
                if (model.Skeleton.Bones[i].ParentIndex == -1)
                {
                    rootIndices.Add(i);
                    model.Skeleton.Bones[i].LocalRest = rootRot * model.Skeleton.Bones[i].LocalRest;
                }
            }
            if (!onlyAnimations)
            {
                var geomNodes = objectsNode.children.Where(n => n.Name == "Geometry" && n.properties.Count >= 3 && (string)n.properties[2].Value == "Mesh").ToList();
                if (geomNodes.Count == 0 && !objectsNode.children.Any(n => n.Name == "AnimationStack"))
                {
                    Console.WriteLine("BuildModelFromForest: No Geometry::Mesh or AnimationStack nodes found");
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
                        Console.WriteLine($"BuildModelFromForest: Skipping invalid mesh, vertsD null: {vertsD == null}, pviArray null: {pviArray == null}");
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
                    // Prepare per-original-vertex bone data
                    int numVerts = vertsD.Length / 3;
                    List<List<(int boneIdx, float weight)>> perVertBones = Enumerable.Range(0, numVerts).Select(_ => new List<(int, float)>()).ToList();
                    // Parse skin if present
                    long geomId = (long)geom.properties[0].Value;
                    var skinConns = conns.Where(c => c.type == "OO" && c.parent == geomId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "Deformer" && (string)objectsById[c.child].properties[2].Value == "Skin").ToList();
                    if (skinConns.Any())
                    {
                        model.HasSkin = true;
                        foreach (var skinConn in skinConns)
                        {
                            var skinNode = objectsById[skinConn.child];
                            var clusterConns = conns.Where(c => c.type == "OO" && c.parent == skinConn.child && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "Deformer" && (string)objectsById[c.child].properties[2].Value == "Cluster").ToList();
                            foreach (var clusterConn in clusterConns)
                            {
                                var clusterNode = objectsById[clusterConn.child];
                                var boneConn = conns.FirstOrDefault(c => c.type == "OO" && c.child == clusterConn.child && objectsById.ContainsKey(c.parent) && objectsById[c.parent].Name == "Model");
                                if (boneConn.type == null) continue;
                                long boneId = boneConn.parent;
                                if (!boneIndexById.TryGetValue(boneId, out int boneIdx)) continue;
                                var indexesNode = clusterNode.children.FirstOrDefault(c => c.Name == "Indexes");
                                int[] indexes = indexesNode != null && indexesNode.properties[0].TypeCode == 'i' ? (int[])indexesNode.properties[0].Value : Array.Empty<int>();
                                var weightsNode = clusterNode.children.FirstOrDefault(c => c.Name == "Weights");
                                double[] weights = weightsNode != null && weightsNode.properties[0].TypeCode == 'd' ? (double[])weightsNode.properties[0].Value : Array.Empty<double>();
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
                                    perVertBones[vertIdx].Add((boneIdx, w));
                                }
                            }
                        }
                    }
                    // Normalize weights and limit to 4 per vertex
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
                    // Build expanded vertices
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
                                Vector3 pos_source = new Vector3(x, y, z);
                                Vector3 pos = RemapVector(pos_source, sourceToTarget, signs) * modelScale;
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
                                Vector3 normal = RemapVector(normal_source, sourceToTarget, signs);
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
                    mesh.Vertices = expandedVertices;
                    mesh.Indices = newIndices;
                    // Extract materials (unchanged)
                    long geomIdMesh = geomId; // rename to avoid shadow
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
            }
            // Parse animations
            var animStackNodes = objectsNode.children.Where(n => n.Name == "AnimationStack").ToList();
            Console.WriteLine($"Animation stacks found: {animStackNodes.Count}");
            foreach (var stack in animStackNodes)
            {
                long stackId = (long)stack.properties[0].Value;
                string fullAnimName = ((string)stack.properties[1].Value).Split('\0')[0];
                string[] animNameParts = fullAnimName.Split("::");
                string animName = animNameParts.Length > 1 ? animNameParts[1] : animNameParts[0];
                Animation anim = new Animation { Name = animName, Keyframes = new List<Keyframe>() };
                model.Animations.Add(anim);
                Console.WriteLine($"Parsing animation stack {animName}");
                // Find layer
                var layerConns = conns.Where(c => c.type == "OO" && c.parent == stackId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "AnimationLayer").ToList();
                Console.WriteLine($"Layers for stack: {layerConns.Count}");
                if (layerConns.Count == 0) continue;
                long layerId = layerConns[0].child;
                var layerNode = objectsById[layerId];
                var curveNodeConns = conns.Where(c => c.type == "OO" && c.parent == layerId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "AnimationCurveNode").ToList();
                Console.WriteLine($"Curve nodes for layer: {curveNodeConns.Count}");
                var timeBoneTRS = new Dictionary<float, Dictionary<int, Dictionary<string, Vector3>>>();
                foreach (var curveNodeConn in curveNodeConns)
                {
                    long curveNodeId = curveNodeConn.child;
                    var boneConns = conns.Where(c => c.type == "OP" && c.child == curveNodeId && objectsById.ContainsKey(c.parent) && objectsById[c.parent].Name == "Model").ToList();
                    Console.WriteLine($"Bone connections for curve node {curveNodeId}: {boneConns.Count}");
                    if (boneConns.Count == 0) continue;
                    var boneConn = boneConns[0];
                    long boneId = boneConn.parent;
                    if (!boneIndexById.TryGetValue(boneId, out int boneIdx)) continue;
                    string prop = boneConn.prop; // "Lcl Translation", "Lcl Rotation", "Lcl Scaling"
                    string trsType = "";
                    if (prop == "Lcl Translation") trsType = "T";
                    else if (prop == "Lcl Rotation") trsType = "R";
                    else if (prop == "Lcl Scaling") trsType = "S";
                    else continue;
                    // Get X, Y, Z curves
                    var curveXConn = conns.FirstOrDefault(c => c.type == "OP" && c.parent == curveNodeId && c.prop == "d|X");
                    long curveXId = curveXConn.child;
                    var curveXNode = objectsById.GetValueOrDefault(curveXId);
                    if (curveXNode == null)
                    {
                        Console.WriteLine($"No curveX for {trsType} on bone {boneIdx}");
                        continue;
                    }
                    var keyTimeNodeX = curveXNode.children.FirstOrDefault(c => c.Name == "KeyTime");
                    long[] keyTimes = keyTimeNodeX != null ? (long[])keyTimeNodeX.properties[0].Value : Array.Empty<long>();
                    var keyValueNodeX = curveXNode.children.FirstOrDefault(c => c.Name == "KeyValueFloat");
                    if (keyValueNodeX == null)
                    {
                        Console.WriteLine($"No KeyValueFloat for curveX {trsType} on bone {boneIdx}");
                    }
                    var keyValuePropX = keyValueNodeX?.properties[0];
                    char typeCodeX = keyValuePropX != null ? keyValuePropX.TypeCode : ' ';
                    Console.WriteLine($"KeyValue type for X: {typeCodeX}");
                    float[] keyValuesX = null;
                    if (keyValuePropX != null)
                    {
                        if (keyValuePropX.TypeCode == 'f')
                        {
                            keyValuesX = (float[])keyValuePropX.Value;
                        }
                        else if (keyValuePropX.TypeCode == 'd')
                        {
                            double[] dvals = (double[])keyValuePropX.Value;
                            keyValuesX = dvals.Select(d => (float)d).ToArray();
                        }
                        else
                        {
                            Console.WriteLine($"Unexpected type for KeyValueFloat X: {keyValuePropX.TypeCode}");
                        }
                    }
                    var curveYConn = conns.FirstOrDefault(c => c.type == "OP" && c.parent == curveNodeId && c.prop == "d|Y");
                    long curveYId = curveYConn.child;
                    var curveYNode = objectsById.GetValueOrDefault(curveYId);
                    float[] keyValuesY = null;
                    var keyValueNodeY = curveYNode?.children.FirstOrDefault(c => c.Name == "KeyValueFloat");
                    var keyValuePropY = keyValueNodeY?.properties[0];
                    if (keyValuePropY != null)
                    {
                        if (keyValuePropY.TypeCode == 'f')
                        {
                            keyValuesY = (float[])keyValuePropY.Value;
                        }
                        else if (keyValuePropY.TypeCode == 'd')
                        {
                            double[] dvals = (double[])keyValuePropY.Value;
                            keyValuesY = dvals.Select(d => (float)d).ToArray();
                        }
                    }
                    var curveZConn = conns.FirstOrDefault(c => c.type == "OP" && c.parent == curveNodeId && c.prop == "d|Z");
                    long curveZId = curveZConn.child;
                    var curveZNode = objectsById.GetValueOrDefault(curveZId);
                    float[] keyValuesZ = null;
                    var keyValueNodeZ = curveZNode?.children.FirstOrDefault(c => c.Name == "KeyValueFloat");
                    var keyValuePropZ = keyValueNodeZ?.properties[0];
                    if (keyValuePropZ != null)
                    {
                        if (keyValuePropZ.TypeCode == 'f')
                        {
                            keyValuesZ = (float[])keyValuePropZ.Value;
                        }
                        else if (keyValuePropZ.TypeCode == 'd')
                        {
                            double[] dvals = (double[])keyValuePropZ.Value;
                            keyValuesZ = dvals.Select(d => (float)d).ToArray();
                        }
                    }
                    if (keyTimes.Length == 0 || keyTimes.Length != (keyValuesX?.Length ?? 0) || keyTimes.Length != (keyValuesY?.Length ?? 0) || keyTimes.Length != (keyValuesZ?.Length ?? 0)) continue;
                    Console.WriteLine($"Curve for bone {boneIdx} {trsType} with {keyTimes.Length} keys");
                    for (int k = 0; k < keyTimes.Length; k++)
                    {
                        float t = keyTimes[k] / 46186158000f;
                        if (!timeBoneTRS.TryGetValue(t, out var boneTRS))
                        {
                            boneTRS = new Dictionary<int, Dictionary<string, Vector3>>();
                            timeBoneTRS[t] = boneTRS;
                        }
                        if (!boneTRS.TryGetValue(boneIdx, out var trsVals))
                        {
                            trsVals = new Dictionary<string, Vector3>();
                            boneTRS[boneIdx] = trsVals;
                        }
                        float vx = keyValuesX != null ? keyValuesX[k] : 0f;
                        float vy = keyValuesY != null ? keyValuesY[k] : 0f;
                        float vz = keyValuesZ != null ? keyValuesZ[k] : 0f;
                        Vector3 val_source = new Vector3(vx, vy, vz);
                        Vector3 val;
                        if (trsType == "T")
                        {
                            val = RemapVector(val_source, sourceToTarget, signs) * modelScale;
                        }
                        else if (trsType == "R")
                        {
                            val = RemapRotation(val_source, sourceToTarget, signs);
                        }
                        else if (trsType == "S")
                        {
                            val = RemapScale(val_source, sourceToTarget, signs);
                        }
                        else
                        {
                            val = val_source;
                        }
                        trsVals[trsType] = val;
                    }
                }
                foreach (var kvTime in timeBoneTRS)
                {
                    float t = kvTime.Key;
                    Keyframe kf = anim.Keyframes.FirstOrDefault(existingKf => existingKf.Time == t);
                    if (kf == null)
                    {
                        kf = new Keyframe { Time = t, BoneTransforms = new List<Matrix4x4>() };
                        for (int i = 0; i < model.Skeleton.Bones.Count; i++)
                        {
                            kf.BoneTransforms.Add(model.Skeleton.Bones[i].LocalRest);
                        }
                        anim.Keyframes.Add(kf);
                    }
                    foreach (var kvBone in kvTime.Value)
                    {
                        int boneIdx = kvBone.Key;
                        var trsVals = kvBone.Value;
                        Vector3? animT = trsVals.ContainsKey("T") ? (Vector3?)trsVals["T"] : null;
                        Vector3? animR = trsVals.ContainsKey("R") ? (Vector3?)trsVals["R"] : null;
                        Vector3? animS = trsVals.ContainsKey("S") ? (Vector3?)trsVals["S"] : null;
                        Matrix4x4 local = model.Skeleton.Bones[boneIdx].ComputeLocal(animT, animR, animS);
                        if (rootIndices.Contains(boneIdx) && upAxis == 2)
                        {
                            local = rootRot * local;
                        }
                        kf.BoneTransforms[boneIdx] = local;
                    }
                }
                anim.Keyframes = anim.Keyframes.OrderBy(kf => kf.Time).ToList();
                if (anim.Keyframes.Count > 0)
                {
                    float duration = anim.Keyframes.Last().Time;
                    Console.WriteLine($"Finished parsing animation {anim.Name} with {anim.Keyframes.Count} keyframes, duration: {duration} seconds");
                }
                else
                {
                    Console.WriteLine($"Finished parsing animation {anim.Name} with 0 keyframes");
                }
            }
            return model;
        }
        private static Vector3 RemapVector(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            Vector3 result = Vector3.Zero;
            float[] comps = new float[] { v.X, v.Y, v.Z };
            for (int src = 0; src < 3; src++)
            {
                float val = comps[src] * signs[src];
                int tgt = sourceToTarget[src];
                if (tgt == 0) result.X = val;
                else if (tgt == 1) result.Y = val;
                else if (tgt == 2) result.Z = val;
            }
            return result;
        }
        private static Vector3 RemapScale(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            Vector3 result = Vector3.Zero;
            float[] comps = new float[] { v.X, v.Y, v.Z };
            for (int src = 0; src < 3; src++)
            {
                float val = Math.Abs(comps[src]) * Math.Abs(signs[src]);
                int tgt = sourceToTarget[src];
                if (tgt == 0) result.X = val;
                else if (tgt == 1) result.Y = val;
                else if (tgt == 2) result.Z = val;
            }
            return result;
        }
        private static Vector3 RemapRotation(Vector3 v, int[] sourceToTarget, int[] signs)
        {
            Vector3 result = Vector3.Zero;
            float[] comps = new float[] { v.X, v.Y, v.Z };
            for (int src = 0; src < 3; src++)
            {
                float val = comps[src] * signs[src];
                int tgt = sourceToTarget[src];
                if (tgt == 0) result.X = val;
                else if (tgt == 1) result.Y = val;
                else if (tgt == 2) result.Z = val;
            }
            return result;
        }
        private static int RemapRotationOrder(int order, int[] sourceToTarget)
        {
            int[] seq_source = GetOrderSequence(order);
            int[] seq_target = new int[3];
            for (int i = 0; i < 3; i++)
            {
                seq_target[i] = sourceToTarget[seq_source[i]];
            }
            return GetOrderFromSequence(seq_target);
        }
        private static int[] GetOrderSequence(int order)
        {
            switch (order)
            {
                case 0: return new int[] { 0, 1, 2 }; // XYZ
                case 1: return new int[] { 0, 2, 1 }; // XZY
                case 2: return new int[] { 1, 2, 0 }; // YZX
                case 3: return new int[] { 1, 0, 2 }; // YXZ
                case 4: return new int[] { 2, 0, 1 }; // ZXY
                case 5: return new int[] { 2, 1, 0 }; // ZYX
                default: return new int[] { 0, 1, 2 };
            }
        }
        private static int GetOrderFromSequence(int[] seq)
        {
            string s = string.Join("", seq);
            switch (s)
            {
                case "012": return 0;
                case "021": return 1;
                case "120": return 2;
                case "102": return 3;
                case "201": return 4;
                case "210": return 5;
                default: return 0;
            }
        }
    }
}