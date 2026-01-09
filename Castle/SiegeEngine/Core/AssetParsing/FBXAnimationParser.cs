// Folder: SiegeEngine.Core
// File: AssetParsing/FBXAnimationParser.cs
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing
{
    // This static class parses animations from AnimationStack/Layer/Curve nodes into keyframes.
    public static class FBXAnimationParser
    {
        // Parses all animation stacks, layers, curves, interpolates values, remaps, builds keyframes.
        public static void ParseAnimations(FBXModel model, BaseNode objectsNode, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, Dictionary<long, int> boneIndexById, int[] sourceToTarget, int[] signs, float modelScale, List<int> rootIndices, Matrix4x4 P4, Matrix4x4 invP4)
        {
            var animStackNodes = objectsNode.children.Where(n => n.Name == "AnimationStack").ToList();
            foreach (var stack in animStackNodes)
            {
                long stackId = (long)stack.properties[0].Value;
                string fullAnimName = ((string)stack.properties[1].Value).Split('\0')[0];
                string[] animNameParts = fullAnimName.Split(new string[] { "::", "|" }, StringSplitOptions.None);
                string animName = animNameParts[animNameParts.Length - 1];
                Animation anim = new Animation { Name = animName, Keyframes = new List<Keyframe>() };
                model.Animations.Add(anim);
                // Find layer
                var layerConns = conns.Where(c => c.type == "OO" && c.parent == stackId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "AnimationLayer").ToList();
                if (layerConns.Count == 0) continue;
                long layerId = layerConns[0].child;
                var layerNode = objectsById[layerId];
                var curveNodeConns = conns.Where(c => c.type == "OO" && c.parent == layerId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "AnimationCurveNode").ToList();
                var timeBoneTRS = new Dictionary<float, Dictionary<int, Dictionary<string, Vector3>>>();
                foreach (var curveNodeConn in curveNodeConns)
                {
                    long curveNodeId = curveNodeConn.child;
                    var boneConns = conns.Where(c => c.type == "OP" && c.child == curveNodeId && objectsById.ContainsKey(c.parent) && objectsById[c.parent].Name == "Model").ToList();
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
                    var (keyTimesX, keyValuesX) = GetCurveData(conns, objectsById, curveNodeId, "d|X");
                    var (keyTimesY, keyValuesY) = GetCurveData(conns, objectsById, curveNodeId, "d|Y");
                    var (keyTimesZ, keyValuesZ) = GetCurveData(conns, objectsById, curveNodeId, "d|Z");
                    // Collect unique times
                    HashSet<long> allKeyTimesSet = new HashSet<long>();
                    allKeyTimesSet.UnionWith(keyTimesX);
                    allKeyTimesSet.UnionWith(keyTimesY);
                    allKeyTimesSet.UnionWith(keyTimesZ);
                    List<long> allKeyTimes = allKeyTimesSet.OrderBy(t => t).ToList();
                    if (allKeyTimes.Count == 0) continue;
                    Bone bone = model.Skeleton.Bones[boneIdx];
                    Vector3 defaultVal = trsType switch
                    {
                        "T" => bone.LclTranslation,
                        "R" => Vector3.Zero, // Since we use quats, but default is identity, Euler 0
                        "S" => bone.LclScaling,
                        _ => Vector3.Zero
                    };
                    for (int k = 0; k < allKeyTimes.Count; k++)
                    {
                        long kt = allKeyTimes[k];
                        float t = kt / 46186158000f;
                        float vx = FBXParserUtils.GetValueAtTime(keyTimesX, keyValuesX, kt, defaultVal.X);
                        float vy = FBXParserUtils.GetValueAtTime(keyTimesY, keyValuesY, kt, defaultVal.Y);
                        float vz = FBXParserUtils.GetValueAtTime(keyTimesZ, keyValuesZ, kt, defaultVal.Z);
                        Vector3 val_source = new Vector3(vx, vy, vz);
                        Vector3 val;
                        if (trsType == "T")
                        {
                            val = FBXCoordinateUtils.RemapVector(val_source, sourceToTarget, signs) * modelScale;
                        }
                        else if (trsType == "R")
                        {
                            val = FBXCoordinateUtils.RemapRotation(val_source, sourceToTarget, signs);
                        }
                        else if (trsType == "S")
                        {
                            val = FBXCoordinateUtils.RemapScale(val_source, sourceToTarget, signs);
                        }
                        else
                        {
                            val = val_source;
                        }
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
                        trsVals[trsType] = val;
                    }
                }
                foreach (var kvTime in timeBoneTRS.OrderBy(kv => kv.Key))
                {
                    float t = kvTime.Key;
                    Keyframe kf = new Keyframe { Time = t, BoneTransforms = new List<Matrix4x4>() };
                    for (int i = 0; i < model.Skeleton.Bones.Count; i++)
                    {
                        kf.BoneTransforms.Add(model.Skeleton.Bones[i].LocalRest);
                    }
                    foreach (var kvBone in kvTime.Value)
                    {
                        int boneIdx = kvBone.Key;
                        var trsVals = kvBone.Value;
                        Vector3? animT = trsVals.ContainsKey("T") ? (Vector3?)trsVals["T"] : null;
                        Vector3? animRDeg = trsVals.ContainsKey("R") ? (Vector3?)trsVals["R"] : null;
                        Quaternion? animR = animRDeg.HasValue ? (Quaternion?)model.Skeleton.Bones[boneIdx].ToQuaternion(animRDeg.Value, model.Skeleton.Bones[boneIdx].RotationOrder) : null;
                        Vector3? animS = trsVals.ContainsKey("S") ? (Vector3?)trsVals["S"] : null;
                        Matrix4x4 local = model.Skeleton.Bones[boneIdx].ComputeLocal(animT, animR, animS);
                        local = model.P4 * local * model.InvP4;
                        kf.BoneTransforms[boneIdx] = local;
                    }
                    anim.Keyframes.Add(kf);
                }
                if (anim.Keyframes.Count == 0 && curveNodeConns.Count > 0)
                {
                    Keyframe defaultKf = new Keyframe { Time = 0, BoneTransforms = new List<Matrix4x4>() };
                    for (int i = 0; i < model.Skeleton.Bones.Count; i++)
                    {
                        defaultKf.BoneTransforms.Add(model.Skeleton.Bones[i].LocalRest);
                    }
                    anim.Keyframes.Add(defaultKf);
                }
            }
        }

        // Gets key times and values for a specific curve (X/Y/Z).
        private static (long[] keyTimes, float[] keyValues) GetCurveData(List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, long curveNodeId, string propName)
        {
            var curveConn = conns.FirstOrDefault(c => c.type == "OP" && c.parent == curveNodeId && c.prop == propName);
            long curveId = curveConn.type != null ? curveConn.child : 0;
            var curveNode = curveId != 0 ? objectsById.GetValueOrDefault(curveId) : null;
            var keyTimeNode = curveNode?.children.FirstOrDefault(c => c.Name == "KeyTime");
            long[] keyTimes = keyTimeNode != null ? (long[])keyTimeNode.properties[0].Value : new long[0];
            var keyValueNode = curveNode?.children.FirstOrDefault(c => c.Name == "KeyValueFloat");
            float[] keyValues = FBXParserUtils.ParseKeyValues(keyValueNode, keyTimes.Length);
            return (keyTimes, keyValues);
        }
    }
}