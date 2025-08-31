using SiegeEngine.AssetObjects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SiegeEngine.AssetParsing
{
    internal class MetaDataExporter
    {
        public static void ExportMetadata(FBXFileForest forest, string filePath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true }; // For readability
            var summary = SummarizeForest(forest);
            File.WriteAllText(filePath, JsonSerializer.Serialize(summary, options));
        }

        private static List<Dictionary<string, object>> SummarizeForest(FBXFileForest forest)
        {
            var rootSummaries = new List<Dictionary<string, object>>();
            foreach (var root in forest.TreeList)
            {
                rootSummaries.Add(SummarizeNode(root, depth: 0));
            }
            return rootSummaries;
        }

        private static Dictionary<string, object> SummarizeNode(BaseNode node, int depth)
        {
            if (depth > 5) // Limit depth to prevent explosion
            {
                return new Dictionary<string, object> { { "summary", $"Deep subtree: {node.Name} with {node.children.Count} children" } };
            }

            var nodeSummary = new Dictionary<string, object>
    {
        { "name", node.Name ?? "Unnamed" },
        { "numProperties", node.numProperties },
        { "childrenCount", node.children.Count }
    };

            var propSummaries = new List<Dictionary<string, object>>();
            foreach (var prop in node.properties)
            {
                var propSummary = new Dictionary<string, object> { { "typeCode", prop.TypeCode } };
                if (prop.Value is Array arr)
                {
                    propSummary["length"] = arr.Length;
                    var sample = new List<object>();
                    for (int i = 0; i < Math.Min(5, arr.Length); i++)
                    {
                        sample.Add(arr.GetValue(i));
                    }
                    propSummary["sample"] = sample;
                }
                else if (prop.Value is byte[] bytes)
                {
                    propSummary["value"] = $"Binary blob of length {bytes.Length}";
                }
                else if (prop.Value is string str)
                {
                    propSummary["value"] = str.Length > 100 ? str.Substring(0, 50) + "..." : str;
                }
                else
                {
                    propSummary["value"] = prop.Value ?? "null";
                }
                propSummaries.Add(propSummary);
            }
            nodeSummary["properties"] = propSummaries;

            var childSummaries = new List<Dictionary<string, object>>();
            for (int i = 0; i < node.children.Count; i++)
            {
                if (i < 5 || node.children.Count <= 20) // Truncate large child lists
                {
                    childSummaries.Add(SummarizeNode(node.children[i], depth + 1));
                }
                else
                {
                    nodeSummary["additionalChildren"] = node.children.Count - 5;
                    break;
                }
            }
            nodeSummary["children"] = childSummaries;

            return nodeSummary;
        }
    }
}
