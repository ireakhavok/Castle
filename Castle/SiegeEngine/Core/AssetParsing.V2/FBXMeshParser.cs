// Folder: SiegeEngine.Core
// File: AssetParsing.V2/FBXMeshParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing.V2.Model;

namespace SiegeEngine.Core.AssetParsing.V2
{
    public static class FBXMeshParser
    {
        public static void ParseMeshes(FBXModel model, BaseNode objectsNode, List<(string type, long child, long parent, string prop)> conns, Dictionary<long, BaseNode> objectsById, int[] sourceToTarget, int[] signs, float modelScale, Dictionary<long, int> boneIndexById, List<int> rootIndices, Matrix4x4 P4, Matrix4x4 invP4, FBXFileForest forest)
        {
            // Parse meshes here iteratively
        }
    }
}