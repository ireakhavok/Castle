// Folder: SiegeEngine.Core
// File: AssetParsing.V2/FBXParser.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.V2.Model;
namespace SiegeEngine.Core.AssetParsing.V2
{
    public static class FBXParser
    {
        public static FBXFileForest Load(string path)
        {
            var forest = new FBXFileForest();
            if (!File.Exists(path))
            {
                FBXParserBase.Log($"FBXParser: File not found at {path}");
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
            var objectsById = GatherObjectsById(objectsNode);
            var conns = GatherConnections(forest);
            var settings = new FBXSettings();
            var (boneIndexById, rootIndices) = FBXSkeletonParser.ParseSkeleton(model, objectsNode, objectsById, conns, settings.AxisMapping, settings.AxisSigns, settings.ModelScale);
            FBXSkeletonParser.BuildHierarchy(model, conns, boneIndexById);
            FBXMeshParser.ParseMeshes(model, objectsNode, conns, objectsById, settings.AxisMapping, settings.AxisSigns, settings.ModelScale, boneIndexById, rootIndices, settings.P4, settings.InvP4, forest);
            FBXAnimationParser.ParseAnimations(model, objectsNode, conns, objectsById, boneIndexById, settings.AxisMapping, settings.AxisSigns, settings.ModelScale, rootIndices, settings.P4, settings.InvP4);
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
    }
}