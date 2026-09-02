// Folder: Keystone
// File: OutlinerNode.cs
using System.Collections.Generic;

namespace Keystone
{
    // Lightweight, clean node for the hierarchy tree.
    // Holds the real live object reference so PropertiesPanel can use pure reflection.
    public class OutlinerNode
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Icon { get; set; } = "📄";
        public string ParentId { get; set; }
        public List<string> Children { get; set; } = new List<string>();
        public object AssociatedObject { get; set; }
        public bool IsExpanded { get; set; } = false;
    }
}