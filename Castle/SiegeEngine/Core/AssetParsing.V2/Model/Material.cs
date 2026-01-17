// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/Material.cs
using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Collections.Generic;

namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class Material
    {
        public string Name { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, TextureInfo> Textures { get; set; } = new Dictionary<string, TextureInfo>();
    }
}