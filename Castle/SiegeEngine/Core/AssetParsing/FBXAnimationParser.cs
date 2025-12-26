// Folder: SiegeEngine.Core
// File: AssetParsing/FBXAnimationParser.cs
using SiegeEngine.Core.AssetObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing
{
    public static class FBXAnimationParser
    {
        public static void ParseAnimations(FBXModel model, BaseNode objectsNode, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, Dictionary<long, int> boneIndexById, int[] sourceToTarget, int[] signs, float modelScale, Matrix4x4 rootRot, List<int> rootIndices, Matrix4x4 P4, Matrix4x4 invP4)
        {
            var animStackNodes = objectsNode.children.Where(n => n.Name == "AnimationStack").ToList();
            Console.WriteLine($"Animation stacks found: {animStackNodes.Count}");
            foreach (var stack in animStackNodes)
            {
                long stackId = (long)stack.properties[0].Value;
                string fullAnimName = ((string)stack.properties[1].Value).Split('\0')[0];
                string[] animNameParts = fullAnimName.Split(new string[] { "::", "|" }, StringSplitOptions.None);
                string animName = animNameParts[animNameParts.Length - 1];
                Animation anim = new Animation { Name = animName, Keyframes = new List<Keyframe>() };
                model.Animations.Add(anim);
                Console.WriteLine($"Parsing animation stack {animName}");
                // Find layer
                var layerConns = conns.Where(c => c.type == "OO" && c.parent == stackId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "AnimationLayer").ToList();
                Console.WriteLine($"Layers for stack: {layerConns.Count}");
                if (layerConns.Count == 0) continue;
                long layerId = layerConns[0].child;
                var layerNode = objectsById[layerId];
                var curveNodeConns = conns.Where(c => c.type == "OO" && c.parent == layerId && objectsById.ContainsKey(c.child) && objectsById[c.child].Name == "AnimationCurveNode").ToList();
                Console.WriteLine($"Curve nodes for layer: {curveNodeConns.Count}");
                var timeBoneTRS = new Dictionary<float, Dictionary<int, Dictionary<string, Vector3>>>();
                foreach (var curveNodeConn in curveNodeConns)
                {
                    long curveNodeId = curveNodeConn.child;
                    var boneConns = conns.Where(c => c.type == "OP" && c.child == curveNodeId && objectsById.ContainsKey(c.parent) && objectsById[c.parent].Name == "Model").ToList();
                    Console.WriteLine($"Bone connections for curve node {curveNodeId}: {boneConns.Count}");
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
                    Console.WriteLine($"Curve for bone {model.Skeleton.Bones[boneIdx].Name} {trsType} with {allKeyTimes.Count} unique keys");
                    Bone bone = model.Skeleton.Bones[boneIdx];
                    Vector3 defaultVal = trsType switch
                    {
                        "T" => bone.LclTranslation,
                        "R" => bone.LclRotation,
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
                        Vector3? animR = trsVals.ContainsKey("R") ? (Vector3?)trsVals["R"] : null;
                        Vector3? animS = trsVals.ContainsKey("S") ? (Vector3?)trsVals["S"] : null;
                        Matrix4x4 local = model.Skeleton.Bones[boneIdx].ComputeLocal(animT, animR, animS);
                        if (rootIndices.Contains(boneIdx))
                        {
                            local = rootRot * local;
                        }
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
                    Console.WriteLine($"Added default keyframe for animation {anim.Name} since no keys parsed");
                }
                if (anim.Keyframes.Count > 0)
                {
                    float duration = anim.Keyframes.Last().Time;
                    Console.WriteLine($"Finished parsing animation {anim.Name} with {anim.Keyframes.Count} keyframes, duration: {duration} seconds");
                }
                else
                {
                    Console.WriteLine($"Finished parsing animation {anim.Name} with 0 keyframes");
                }
            }
        }

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