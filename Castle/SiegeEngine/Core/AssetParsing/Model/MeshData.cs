// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/MeshData.cs
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class MeshData
    {
        public string Name { get; set; }
        public string LodGroup { get; set; }
        public int LodLevel { get; set; }
        public List<FBXVertex> Vertices { get; set; } = new List<FBXVertex>();
        public List<uint> Indices { get; set; } = new List<uint>();
        public List<Material> Materials { get; set; } = new List<Material>();
        public Vector3 Bounds { get; set; }
        public Vector3 BoundsMin { get; set; }

        public static void AssignLodFromNames(IList<MeshData> meshes)
        {
            if (meshes == null) return;
            for (int i = 0; i < meshes.Count; i++)
            {
                MeshData mesh = meshes[i];
                if (mesh == null) continue;
                if (string.IsNullOrWhiteSpace(mesh.Name))
                    mesh.Name = "Mesh" + i;
                ParseLodName(mesh.Name, out string group, out int level);
                mesh.LodGroup = group;
                mesh.LodLevel = level;
            }
        }

        public static void ParseLodName(string name, out string group, out int level)
        {
            group = "";
            level = 0;
            if (string.IsNullOrWhiteSpace(name))
                return;
            string trimmed = name.Trim();
            if (TryTakeLodSuffix(trimmed, out string stripped, out level))
            {
                group = stripped;
                return;
            }
            group = "";
            level = 0;
        }

        public static int ChooseLodLevel(IList<MeshData> meshes, string group, float distance, float size)
        {
            if (meshes == null || string.IsNullOrEmpty(group))
                return 0;
            int want = DistanceToLod(distance, size);
            int best = -1;
            int maxLevel = -1;
            for (int i = 0; i < meshes.Count; i++)
            {
                MeshData mesh = meshes[i];
                if (mesh == null || !string.Equals(mesh.LodGroup, group, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (mesh.LodLevel > maxLevel)
                    maxLevel = mesh.LodLevel;
                if (mesh.LodLevel >= want && (best < 0 || mesh.LodLevel < best))
                    best = mesh.LodLevel;
            }
            if (best >= 0)
                return best;
            return maxLevel < 0 ? 0 : maxLevel;
        }

        public static bool ShouldSkipLod(IList<MeshData> meshes, int meshIndex, float distance, float size)
        {
            if (meshes == null || meshIndex < 0 || meshIndex >= meshes.Count)
                return false;
            MeshData mesh = meshes[meshIndex];
            if (mesh == null || string.IsNullOrEmpty(mesh.LodGroup))
                return false;
            bool grouped = false;
            for (int i = 0; i < meshes.Count; i++)
            {
                if (i == meshIndex) continue;
                MeshData other = meshes[i];
                if (other != null && string.Equals(other.LodGroup, mesh.LodGroup, StringComparison.OrdinalIgnoreCase))
                {
                    grouped = true;
                    break;
                }
            }
            if (!grouped)
                return false;
            int chosen = ChooseLodLevel(meshes, mesh.LodGroup, distance, size);
            return mesh.LodLevel != chosen;
        }

        public static int DistanceToLod(float distance, float size)
        {
            float s = size > 1e-4f ? size : 1f;
            if (distance > s * 40f) return 3;
            if (distance > s * 20f) return 2;
            if (distance > s * 8f) return 1;
            return 0;
        }

        private static bool TryTakeLodSuffix(string name, out string stripped, out int level)
        {
            stripped = name;
            level = 0;
            if (TryParseTrailingLod(name, "_LOD", out stripped, out level))
                return true;
            if (TryParseTrailingLod(name, "_lod", out stripped, out level))
                return true;
            if (TryParseTrailingLod(name, "LOD", out stripped, out level))
                return true;
            if (TryParseTrailingLod(name, "lod", out stripped, out level))
                return true;
            int space = name.LastIndexOf(" LOD ", StringComparison.OrdinalIgnoreCase);
            if (space >= 0 && space + 5 < name.Length && char.IsDigit(name[space + 5]))
            {
                int n = 0;
                int i = space + 5;
                while (i < name.Length && char.IsDigit(name[i]))
                {
                    n = n * 10 + (name[i] - '0');
                    i++;
                }
                stripped = name.Substring(0, space).TrimEnd('_', ' ', '.');
                if (string.IsNullOrEmpty(stripped))
                    stripped = name;
                level = n;
                return true;
            }
            return false;
        }

        private static bool TryParseTrailingLod(string name, string marker, out string stripped, out int level)
        {
            stripped = name;
            level = 0;
            int idx = name.LastIndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
                return false;
            int digitAt = idx + marker.Length;
            if (digitAt >= name.Length || !char.IsDigit(name[digitAt]))
                return false;
            int n = 0;
            int i = digitAt;
            while (i < name.Length && char.IsDigit(name[i]))
            {
                n = n * 10 + (name[i] - '0');
                i++;
            }
            stripped = name.Substring(0, idx).TrimEnd('_', ' ', '.');
            if (string.IsNullOrEmpty(stripped))
                stripped = name;
            level = n;
            return true;
        }
    }
}
