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
            var animStacks = objectsNode.children.Where(n => n.Name == "AnimationStack").ToList();
            foreach (var stack in animStacks)
            {
                long stackId = (long)stack.properties[0].Value;
                string name = (string)stack.properties[1].Value;
                var anim = new Animation { Name = name };
                var layerConns = conns.Where(c => c.type == "OO" && c.parent == stackId).ToList();
                if (layerConns.Count == 0) continue;
                long layerId = layerConns[0].child; // assume first layer
                var layerCurveNodeIds = conns.Where(c => c.type == "OO" && c.parent == layerId).Select(c => c.child).ToList();
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
                var allTimes = new HashSet<long>();
                foreach (var kv in boneCurveNodes)
                {
                    foreach (var pkv in kv.Value)
                    {
                        var curveNode = objectsById[pkv.Value];
                        foreach (string chan in new[] { "d|X", "d|Y", "d|Z" })
                        {
                            var curve = curveNode.children.FirstOrDefault(n => n.Name == chan);
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
                var sortedTimes = allTimes.OrderBy(t => t).ToList();
                float duration = sortedTimes.Any() ? (float)(sortedTimes.Last() / (double)TicksPerSecond) : 0f;
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
                                t = GetInterpolatedVector(objectsById[cnid], tick, sourceToTarget, signs, modelScale, true);
                            }
                            if (dict.TryGetValue("Lcl Rotation", out cnid))
                            {
                                Vector3 euler = GetInterpolatedVector(objectsById[cnid], tick, sourceToTarget, signs, modelScale, false);
                                Quaternion rot = Quaternion.CreateFromYawPitchRoll(euler.Y * MathF.PI / 180f, euler.X * MathF.PI / 180f, euler.Z * MathF.PI / 180f);
                                Matrix4x4 rotMat = Matrix4x4.CreateFromQuaternion(rot);
                                rotMat = FBXCoordinateUtils.RemapMatrix(rotMat, sourceToTarget, signs);
                                r = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rotMat));
                            }
                            if (dict.TryGetValue("Lcl Scaling", out cnid))
                            {
                                Vector3 scale = GetInterpolatedVector(objectsById[cnid], tick, sourceToTarget, signs, modelScale, false);
                                s = scale;
                            }
                        }
                        locals[b] = Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
                    }
                    var globals = new Matrix4x4[model.Skeleton.Bones.Count];
                    foreach (int root in rootIndices)
                    {
                        ComputeGlobalRecursive(model.Skeleton.Bones[root], Matrix4x4.Identity, locals, globals, model.Skeleton.Bones);
                    }
                    kf.BoneTransforms = globals.ToList();
                    anim.Keyframes.Add(kf);
                }
                if (anim.Keyframes.Count > 0) model.Animations.Add(anim);
            }
        }
        private static Vector3 GetInterpolatedVector(BaseNode curveNode, long tick, int[] sourceToTarget, int[] signs, float modelScale, bool isTranslation)
        {
            Vector3 v = Vector3.Zero;
            v.X = GetInterpolatedValue(curveNode, "d|X", tick);
            v.Y = GetInterpolatedValue(curveNode, "d|Y", tick);
            v.Z = GetInterpolatedValue(curveNode, "d|Z", tick);
            v = FBXCoordinateUtils.RemapVector(v, sourceToTarget, signs);
            if (isTranslation) v *= modelScale;
            return v;
        }
        private static float GetInterpolatedValue(BaseNode curveNode, string chan, long tick)
        {
            var curve = curveNode.children.FirstOrDefault(n => n.Name == chan);
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
            globals[idx] = childGlobal;
            foreach (var child in bone.Children)
            {
                ComputeGlobalRecursive(child, childGlobal, locals, globals, bones);
            }
        }
    }
}