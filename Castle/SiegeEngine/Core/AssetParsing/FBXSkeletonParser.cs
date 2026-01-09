// Folder: SiegeEngine.Core
// File: AssetParsing/FBXSkeletonParser.cs
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing
{
    // This static class parses skeleton (bones) from Model::LimbNode/Root/Null nodes, builds hierarchy.
    public static class FBXSkeletonParser
    {
        // Parses all bones, their properties, remaps them, assigns indices.
        // Returns bone index by ID and root bone indices.
        public static (Dictionary<long, int> boneIndexById, List<int> rootIndices) ParseSkeleton(FBXModel model, BaseNode objectsNode, Dictionary<long, BaseNode> objectsById, List<(string type, long child, long parent, string prop)> conns, int[] sourceToTarget, int[] signs, float modelScale)
        {
            var modelNodes = objectsNode.children.Where(n => n.Name == "Model" && n.properties.Count >= 3 &&
                ((string)n.properties[2].Value == "LimbNode" || (string)n.properties[2].Value == "Limb" ||
                 (string)n.properties[2].Value == "Root" || (string)n.properties[2].Value == "Null")).ToList();
            Dictionary<long, int> boneIndexById = new Dictionary<long, int>();
            int boneIndex = 0;
            foreach (var modelNode in modelNodes)
            {
                long id = (long)modelNode.properties[0].Value;
                string fullName = ((string)modelNode.properties[1].Value).Split('\0')[0];
                string[] nameParts = fullName.Split(new string[] { "::", "|" }, StringSplitOptions.None);
                string name = nameParts[nameParts.Length - 1].Trim();
                if (name.EndsWith("_end")) continue; // Skip Blender end bones
                Bone bone = new Bone { Name = name, ParentIndex = -1, BindPose = Matrix4x4.Identity };
                string boneType = (string)modelNode.properties[2].Value;
                bone.BoneType = boneType;
                // Parse properties
                var props70 = modelNode.children.FirstOrDefault(c => c.Name == "Properties70");
                if (props70 != null)
                {
                    foreach (var p in props70.children)
                    {
                        if (p.Name == "P" && p.properties.Count >= 5)
                        {
                            string pname = (string)p.properties[0].Value;
                            if (pname == "Lcl Translation" && p.properties.Count >= 7)
                            {
                                float tx = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float ty = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float tz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 t_source = new Vector3(tx, ty, tz);
                                bone.LclTranslation = FBXCoordinateUtils.RemapVector(t_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "Lcl Rotation" && p.properties.Count >= 7)
                            {
                                float rx = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float ry = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float rz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 r_source = new Vector3(rx, ry, rz);
                                Vector3 r_remap = FBXCoordinateUtils.RemapRotation(r_source, sourceToTarget, signs);
                                bone.LclRotation = bone.ToQuaternion(r_remap, bone.RotationOrder);
                            }
                            else if (pname == "Lcl Scaling" && p.properties.Count >= 7)
                            {
                                float sx = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float sy = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float sz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 s_source = new Vector3(sx, sy, sz);
                                bone.LclScaling = FBXCoordinateUtils.RemapScale(s_source, sourceToTarget, signs);
                            }
                            else if (pname == "PreRotation" && p.properties.Count >= 7)
                            {
                                float prx = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float pry = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float prz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 pr_source = new Vector3(prx, pry, prz);
                                Vector3 pr_remap = FBXCoordinateUtils.RemapRotation(pr_source, sourceToTarget, signs);
                                bone.PreRotation = bone.ToQuaternion(pr_remap, 0);
                            }
                            else if (pname == "PostRotation" && p.properties.Count >= 7)
                            {
                                float pox = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float poy = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float poz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 po_source = new Vector3(pox, poy, poz);
                                Vector3 po_remap = FBXCoordinateUtils.RemapRotation(po_source, sourceToTarget, signs);
                                bone.PostRotation = bone.ToQuaternion(po_remap, 0);
                            }
                            else if (pname == "RotationPivot" && p.properties.Count >= 7)
                            {
                                float rpx = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float rpy = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float rpz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 rp_source = new Vector3(rpx, rpy, rpz);
                                bone.RotationPivot = FBXCoordinateUtils.RemapVector(rp_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "RotationOffset" && p.properties.Count >= 7)
                            {
                                float rox = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float roy = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float roz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 ro_source = new Vector3(rox, roy, roz);
                                bone.RotationOffset = FBXCoordinateUtils.RemapVector(ro_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "ScalingPivot" && p.properties.Count >= 7)
                            {
                                float spx = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float spy = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float spz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 sp_source = new Vector3(spx, spy, spz);
                                bone.ScalingPivot = FBXCoordinateUtils.RemapVector(sp_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "ScalingOffset" && p.properties.Count >= 7)
                            {
                                float sox = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float soy = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float soz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 so_source = new Vector3(sox, soy, soz);
                                bone.ScalingOffset = FBXCoordinateUtils.RemapVector(so_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "RotationOrder" && p.properties.Count >= 5)
                            {
                                int order_source = FBXParserUtils.GetPropertyInt(p.properties[4].Value);
                                bone.RotationOrder = FBXCoordinateUtils.RemapRotationOrder(order_source, sourceToTarget);
                            }
                            else if (pname == "Size" && p.properties.Count >= 5)
                            {
                                bone.Size = FBXParserUtils.GetPropertyFloat(p.properties[4].Value) * modelScale;
                            }
                            else if (pname == "GeometricTranslation" && p.properties.Count >= 7)
                            {
                                float gtx = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float gty = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float gtz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 gt_source = new Vector3(gtx, gty, gtz);
                                bone.GeometricTranslation = FBXCoordinateUtils.RemapVector(gt_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "GeometricRotation" && p.properties.Count >= 7)
                            {
                                float grx = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float gry = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float grz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 gr_source = new Vector3(grx, gry, grz);
                                Vector3 gr_remap = FBXCoordinateUtils.RemapRotation(gr_source, sourceToTarget, signs);
                                bone.GeometricRotation = bone.ToQuaternion(gr_remap, 0);
                            }
                            else if (pname == "GeometricScaling" && p.properties.Count >= 7)
                            {
                                float gsx = FBXParserUtils.GetPropertyFloat(p.properties[4].Value);
                                float gsy = FBXParserUtils.GetPropertyFloat(p.properties[5].Value);
                                float gsz = FBXParserUtils.GetPropertyFloat(p.properties[6].Value);
                                Vector3 gs_source = new Vector3(gsx, gsy, gsz);
                                bone.GeometricScaling = FBXCoordinateUtils.RemapScale(gs_source, sourceToTarget, signs);
                            }
                        }
                    }
                }
                bone.LocalRest = bone.ComputeLocal();
                bone.LocalRest = model.P4 * bone.LocalRest * model.InvP4;
                model.Skeleton.Bones.Add(bone);
                boneIndexById[id] = boneIndex++;
            }
            List<int> rootIndices = new List<int>();
            for (int i = 0; i < model.Skeleton.Bones.Count; i++)
            {
                if (model.Skeleton.Bones[i].ParentIndex == -1)
                {
                    rootIndices.Add(i);
                }
            }
            return (boneIndexById, rootIndices);
        }

        // Builds bone hierarchy by setting parent-child relations from connections.
        public static void BuildHierarchy(FBXModel model, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, int> boneIndexById)
        {
            foreach (var conn in conns)
            {
                if (conn.type == "OO" && boneIndexById.ContainsKey(conn.child) && boneIndexById.ContainsKey(conn.parent))
                {
                    int childIdx = boneIndexById[conn.child];
                    int parentIdx = boneIndexById[conn.parent];
                    model.Skeleton.Bones[childIdx].ParentIndex = parentIdx;
                    model.Skeleton.Bones[parentIdx].Children.Add(model.Skeleton.Bones[childIdx]);
                }
            }
        }

        // Recursively adds ancestors and descendants to a set of bone IDs, for gathering related bones.
        private static void AddAncestorsAndDescendants(long boneId, HashSet<long> usedIds, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById)
        {
            // Add ancestors
            var parentConn = conns.FirstOrDefault(c => c.type == "OO" && (c.child == boneId || c.parent == boneId));
            if (parentConn.type != null)
            {
                long parentId = (parentConn.child == boneId) ? parentConn.parent : parentConn.child;
                if (objectsById.ContainsKey(parentId) && objectsById[parentId].Name == "Model")
                {
                    if (usedIds.Add(parentId))
                    {
                        AddAncestorsAndDescendants(parentId, usedIds, conns, objectsById);
                    }
                }
            }
            // Add descendants
            var childConns = conns.Where(c => c.type == "OO" && (c.parent == boneId || c.child == boneId)).ToList();
            foreach (var childConn in childConns)
            {
                long childId = (childConn.parent == boneId) ? childConn.child : childConn.parent;
                if (objectsById.ContainsKey(childId) && objectsById[childId].Name == "Model")
                {
                    if (usedIds.Add(childId))
                    {
                        AddAncestorsAndDescendants(childId, usedIds, conns, objectsById);
                    }
                }
            }
        }
    }
}