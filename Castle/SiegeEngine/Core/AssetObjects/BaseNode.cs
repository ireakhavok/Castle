using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiegeEngine.Core.AssetObjects
{
    public class BaseNode
    {
        public Guid Id { get; } = Guid.NewGuid();
        public long endOffset { get; set; }
        public long numProperties { get; set; }
        public long propertyListLen { get; set; }
        public long nameLen { get; set; }
        public List<PropertyNode> properties { get; set; } = new List<PropertyNode>();
        public string Name { get; set; }
        public List<BaseNode> children { get; set; } = new List<BaseNode>();


    }
}
