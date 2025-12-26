using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class TextureInfo
    {
        public string Path { get; set; }
        public int WrapU { get; set; }
        public int WrapV { get; set; }
    }
}