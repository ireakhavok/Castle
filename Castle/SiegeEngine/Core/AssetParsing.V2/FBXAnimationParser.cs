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
        public static void ParseAnimations(FBXModel model, BaseNode objectsNode, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, Dictionary<long, int> boneIndexById, int[] sourceToTarget, int[] signs, float modelScale, List<int> rootIndices, Matrix4x4 P4, Matrix4x4 invP4)
        {
            // Parse animations here iteratively
        }
    }
}