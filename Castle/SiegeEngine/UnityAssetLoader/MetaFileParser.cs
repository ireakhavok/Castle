using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace SiegeEngine.UnityAssetLoader
{
    public class MetaFileParser
    {
        public string ExtractGuid(string metaFilePath)
        {
            if (!File.Exists(metaFilePath))
            {
                throw new FileNotFoundException($"Meta file not found: {metaFilePath}");
            }

            try
            {
                using (var reader = new StreamReader(metaFilePath))
                {
                    var deserializer = new DeserializerBuilder().Build();
                    var yamlObject = deserializer.Deserialize<Dictionary<string, object>>(reader);
                    if (yamlObject.TryGetValue("guid", out var guidObj))
                    {
                        return guidObj.ToString();
                    }
                    else
                    {
                        throw new Exception($"GUID not found in meta file: {metaFilePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse meta file {metaFilePath}: {ex.Message}", ex);
            }
        }
    }
}