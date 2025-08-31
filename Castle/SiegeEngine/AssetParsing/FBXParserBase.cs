// Engine.Core.AssetParsing/FBXParserBase.cs
using SiegeEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace SiegeEngine.AssetParsing
{
    public static class FBXParserBase
    {
        private static int totalLogCount = 0;
        private const int MaxTotalLogs = 5000;
        public static void Log(string message)
        {
            if (totalLogCount >= MaxTotalLogs)
            {
                if (totalLogCount == MaxTotalLogs)
                {
                    Console.WriteLine($"FBXParser: Log limit reached, suppressing further logs");
                    totalLogCount++;
                }
                return;
            }
            Console.WriteLine(message);
            totalLogCount++;
        }
        public static bool IsValidNodeName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 255)
                return false;
            return name.All(c => c >= 32 && c <= 126);
        }
        public static FBXModel CreateDefaultCubeModel()
        {
            var model = new FBXModel
            {
                Skeleton = new Skeleton { Bones = new List<Bone>() },
                Animations = new List<Animation>()
            };
            var meshData = new MeshData
            {
                Vertices = new List<FBXVertex>(),
                Indices = new List<uint>()
            };
            model.Meshes.Add(meshData);
            float size = 5.0f;
            meshData.Vertices = new List<FBXVertex>
            {
                new FBXVertex(-size, -size, -size, 0f, 0f, -1f, 0f, 0f, 0f),
                new FBXVertex(size, -size, -size, 0f, 0f, -1f, 1f, 0f, 0f),
                new FBXVertex(size, size, -size, 0f, 0f, -1f, 1f, 1f, 0f),
                new FBXVertex(-size, size, -size, 0f, 0f, -1f, 0f, 1f, 0f),
                new FBXVertex(-size, -size, size, 0f, 0f, 1f, 0f, 0f, 0f),
                new FBXVertex(size, -size, size, 0f, 0f, 1f, 1f, 0f, 0f),
                new FBXVertex(size, size, size, 0f, 0f, 1f, 1f, 1f, 0f),
                new FBXVertex(-size, size, size, 0f, 0f, 1f, 0f, 1f, 0f)
            };
            meshData.Indices = new List<uint>
            {
                0, 1, 2, 2, 3, 0,
                4, 5, 6, 6, 7, 4,
                0, 1, 5, 5, 4, 0,
                2, 3, 7, 7, 6, 2,
                1, 2, 6, 6, 5, 1,
                3, 0, 4, 4, 7, 3
            };
            meshData.Bounds = new Vector3(size * 2, size * 2, size * 2);
            model.Skeleton.Bones.Add(new Bone { Name = "Root", BindPose = Matrix4x4.Identity, ParentIndex = -1 });
            model.Animations.Add(new Animation { Name = "Walk", Keyframes = new List<Keyframe>() });
            Log($"FBXParser: Created default cube model with {meshData.Vertices.Count} vertices");
            return model;
        }
    }
}