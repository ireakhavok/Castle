// Engine.Core.AssetObjects/FBXFileForest.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace SiegeEngine.Core.AssetObjects
{
    public class FBXFileForest
    {
        public List<BaseNode> TreeList { get; set; } = new List<BaseNode>();
        public List<(string Name, byte[] Data)> EmbeddedTextures { get; set; } = new List<(string, byte[])>();
    }
}