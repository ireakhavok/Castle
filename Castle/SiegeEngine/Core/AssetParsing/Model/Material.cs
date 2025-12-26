using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class Material
    {
        public string Name { get; set; }
        public string ShadingModel { get; set; }
        public string CullingMode { get; set; }
        public bool MultiLayer { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public Dictionary<string, TextureInfo> Textures { get; set; } // e.g., "albedo" -> TextureInfo
        public Material()
        {
            Properties = new Dictionary<string, object>();
            Textures = new Dictionary<string, TextureInfo>();
        }
    }
}
