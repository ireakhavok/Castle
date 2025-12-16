using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SiegeEngine.Core.UnityAssetLoader
{
    public class UnityAssetScanner
    {
        public (Dictionary<string, UnityAssetFileType> Files, Dictionary<string, string> GuidMap) ScanDirectoryDetailed(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            }

            var files = new Dictionary<string, UnityAssetFileType>();
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                UnityAssetFileType type;
                switch (extension)
                {
                    case ".prefab":
                        type = UnityAssetFileType.Prefab;
                        break;
                    case ".fbx":
                    case ".obj":
                        type = UnityAssetFileType.Model;
                        break;
                    case ".tga":
                    case ".png":
                        type = UnityAssetFileType.Texture;
                        break;
                    case ".mat":
                        type = UnityAssetFileType.Material;
                        break;
                    case ".meta":
                        type = UnityAssetFileType.Meta;
                        break;
                    default:
                        type = UnityAssetFileType.Unknown;
                        break;
                }
                files[file] = type;
                Console.WriteLine($"Found file: {file} ({type})");
            }

            var guidMap = new Dictionary<string, string>();
            foreach (var file in files.Keys.Where(f => Path.GetExtension(f).ToLowerInvariant() == ".meta"))
            {
                var assetPath = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));
                if (File.Exists(assetPath))
                {
                    try
                    {
                        var parser = new MetaFileParser();
                        var guid = parser.ExtractGuid(file);
                        guidMap[guid] = assetPath;
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine($"Error parsing meta file {file}: {ex.Message}");
                    }
                }
                else
                {
                    //Console.WriteLine($"Warning: Meta file {file} has no corresponding asset.");
                }
            }

            return (files, guidMap);
        }

        public Dictionary<string, UnityAssetFileType> ScanDirectory(string path)
        {
            var (files, _) = ScanDirectoryDetailed(path);
            return files;
        }

        public Dictionary<string, string> BuildGuidMap(string path)
        {
            var (_, guidMap) = ScanDirectoryDetailed(path);
            return guidMap;
        }
    }
}