using System;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace SiegeEngine.Core.UnityAssetLoader
{
    public class PrefabFileReader
    {
        public int CountGameObjects(string prefabPath)
        {
            if (!File.Exists(prefabPath))
            {
                throw new FileNotFoundException($"Prefab file not found: {prefabPath}");
            }

            try
            {
                using (var reader = new StreamReader(prefabPath))
                {
                    var yaml = new YamlStream();
                    yaml.Load(reader);
                    int count = 0;
                    foreach (var document in yaml.Documents)
                    {
                        var tag = document.RootNode.Tag.Value ?? string.Empty;
                        Console.WriteLine($"Prefab {prefabPath}: Document tag: '{tag}'");

                        // Check for Unity GameObject tag
                        if (tag == "tag:unity3d.com,2011:1")
                        {
                            count++;
                            Console.WriteLine($"Prefab {prefabPath}: GameObject detected via tag: {tag}");
                        }
                        // Fallback: Check if document contains a GameObject node
                        else if (document.RootNode is YamlMappingNode mappingNode &&
                                 mappingNode.Children.ContainsKey(new YamlScalarNode("GameObject")))
                        {
                            count++;
                            Console.WriteLine($"Prefab {prefabPath}: GameObject detected via fallback (GameObject node)");
                        }
                    }
                    Console.WriteLine($"Total GameObjects found in {prefabPath}: {count}");
                    return count;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing prefab {prefabPath}: {ex.Message}");
                throw new Exception($"Failed to parse prefab file {prefabPath}", ex);
            }
        }
    }
}