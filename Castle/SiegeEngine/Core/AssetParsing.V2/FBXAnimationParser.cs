// Folder: SiegeEngine.Core
// File: AssetParsing.V2/FBXAnimationParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.V2.Model;

namespace SiegeEngine.Core.AssetParsing.V2
{
    public static class FBXAnimationParser
    {
        private const long TicksPerSecond = 46186158000L;

        public static void ParseAnimations(FBXModel model, BaseNode objectsNode, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, Dictionary<long, int> boneIndexById, int[] sourceToTarget, int[] signs, float modelScale, List<int> rootIndices, Matrix4x4 P4, Matrix4x4 invP4)
        {
            // In FBXAnimationParser.cs, in ParseAnimations
            int[] animSigns = new int[] { 1, 1, 1 };
            var animStacks = objectsNode.children.Where(n => n.Name == "AnimationStack").ToList();
            FBXParserBase.Log($"Found {animStacks.Count} AnimationStack nodes");
            foreach (var stack in animStacks)
            {
                long stackId = (long)stack.properties[0].Value;
                string name = (string)stack.properties[1].Value;
                var anim = new Animation { Name = name };
                var props70 = stack.children.FirstOrDefault(c => c.Name == "Properties70");
                if (props70 != null)
                {
                    var stopP = props70.children.FirstOrDefault(p => p.Name == "P" && (string)p.properties[0].Value == "LocalStop");
                    if (stopP != null)
                    {
                        long stopTicks = Convert.ToInt64(stopP.properties[4].Value);
                        anim.Duration = (float)(stopTicks / (double)TicksPerSecond);
                    }
                }
                var layerConns = conns.Where(c => c.type == "OO" && c.parent == stackId).ToList();
                FBXParserBase.Log($"For stack {name}, found {layerConns.Count} layers");
                if (layerConns.Count == 0) continue;
                long layerId = layerConns[0].child; // assume first layer
                var layerCurveNodeIds = conns.Where(c => c.type == "OO" && c.parent == layerId).Select(c => c.child).ToList();
                FBXParserBase.Log($"For layer, found {layerCurveNodeIds.Count} curve nodes");
                var boneCurveNodes = new Dictionary<int, Dictionary<string, long>>();
                foreach (var curveNodeId in layerCurveNodeIds)
                {
                    var boneConn = conns.FirstOrDefault(c => c.type.StartsWith("OP") && c.child == curveNodeId);
                    if (boneConn != default)
                    {
                        long boneId = boneConn.parent;
                        string prop = boneConn.prop;
                        if (boneIndexById.TryGetValue(boneId, out int bidx) && (prop == "Lcl Translation" || prop == "Lcl Rotation" || prop == "Lcl Scaling"))
                        {
                            if (!boneCurveNodes.TryGetValue(bidx, out var dict))
                            {
                                dict = new Dictionary<string, long>();
                                boneCurveNodes[bidx] = dict;
                            }
                            dict[prop] = curveNodeId;
                        }
                    }
                }
                FBXParserBase.Log($"Found {boneCurveNodes.Count} bones with curves");
                var allTimes = new HashSet<long>();
                foreach (var kv in boneCurveNodes)
                {
                    foreach (var pkv in kv.Value)
                    {
                        var curveNodeId = pkv.Value;
                        foreach (string chan in new[] { "d|X", "d|Y", "d|Z" })
                        {
                            var curve = GetCurveNodeForChan(objectsById, conns, curveNodeId, chan);
                            if (curve != null)
                            {
                                var keyTimeNode = curve.children.FirstOrDefault(n => n.Name == "KeyTime");
                                if (keyTimeNode != null && keyTimeNode.properties.Count > 0)
                                {
                                    long[] times = (long[])keyTimeNode.properties[0].Value;
                                    foreach (var t in times) allTimes.Add(t);
                                }
                            }
                        }
                    }
                }
                FBXParserBase.Log($"Found {allTimes.Count} unique key times");
                var sortedTimes = allTimes.OrderBy(t => t).ToList();
                if (anim.Duration == 0f && sortedTimes.Any())
                {
                    anim.Duration = (float)(sortedTimes.Last() / (double)TicksPerSecond);
                }
                for (int ti = 0; ti < sortedTimes.Count; ti++)
                {
                    long tick = sortedTimes[ti];
                    float time = (float)(tick / (double)TicksPerSecond);
                    var kf = new Keyframe { Time = time };
                    var locals = new Matrix4x4[model.Skeleton.Bones.Count];
                    for (int b = 0; b < model.Skeleton.Bones.Count; b++)
                    {
                        Vector3 t = model.Skeleton.Bones[b].LclTranslation;
                        Quaternion r = model.Skeleton.Bones[b].LclRotation;
                        Vector3 s = model.Skeleton.Bones[b].LclScaling;
                        if (boneCurveNodes.TryGetValue(b, out var dict))
                        {
                            if (dict.TryGetValue("Lcl Translation", out long cnid))
                            {
                                t = GetInterpolatedVector(objectsById, conns, cnid, tick, sourceToTarget, animSigns, modelScale, true);
                            }
                            if (dict.TryGetValue("Lcl Rotation", out cnid))
                            {
                                Vector3 euler = GetInterpolatedVector(objectsById, conns, cnid, tick, sourceToTarget, animSigns, modelScale, false);
                                r = model.Skeleton.Bones[b].ToQuaternion(euler);
                            }
                            if (dict.TryGetValue("Lcl Scaling", out cnid))
                            {
                                Vector3 scale = GetInterpolatedVector(objectsById, conns, cnid, tick, sourceToTarget, animSigns, modelScale, false);
                                s = scale;
                            }
                        }
                        locals[b] = model.Skeleton.Bones[b].ComputeLocal(t, r, s);
                    }
                    kf.BoneTransforms = locals.ToList();
                    anim.Keyframes.Add(kf);
                }
                if (anim.Keyframes.Count > 0) model.Animations.Add(anim);
            }
        }

