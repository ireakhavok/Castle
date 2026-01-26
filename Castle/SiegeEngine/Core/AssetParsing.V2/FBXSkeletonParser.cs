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
            var limbNodes = objectsNode.children.Where(n => n.Name == "Model" && n.properties.Count >= 3 &&
                (n.properties[2].Value.ToString() == "LimbNode" || n.properties[2].Value.ToString() == "Limb" ||
                 n.properties[2].Value.ToString() == "Root" || n.properties[2].Value.ToString() == "Null")).ToList();
            int index = 0;
            foreach (var limbNode in limbNodes)
            {
                long id = (long)limbNode.properties[0].Value;
                string fullName = ((string)limbNode.properties[1].Value).Split('\0')[0];
                string[] nameParts = fullName.Split(new string[] { "::", "|" }, StringSplitOptions.None);
                string name = nameParts[nameParts.Length - 1].Trim();
                if (name.EndsWith("_end")) continue;
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
                            Vector3 euler = new Vector3((float)rx, (float)ry, (float)rz); // No vector remap here
                            Quaternion rot = bone.ToQuaternion(euler);
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
                        else if (propName == "PreRotation")
                        {
                            double prx = (double)p.properties[4].Value;
                            double pry = (double)p.properties[5].Value;
                            double prz = (double)p.properties[6].Value;
                            Vector3 euler = FBXCoordinateUtils.RemapVector(new Vector3((float)prx, (float)pry, (float)prz), sourceToTarget, signs);
                            bone.PreRotation = euler;
                        }
                        else if (propName == "PostRotation")
                        {
                            double pox = (double)p.properties[4].Value;
                            double poy = (double)p.properties[5].Value;
                            double poz = (double)p.properties[6].Value;
                            Vector3 euler = FBXCoordinateUtils.RemapVector(new Vector3((float)pox, (float)poy, (float)poz), sourceToTarget, signs);
                            bone.PostRotation = euler;
                        }
                        else if (propName == "RotationPivot")
                        {
                            double rpx = (double)p.properties[4].Value;
                            double rpy = (double)p.properties[5].Value;
                            double rpz = (double)p.properties[6].Value;
                            bone.RotationPivot = FBXCoordinateUtils.RemapVector(new Vector3((float)rpx, (float)rpy, (float)rpz), sourceToTarget, signs) * modelScale;
                        }
                        else if (propName == "RotationOffset")
                        {
                            double rox = (double)p.properties[4].Value;
                            double roy = (double)p.properties[5].Value;
                            double roz = (double)p.properties[6].Value;
                            bone.RotationOffset = FBXCoordinateUtils.RemapVector(new Vector3((float)rox, (float)roy, (float)roz), sourceToTarget, signs) * modelScale;
                        }
                        else if (propName == "ScalingPivot")
                        {
                            double spx = (double)p.properties[4].Value;
                            double spy = (double)p.properties[5].Value;
                            double spz = (double)p.properties[6].Value;
                            bone.ScalingPivot = FBXCoordinateUtils.RemapVector(new Vector3((float)spx, (float)spy, (float)spz), sourceToTarget, signs) * modelScale;
                        }
                        else if (propName == "ScalingOffset")
                        {
                            double sox = (double)p.properties[4].Value;
                            double soy = (double)p.properties[5].Value;
                            double soz = (double)p.properties[6].Value;
                            bone.ScalingOffset = FBXCoordinateUtils.RemapVector(new Vector3((float)sox, (float)soy, (float)soz), sourceToTarget, signs) * modelScale;
                        }
                        else if (propName == "RotationOrder")
                        {
                            bone.RotationOrder = (int)(double)p.properties[4].Value;
                        }
                        else if (propName == "GeometricTranslation")
                        {
                            double gtx = (double)p.properties[4].Value;
                            double gty = (double)p.properties[5].Value;
                            double gtz = (double)p.properties[6].Value;
                            bone.GeometricTranslation = FBXCoordinateUtils.RemapVector(new Vector3((float)gtx, (float)gty, (float)gtz), sourceToTarget, signs) * modelScale;
                        }
                        else if (propName == "GeometricRotation")
                        {
                            double grx = (double)p.properties[4].Value;
                            double gry = (double)p.properties[5].Value;
                            double grz = (double)p.properties[6].Value;
                            Vector3 euler = FBXCoordinateUtils.RemapVector(new Vector3((float)grx, (float)gry, (float)grz), sourceToTarget, signs);
                            bone.GeometricRotation = euler;
                        }
                        else if (propName == "GeometricScaling")
                        {
                            double gsx = (double)p.properties[4].Value;
                            double gsy = (double)p.properties[5].Value;
                            double gsz = (double)p.properties[6].Value;
                            bone.GeometricScaling = new Vector3((float)gsx, (float)gsy, (float)gsz);
                        }
                        else if (propName == "InheritType")
                        {
                            bone.InheritType = Convert.ToInt32(p.properties[4].Value);
                        }
                        else
                        {
                            Console.WriteLine($"{propName}: not parsed.");
                        }
                    }
                    Console.WriteLine("** MODEL ORIGINAL Limbnode ComputeLocal logs below ***");
                    bone.LocalRest = bone.ComputeLocal();
                    bone.LocalRest = FBXCoordinateUtils.RemapMatrix(bone.LocalRest, sourceToTarget, signs);
                    bone.GeometricTransform = Matrix4x4.CreateScale(bone.GeometricScaling) * Matrix4x4.CreateFromYawPitchRoll(bone.GeometricRotation.Y * MathF.PI / 180f, bone.GeometricRotation.X * MathF.PI / 180f, bone.GeometricRotation.Z * MathF.PI / 180f) * Matrix4x4.CreateTranslation(bone.GeometricTranslation);
                    bone.GeometricTransform = FBXCoordinateUtils.RemapMatrix(bone.GeometricTransform, sourceToTarget, signs);
                }
                model.Skeleton.Bones.Add(bone);
                boneIndexById[id] = index;
                FBXParserBase.Log($"Parsed bone: ID={id}, Index={index}, Name={name}, Type={limbNode.properties[2].Value}, Translation={bone.LclTranslation}, Rotation={bone.LclRotation}, Scaling={bone.LclScaling}");
                index++;
            }
            var boneIds = new HashSet<long>(boneIndexById.Keys);
            var childBones = new HashSet<long>();
            foreach (var conn in conns.Where(conn => conn.type == "OO" && boneIds.Contains(conn.child) && boneIds.Contains(conn.parent)))
            {
                childBones.Add(conn.child);
                long parentId = conn.parent;
                long childId = conn.child;
                string parentName = model.Skeleton.Bones[boneIndexById[parentId]].Name;
                string childName = model.Skeleton.Bones[boneIndexById[childId]].Name;
                //FBXParserBase.Log($"Hierarchy connection: Parent ID={parentId} ({parentName}), Child ID={childId} ({childName})");
            }
            foreach (var bid in boneIds)
            {
                if (!childBones.Contains(bid))
                {
                    int rootIdx = boneIndexById[bid];
                    rootIndices.Add(rootIdx);
                    string rootName = model.Skeleton.Bones[rootIdx].Name;
                    FBXParserBase.Log($"Root bone: ID={bid}, Index={rootIdx}, Name={rootName}");
                }
            }
            // Fix Blender artifact on Armature node
            var armatureIdx = model.Skeleton.Bones.FindIndex(b => b.Name == "Armature");
            if (armatureIdx >= 0)
            {
                var armatureBone = model.Skeleton.Bones[armatureIdx];
                armatureBone.LclRotation = Quaternion.Identity;
                Console.WriteLine("** MODEL Limbnode recompute **");
                armatureBone.LocalRest = armatureBone.ComputeLocal();
                armatureBone.LocalRest = FBXCoordinateUtils.RemapMatrix(armatureBone.LocalRest, sourceToTarget, signs);
                FBXParserBase.Log("Applied fix to Armature bone rotation");
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
            FBXParserBase.Log("Built hierarchy:");
            foreach (var bone in model.Skeleton.Bones.Where(b => b.ParentIndex == -1))
            {
                LogBoneHierarchy(model.Skeleton.Bones, bone, 0);
            }
        }
        public static void LogBoneHierarchy(List<Bone> bones, Bone bone, int level)
        {
            string indent = new string(' ', level * 2);
            int idx = bones.IndexOf(bone);
            FBXParserBase.Log($"{indent}Bone {idx}: {bone.Name}, ParentIndex={bone.ParentIndex}, LocalRest Translation={bone.LocalRest.Translation}");
            foreach (var child in bone.Children)
            {
                LogBoneHierarchy(bones, child, level + 1);
            }
        }
    }
}