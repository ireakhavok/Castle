// Folder: SiegeEngine.Core
// File: AssetParsing.V2/FBXSkeletonParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.V2.Model;

namespace SiegeEngine.Core.AssetParsing.V2
{
    public static class FBXSkeletonParser
    {
        public static (Dictionary<long, int> boneIndexById, List<int> rootIndices) ParseSkeleton(FBXModel model, BaseNode objectsNode, Dictionary<long, BaseNode> objectsById, List<(string type, long child, long parent, string prop)> conns, int[] sourceToTarget, int[] signs, float modelScale)
        {
            var boneIndexById = new Dictionary<long, int>();
            var rootIndices = new List<int>();
            var limbNodes = objectsNode.children.Where(n => n.Name == "Model" && n.properties.Count >= 3 && n.properties[2].Value.ToString() == "LimbNode").ToList();
            int index = 0;
            foreach (var limbNode in limbNodes)
            {
                long id = (long)limbNode.properties[0].Value;
                string fullName = (string)limbNode.properties[1].Value;
                string name = fullName.Split("::").LastOrDefault() ?? fullName;
                var bone = new Bone { Name = name, ParentIndex = -1 };
                var props70 = limbNode.children.FirstOrDefault(c => c.Name == "Properties70");
                if (props70 != null)
                {
                    foreach (var p in props70.children.Where(c => c.Name == "P"))
                    {
                        string propName = (string)p.properties[0].Value;
                        if (propName == "Lcl Translation")
                        {
                            double tx = (double)p.properties[4].Value;
                            double ty = (double)p.properties[5].Value;
                            double tz = (double)p.properties[6].Value;
                            bone.LclTranslation = FBXCoordinateUtils.RemapVector(new Vector3((float)tx, (float)ty, (float)tz), sourceToTarget, signs) * modelScale;
                        }
                        else if (propName == "Lcl Rotation")
                        {
                            double rx = (double)p.properties[4].Value;
                            double ry = (double)p.properties[5].Value;
                            double rz = (double)p.properties[6].Value;
                            Quaternion rot = Quaternion.CreateFromYawPitchRoll((float)ry * MathF.PI / 180f, (float)rx * MathF.PI / 180f, (float)rz * MathF.PI / 180f);
                            Matrix4x4 rotMat = Matrix4x4.CreateFromQuaternion(rot);
                            rotMat = FBXCoordinateUtils.RemapMatrix(rotMat, sourceToTarget, signs);
                            bone.LclRotation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rotMat));
                        }
                        else if (propName == "Lcl Scaling")
                        {
                            double sx = (double)p.properties[4].Value;
                            double sy = (double)p.properties[5].Value;
                            double sz = (double)p.properties[6].Value;
                            bone.LclScaling = new Vector3((float)sx, (float)sy, (float)sz);
                        }
                    }
                    bone.LocalRest = Matrix4x4.CreateScale(bone.LclScaling) * Matrix4x4.CreateFromQuaternion(bone.LclRotation) * Matrix4x4.CreateTranslation(bone.LclTranslation);
                }
                model.Skeleton.Bones.Add(bone);
                boneIndexById[id] = index;
                index++;
            }
            var boneIds = new HashSet<long>(boneIndexById.Keys);
            var childBones = new HashSet<long>();
            foreach (var conn in conns.Where(conn => conn.type == "OO" && boneIds.Contains(conn.child) && boneIds.Contains(conn.parent)))
            {
                childBones.Add(conn.child);
            }
            foreach (var bid in boneIds)
            {
                if (!childBones.Contains(bid))
                {
                    rootIndices.Add(boneIndexById[bid]);
                }
            }
            return (boneIndexById, rootIndices);
        }

        public static void BuildHierarchy(FBXModel model, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, int> boneIndexById)
        {
            foreach (var bone in model.Skeleton.Bones)
            {
                bone.Children.Clear();
            }
            foreach (var conn in conns.Where(conn => conn.type == "OO" && boneIndexById.ContainsKey(conn.child) && boneIndexById.ContainsKey(conn.parent)))
            {
                int childIdx = boneIndexById[conn.child];
                int parentIdx = boneIndexById[conn.parent];
                model.Skeleton.Bones[childIdx].ParentIndex = parentIdx;
                model.Skeleton.Bones[parentIdx].Children.Add(model.Skeleton.Bones[childIdx]);
            }
        }
    }
}