        private static BaseNode GetCurveNodeForChan(Dictionary<long, BaseNode> objectsById, List<(string type, long child, long parent, string prop)> conns, long curveNodeId, string chan)
        {
            var curveConn = conns.FirstOrDefault(c => c.type == "OP" && c.parent == curveNodeId && c.prop == chan);
            if (curveConn.type == null) return null;
            return objectsById.GetValueOrDefault(curveConn.child);
        }

        private static Vector3 GetInterpolatedVector(Dictionary<long, BaseNode> objectsById, List<(string type, long child, long parent, string prop)> conns, long curveNodeId, long tick, int[] sourceToTarget, int[] signs, float modelScale, bool isTranslation)
        {
            Vector3 v = Vector3.Zero;
            v.X = GetInterpolatedValue(objectsById, conns, curveNodeId, "d|X", tick);
            v.Y = GetInterpolatedValue(objectsById, conns, curveNodeId, "d|Y", tick);
            v.Z = GetInterpolatedValue(objectsById, conns, curveNodeId, "d|Z", tick);
            v = FBXCoordinateUtils.RemapVector(v, sourceToTarget, signs);
            if (isTranslation) v *= modelScale;
            return v;
        }

        private static float GetInterpolatedValue(Dictionary<long, BaseNode> objectsById, List<(string type, long child, long parent, string prop)> conns, long curveNodeId, string chan, long tick)
        {
            var curve = GetCurveNodeForChan(objectsById, conns, curveNodeId, chan);
            if (curve == null) return 0f;
            var keyTimeNode = curve.children.FirstOrDefault(n => n.Name == "KeyTime");
            if (keyTimeNode == null) return 0f;
            long[] times = (long[])keyTimeNode.properties[0].Value;
            var keyValueNode = curve.children.FirstOrDefault(n => n.Name == "KeyValueFloat");
            if (keyValueNode == null) return 0f;
            float[] values = (float[])keyValueNode.properties[0].Value;
            if (times.Length == 0) return 0f;
            if (tick <= times[0]) return values[0];
            if (tick >= times[times.Length - 1]) return values[times.Length - 1];
            int low = 0, high = times.Length - 1;
            while (low < high)
            {
                int mid = (low + high) / 2 + 1;
                if (times[mid] > tick)
                {
                    high = mid - 1;
                }
                else
                {
                    low = mid;
                }
            }
            if (times[low] == tick) return values[low];
            float frac = (tick - times[low]) / (float)(times[low + 1] - times[low]);
            return values[low] + frac * (values[low + 1] - values[low]);
        }

        private static void ComputeGlobalRecursive(Bone bone, Matrix4x4 parentGlobal, Matrix4x4[] locals, Matrix4x4[] globals, List<Bone> bones)
        {
            int idx = bones.IndexOf(bone);
            Matrix4x4 local = locals[idx];
            Matrix4x4 childGlobal;
            if (!Matrix4x4.Decompose(parentGlobal, out Vector3 parentScale, out Quaternion parentRot, out Vector3 parentTrans))
            {
                parentScale = Vector3.One;
                parentRot = Quaternion.Identity;
                parentTrans = Vector3.Zero;
            }
            Matrix4x4 parentR = Matrix4x4.CreateFromQuaternion(parentRot);
            Matrix4x4 parentT = Matrix4x4.CreateTranslation(parentTrans);
            Matrix4x4 parentS = Matrix4x4.CreateScale(parentScale);
            if (!Matrix4x4.Decompose(local, out Vector3 childScale, out Quaternion childRot, out Vector3 childTrans))
            {
                childScale = Vector3.One;
                childRot = Quaternion.Identity;
                childTrans = Vector3.Zero;
            }
            Matrix4x4 childR = Matrix4x4.CreateFromQuaternion(childRot);
            Matrix4x4 childT = Matrix4x4.CreateTranslation(childTrans);
            Matrix4x4 childS = Matrix4x4.CreateScale(childScale);
            switch (bone.InheritType)
            {
                case 0: // eInheritRrSs
                    childGlobal = childS * parentS * childR * childT * parentR * parentT;
                    break;
                case 1: // eInheritRSrs
                    childGlobal = childS * childR * childT * parentS * parentR * parentT;
                    break;
                case 2: // eInheritRrs
                    childGlobal = childS * childR * childT * parentR * parentT;
                    break;
                default:
                    childGlobal = local * parentGlobal;
                    break;
            }
            childGlobal = childGlobal * bone.GeometricTransform;
            globals[idx] = childGlobal;
            foreach (var child in bone.Children)
            {
                ComputeGlobalRecursive(child, childGlobal, locals, globals, bones);
            }
        }
    }
}