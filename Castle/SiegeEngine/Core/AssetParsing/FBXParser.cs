// Folder: SiegeEngine/Core/AssetParsing
// File: FBXParser.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.Model;
namespace SiegeEngine.Core.AssetParsing
{
    public static class FBXParser
    {
        public static FBXFileForest Load(string path)
        {
            var forest = new FBXFileForest();
            if (!File.Exists(path))
            {
                //FBXParserBase.Log($"FBXParser: File not found at {path}");
                return forest;
            }
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
                            return forest;
                        }
                        FBXParserBase.Log($"FBXParser: Invalid FBX file format at {path}");
                        return forest;
                    }
                    reader.ReadBytes(2); // Padding
                    uint version = reader.ReadUInt32();
                    FBXParserBase.Log($"FBXParser: FBX version {version}");
                    if (version < 7000)
                    {
                        FBXParserBase.Log($"FBXParser: Unsupported FBX version {version} at {path}, requires version 7000 or higher.");
                        return forest;
                    }
                    long fileLength = stream.Length;
                    NodeParser.ParseNodes(reader, version, fileLength, fileLength, null, forest);
                }
            }
            catch (Exception ex)
            {
                FBXParserBase.Log($"FBXParser: Error loading {path}: {ex.Message}");
            }
            return forest;
        }
        public static FBXModel BuildModelFromForest(FBXFileForest forest)
        {
            var model = new FBXModel();
            if (forest.TreeList.Count == 0)
            {
                FBXParserBase.Log("FBXParser: No nodes parsed, returning empty model");
                return model;
            }
            var objectsNode = forest.TreeList.FirstOrDefault(n => n.Name == "Objects");
            if (objectsNode == null)
            {
                FBXParserBase.Log("FBXParser: 'Objects' node not found, returning empty model");
                return model;
            }
            var headerNode = forest.TreeList.FirstOrDefault(n => n.Name == "FBXHeaderExtension");
            bool isBlender = false;
            if (headerNode != null)
            {
                var creatorNode = headerNode.children.FirstOrDefault(n => n.Name == "Creator");
                if (creatorNode != null && creatorNode.properties.Count > 0)
                {
                    string creator = creatorNode.properties[0].Value.ToString();
                    FBXParserBase.Log($"FBX Creator: {creator}");
                    if (creator.Contains("Blender"))
                    {
                        isBlender = true;
                        FBXParserBase.Log("Detected Blender FBX export");
                    }
                }
            }
            var settings = new FBXSettings();
            var globalSettingsNode = forest.TreeList.FirstOrDefault(n => n.Name == "GlobalSettings");
            if (globalSettingsNode != null)
            {
                var props70 = globalSettingsNode.children.FirstOrDefault(c => c.Name == "Properties70");
                if (props70 != null)
                {
                    var unitScaleP = props70.children.FirstOrDefault(p => p.Name == "P" && (p.properties[0].Value.ToString() == "UnitScaleFactor" || p.properties[0].Value.ToString() == "OriginalUnitScaleFactor"));
                    if (unitScaleP != null)
                    {
                        double unitScale = Convert.ToDouble(unitScaleP.properties[4].Value);
                        settings.ModelScale = (float)unitScale;
                        FBXParserBase.Log($"Detected UnitScaleFactor: {unitScale}, setting ModelScale to {settings.ModelScale}");
                    }
                    else
                    {
                        FBXParserBase.Log("No UnitScaleFactor found, keeping ModelScale at 1.0");
                    }
                    if (isBlender)
                    {
                        var upAxisP = props70.children.FirstOrDefault(p => p.Name == "P" && p.properties[0].Value.ToString() == "UpAxis");
                        var upAxisSignP = props70.children.FirstOrDefault(p => p.Name == "P" && p.properties[0].Value.ToString() == "UpAxisSign");
                        var frontAxisP = props70.children.FirstOrDefault(p => p.Name == "P" && p.properties[0].Value.ToString() == "FrontAxis");
                        var frontAxisSignP = props70.children.FirstOrDefault(p => p.Name == "P" && p.properties[0].Value.ToString() == "FrontAxisSign");
                        var coordAxisP = props70.children.FirstOrDefault(p => p.Name == "P" && p.properties[0].Value.ToString() == "CoordAxis");
                        var coordAxisSignP = props70.children.FirstOrDefault(p => p.Name == "P" && p.properties[0].Value.ToString() == "CoordAxisSign");
                        int upAxis = upAxisP != null ? Convert.ToInt32(upAxisP.properties[4].Value) : 2;
                        int upAxisSign = upAxisSignP != null ? Convert.ToInt32(upAxisSignP.properties[4].Value) : 1;
                        int frontAxis = frontAxisP != null ? Convert.ToInt32(frontAxisP.properties[4].Value) : 1;
                        int frontAxisSign = frontAxisSignP != null ? Convert.ToInt32(frontAxisSignP.properties[4].Value) : 1;
                        int coordAxis = coordAxisP != null ? Convert.ToInt32(coordAxisP.properties[4].Value) : 0;
                        int coordAxisSign = coordAxisSignP != null ? Convert.ToInt32(coordAxisSignP.properties[4].Value) : 1;
                        FBXParserBase.Log($"GlobalSettings: UpAxis={upAxis} (sign={upAxisSign}), FrontAxis={frontAxis} (sign={frontAxisSign}), CoordAxis={coordAxis} (sign={coordAxisSign})");
                        var detected = FBXSettings.DetectAxes(upAxis, upAxisSign, frontAxis, frontAxisSign, coordAxis, coordAxisSign);
                        int[] mapping = detected.mapping;
                        int[] signs = detected.signs;
                        settings.AxisMapping = mapping;
                        settings.AxisSigns = signs;
                        model.AutoCorrected = true;
                    }
                }
            }
            var objectsById = GatherObjectsById(objectsNode);
            var conns = GatherConnections(forest);
            var (boneIndexById, rootIndices) = FBXSkeletonParser.ParseSkeleton(model, objectsNode, objectsById, conns, settings);
            FBXSkeletonParser.BuildHierarchy(model, conns, boneIndexById);
            FBXMeshParser.ParseMeshes(model, objectsNode, conns, objectsById, settings.AxisMapping, settings.AxisSigns, settings.ModelScale, boneIndexById, rootIndices, settings.P4, settings.InvP4, forest);
            FBXAnimationParser.ParseAnimations(model, objectsNode, conns, objectsById, boneIndexById, settings.AxisMapping, settings.AxisSigns, settings.ModelScale, rootIndices, settings.P4, settings.InvP4);
            ParseBindPoses(model, objectsNode, boneIndexById, settings.InternalAxisMapping, settings.InternalAxisSigns);
            FBXParserBase.Log($"FBXParser: Built model with {model.Meshes.Count} meshes, {model.Skeleton.Bones.Count} bones, {model.Animations.Count} animations");
            return model;
        }
        public static Dictionary<long, BaseNode> GatherObjectsById(BaseNode objectsNode)
        {
            var objectsById = new Dictionary<long, BaseNode>();
            if (objectsNode != null)
            {
                foreach (var child in objectsNode.children)
                {
                    if (child.properties.Count > 0 && child.properties[0].Value is long id)
                    {
                        objectsById[id] = child;
                    }
                }
            }
            return objectsById;
        }
        public static List<(string type, long child, long parent, string prop)> GatherConnections(FBXFileForest forest)
        {
            var conns = new List<(string type, long child, long parent, string prop)>();
            var connectionsNode = forest.TreeList.FirstOrDefault(n => n.Name == "Connections");
            if (connectionsNode != null)
            {
                foreach (var conn in connectionsNode.children.Where(c => c.Name == "C"))
                {
                    if (conn.properties.Count >= 3 && conn.properties[0].Value is string type &&
                        conn.properties[1].Value is long child && conn.properties[2].Value is long parent)
                    {
                        string prop = conn.properties.Count > 3 && conn.properties[3].Value is string p ? p : null;
                        conns.Add((type, child, parent, prop));
                    }
                }
            }
            return conns;
        }
        // Additional helper methods will be added in subsequent steps
        private static void ParseBindPoses(FBXModel model, BaseNode objectsNode, Dictionary<long, int> boneIndexById, int[] sourceToTarget, int[] signs)
        {
            var poseNode = objectsNode.children.Where(n => n.Name == "Pose").Where(q => (string)q.properties[2].Value == "BindPose").FirstOrDefault();
            if (poseNode == null)
            {
                FBXParserBase.Log("No Bind Pose found");
                return;
            }
            foreach (var pnode in poseNode.children.Where(c => c.Name == "PoseNode"))
            {
                var nodeIdNode = pnode.children.FirstOrDefault(cn => cn.Name == "Node");
                if (nodeIdNode == null) continue;
                long boneId = (long)nodeIdNode.properties[0].Value;
                if (!boneIndexById.TryGetValue(boneId, out int idx)) continue;
                var matrixNode = pnode.children.FirstOrDefault(cn => cn.Name == "Matrix");
                if (matrixNode == null) continue;
                double[] vals = (double[])matrixNode.properties[0].Value;
                Matrix4x4 globalBind = FBXParserUtils.CreateMatrixFromArray(vals);
                globalBind = FBXCoordinateUtils.RemapMatrix(globalBind, sourceToTarget, signs);
                Matrix4x4.Invert(globalBind, out var invBind);
                model.Skeleton.Bones[idx].BindPose = invBind;
                // Compute BindLocal for alignment
                if (Matrix4x4.Invert(invBind, out Matrix4x4 global))
                {
                    Bone bone = model.Skeleton.Bones[idx];
                    if (bone.ParentIndex >= 0)
                    {
                        Matrix4x4 parentInvBind = model.Skeleton.Bones[bone.ParentIndex].BindPose;
                        if (Matrix4x4.Invert(parentInvBind, out Matrix4x4 parentGlobal))
                        {
                            bone.BindLocal = global * parentGlobal;
                        }
                        else
                        {
                            bone.BindLocal = Matrix4x4.Identity;
                        }
                    }
                    else
                    {
                        bone.BindLocal = global;
                    }
                }
            }
        }
        public static void Export(FBXModel fbxModel, string outputPath)
        {
            // Placeholder for FBX export logic
            // Implement binary FBX writer here
            // For now, log and return
            FBXParserBase.Log($"FBXParser: Exporting model to {outputPath} (implementation pending)");
            // To implement: reverse the parsing logic to write binary FBX
        }
    }
}