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
    public static class FBXSkeletonParser
    {
        public static (Dictionary<long, int> boneIndexById, List<int> rootIndices) ParseSkeleton(FBXModel model, BaseNode objectsNode, Dictionary<long, BaseNode> objectsById, List<(string type, long child, long parent, string prop)> conns, int[] sourceToTarget, int[] signs, float modelScale)
        {
            var modelNodes = objectsNode.children.Where(n => n.Name == "Model" && n.properties.Count >= 3 &&
                ((string)n.properties[2].Value == "LimbNode" || (string)n.properties[2].Value == "Limb" || (string)n.properties[2].Value == "Root")).ToList();
            Dictionary<long, int> boneIndexById = new Dictionary<long, int>();
            int boneIndex = 0;
            HashSet<long> usedBoneIds = new HashSet<long>();
            // First, collect used bone IDs from clusters
            var deformerNodes = objectsNode.children.Where(n => n.Name == "Deformer" && n.properties.Count >= 3 && (string)n.properties[2].Value == "Cluster").ToList();
            foreach (var deformer in deformerNodes)
            {
                long deformerId = (long)deformer.properties[0].Value;
                var boneConn = conns.FirstOrDefault(c => c.type == "OO" && (c.child == deformerId || c.parent == deformerId));
                if (boneConn.type != null)
                {
                    long boneId = (boneConn.child == deformerId) ? boneConn.parent : boneConn.child;
                    if (objectsById.ContainsKey(boneId) && objectsById[boneId].Name == "Model")
                    {
                        usedBoneIds.Add(boneId);
                    }
                }
            }
            // Recursively add ancestors and descendants to usedBoneIds
            HashSet<long> allUsedBoneIds = new HashSet<long>(usedBoneIds);
            foreach (var boneId in usedBoneIds.ToList())
            {
                AddAncestorsAndDescendants(boneId, allUsedBoneIds, conns, objectsById);
            }
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
                                float tx = Convert.ToSingle(p.properties[4].Value);
                                float ty = Convert.ToSingle(p.properties[5].Value);
                                float tz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 t_source = new Vector3(tx, ty, tz);
                                bone.LclTranslation = FBXCoordinateUtils.RemapVector(t_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "Lcl Rotation" && p.properties.Count >= 7)
                            {
                                float rx = Convert.ToSingle(p.properties[4].Value);
                                float ry = Convert.ToSingle(p.properties[5].Value);
                                float rz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 r_source = new Vector3(rx, ry, rz);
                                Vector3 remapped = FBXCoordinateUtils.RemapRotation(r_source, sourceToTarget, signs);
                                bone.LclRotationDegrees = remapped; // Store as degrees, syncs radians
                            }
                            else if (pname == "Lcl Scaling" && p.properties.Count >= 7)
                            {
                                float sx = Convert.ToSingle(p.properties[4].Value);
                                float sy = Convert.ToSingle(p.properties[5].Value);
                                float sz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 s_source = new Vector3(sx, sy, sz);
                                bone.LclScaling = FBXCoordinateUtils.RemapScale(s_source, sourceToTarget, signs);
                            }
                            else if (pname == "PreRotation" && p.properties.Count >= 7)
                            {
                                float prx = Convert.ToSingle(p.properties[4].Value);
                                float pry = Convert.ToSingle(p.properties[5].Value);
                                float prz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 pr_source = new Vector3(prx, pry, prz);
                                Vector3 remapped = FBXCoordinateUtils.RemapRotation(pr_source, sourceToTarget, signs);
                                bone.PreRotationDegrees = remapped;
                            }
                            else if (pname == "PostRotation" && p.properties.Count >= 7)
                            {
                                float pox = Convert.ToSingle(p.properties[4].Value);
                                float poy = Convert.ToSingle(p.properties[5].Value);
                                float poz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 po_source = new Vector3(pox, poy, poz);
                                po_source = -po_source; // Negate angles to correct directionality
                                Vector3 remapped = FBXCoordinateUtils.RemapRotation(po_source, sourceToTarget, signs);
                                bone.PostRotationDegrees = remapped;
                            }
                            else if (pname == "RotationPivot" && p.properties.Count >= 7)
                            {
                                float rpx = Convert.ToSingle(p.properties[4].Value);
                                float rpy = Convert.ToSingle(p.properties[5].Value);
                                float rpz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 rp_source = new Vector3(rpx, rpy, rpz);
                                bone.RotationPivot = FBXCoordinateUtils.RemapVector(rp_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "RotationOffset" && p.properties.Count >= 7)
                            {
                                float rox = Convert.ToSingle(p.properties[4].Value);
                                float roy = Convert.ToSingle(p.properties[5].Value);
                                float roz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 ro_source = new Vector3(rox, roy, roz);
                                bone.RotationOffset = FBXCoordinateUtils.RemapVector(ro_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "ScalingPivot" && p.properties.Count >= 7)
                            {
                                float spx = Convert.ToSingle(p.properties[4].Value);
                                float spy = Convert.ToSingle(p.properties[5].Value);
                                float spz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 sp_source = new Vector3(spx, spy, spz);
                                bone.ScalingPivot = FBXCoordinateUtils.RemapVector(sp_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "ScalingOffset" && p.properties.Count >= 7)
                            {
                                float sox = Convert.ToSingle(p.properties[4].Value);
                                float soy = Convert.ToSingle(p.properties[5].Value);
                                float soz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 so_source = new Vector3(sox, soy, soz);
                                bone.ScalingOffset = FBXCoordinateUtils.RemapVector(so_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "RotationOrder" && p.properties.Count >= 5)
                            {
                                int order_source = Convert.ToInt32(p.properties[4].Value);
                                bone.RotationOrder = FBXCoordinateUtils.RemapRotationOrder(order_source, sourceToTarget);
                            }
                            else if (pname == "Size" && p.properties.Count >= 5)
                            {
                                bone.Size = Convert.ToSingle(p.properties[4].Value) * modelScale; // Ensure scaling
                            }
                            else if (pname == "GeometricTranslation" && p.properties.Count >= 7)
                            {
                                float gtx = Convert.ToSingle(p.properties[4].Value);
                                float gty = Convert.ToSingle(p.properties[5].Value);
                                float gtz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 gt_source = new Vector3(gtx, gty, gtz);
                                bone.GeometricTranslation = FBXCoordinateUtils.RemapVector(gt_source, sourceToTarget, signs) * modelScale;
                            }
                            else if (pname == "GeometricRotation" && p.properties.Count >= 7)
                            {
                                float grx = Convert.ToSingle(p.properties[4].Value);
                                float gry = Convert.ToSingle(p.properties[5].Value);
                                float grz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 gr_source = new Vector3(grx, gry, grz);
                                Vector3 remapped = FBXCoordinateUtils.RemapRotation(gr_source, sourceToTarget, signs);
                                bone.GeometricRotationDegrees = remapped;
                            }
                            else if (pname == "GeometricScaling" && p.properties.Count >= 7)
                            {
                                float gsx = Convert.ToSingle(p.properties[4].Value);
                                float gsy = Convert.ToSingle(p.properties[5].Value);
                                float gsz = Convert.ToSingle(p.properties[6].Value);
                                Vector3 gs_source = new Vector3(gsx, gsy, gsz);
                                bone.GeometricScaling = FBXCoordinateUtils.RemapScale(gs_source, sourceToTarget, signs);
                            }
                        }
                    }
                }
                bone.LocalRest = bone.ComputeLocal();
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
        public static void BuildHierarchy(FBXModel model, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, int> boneIndexById)
        {
            foreach (var conn in conns)
            {
                if (conn.type == "OO" && boneIndexById.ContainsKey(conn.child) && boneIndexById.ContainsKey(conn.parent))
                {
                    int childIdx = boneIndexById[conn.child];
                    int parentIdx = boneIndexById[conn.parent];
                    model.Skeleton.Bones[childIdx].ParentIndex = parentIdx;
                }
            }
        }
        public static void ApplyRootRotation(FBXModel model, Matrix4x4 rootRot, List<int> rootIndices)
        {
            for (int i = 0; i < model.Skeleton.Bones.Count; i++)
            {
                if (model.Skeleton.Bones[i].ParentIndex == -1)
                {
                    model.Skeleton.Bones[i].LocalRest = rootRot * model.Skeleton.Bones[i].LocalRest;
                }
            }
        }
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