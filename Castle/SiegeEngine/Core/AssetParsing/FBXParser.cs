// Folder: Root
// File: FBXParser.cs
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
namespace SiegeEngine.Core.AssetParsing
{
    public static class FBXParser
    {
        public static FBXFileForest Load(string path)
        {
            if (!File.Exists(path))
            {
                FBXParserBase.Log($"FBXParser: File not found at {path}");
                return new FBXFileForest();
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
                    NodeParser.ParseNodes(reader, version, fileLength, fileLength, null, context);
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
            var objectsById = GatherObjectsById(objectsNode);
            var conns = GatherConnections(forest);
            var (sourceToTarget, signs, modelScale, P4, invP4) = ParseGlobalSettingsAndRemapping(forest);
            model.SourceToTarget = sourceToTarget;
            model.Signs = signs;
            model.ModelScale = modelScale;
            model.P4 = P4;
            model.InvP4 = invP4;
            var (boneIndexById, rootIndices) = FBXSkeletonParser.ParseSkeleton(model, objectsNode, objectsById, conns, sourceToTarget, signs, modelScale);
            FBXSkeletonParser.BuildHierarchy(model, conns, boneIndexById);
            FBXMeshParser.ParseMeshes(model, objectsNode, conns, objectsById, sourceToTarget, signs, modelScale, boneIndexById, rootIndices, P4, invP4, forest);
            FBXAnimationParser.ParseAnimations(model, objectsNode, conns, objectsById, boneIndexById, sourceToTarget, signs, modelScale, rootIndices, P4, invP4);
            return model;
        }
        public static Dictionary<long, BaseNode> GatherObjectsById(BaseNode objectsNode)
        {
            var objectsById = new Dictionary<long, BaseNode>();
            foreach (var child in objectsNode.children)
            {
                if (child.properties.Count >= 1 && child.properties[0].TypeCode == 'L')
                {
                    long id = (long)child.properties[0].Value;
                    objectsById[id] = child;
                }
            }
            return objectsById;
        }
        public static List<(string type, long child, long parent, string prop)> GatherConnections(FBXFileForest forest)
        {
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
            return conns;
        }
        public static (int[] sourceToTarget, int[] signs, float modelScale, Matrix4x4 P4, Matrix4x4 invP4) ParseGlobalSettingsAndRemapping(FBXFileForest forest)
        {
            var globalSettings = forest.TreeList.FirstOrDefault(n => n.Name == "GlobalSettings");
            int upAxis = 2; // Y
            int upAxisSign = 1;
            int frontAxis = 1; // Z
            int frontAxisSign = 1;
            int coordAxis = 0; // X
            int coordAxisSign = 1;
            int coordSystem = 0; // 0: right-handed, 1: left-handed (assumed)
            int coordSystemSign = 1;
            float unitScaleFactor = 1f;
            float originalUnitScaleFactor = 1f;
            double frameRate = 30.0;
            int timeMode = 0;
            int snapOnFrameMode = 0;
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
                            try
                            {
                                if (pname == "UpAxis") upAxis = FBXParserUtils.GetPropertyInt(p.properties[4].Value);
                                else if (pname == "UpAxisSign") upAxisSign = FBXParserUtils.GetPropertyInt(p.properties[4].Value);
                                else if (pname == "FrontAxis") frontAxis = FBXParserUtils.GetPropertyInt(p.properties[4].Value);
                                else if (pname == "FrontAxisSign") frontAxisSign = FBXParserUtils.GetPropertyInt(p.properties[4].Value);
                                else if (pname == "CoordAxis") coordAxis = FBXParserUtils.GetPropertyInt(p.properties[4].Value);
                                else if (pname == "CoordAxisSign") coordAxisSign = FBXParserUtils.GetPropertyInt(p.properties[4].Value);
                                else if (pname == "CoordSystem") coordSystem = FBXParserUtils.GetPropertyInt(p.properties[4].Value);
                                else if (pname == "CoordSystemSign") coordSystemSign = FBXParserUtils.GetPropertyInt(p.properties[4].Value);
                                else if (pname == "UnitScaleFactor") unitScaleFactor = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                else if (pname == "OriginalUnitScaleFactor") originalUnitScaleFactor = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                else if (pname == "FrameRate") frameRate = FBXParserUtils.GetPropertyDouble(p.properties[4].Value);
                                else if (pname == "TimeMode") timeMode = FBXParserUtils.GetPropertyInt(p.properties[4].Value);
                                else if (pname == "SnapOnFrameMode") snapOnFrameMode = FBXParserUtils.GetPropertyInt(p.properties[4].Value);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error parsing global setting '{pname}': {ex.Message}");
                            }
                        }
                    }
                }
            }
            float modelScale = unitScaleFactor / originalUnitScaleFactor;
            Console.WriteLine($"Parsed global settings: UpAxis={upAxis} Sign={upAxisSign}, FrontAxis={frontAxis} Sign={frontAxisSign}, CoordAxis={coordAxis} Sign={coordAxisSign}, CoordSystem={coordSystem} Sign={coordSystemSign}, Scale={modelScale}, FrameRate={frameRate}");
            int[] sourceToTarget = new int[3];
            sourceToTarget[coordAxis] = 0; // Map to engine X
            sourceToTarget[frontAxis] = 1; // Map to engine Y (forward)
            sourceToTarget[upAxis] = 2; // Map to engine Z (up)
            int[] signs = new int[3];
            signs[coordAxis] = coordAxisSign;
            signs[frontAxis] = frontAxisSign;
            signs[upAxis] = upAxisSign;
            // Handle handedness
            int handedness = (coordSystem == 0 ? 1 : -1) * coordSystemSign; // Assume 0 right, positive
            // Engine-specific adjustment (e.g., flip forward if needed)
            // signs[frontAxis] = -signs[frontAxis]; // Assuming engine needs positive Y forward, flip if FBX has positive
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
            Matrix4x4 invP4;
            if (!Matrix4x4.Invert(P4, out invP4))
            {
                Console.WriteLine("Failed to invert P4, using transpose as approximation");
                invP4 = Matrix4x4.Transpose(P4);
            }

            return (sourceToTarget, signs, modelScale, P4, invP4);
        }
    }
}