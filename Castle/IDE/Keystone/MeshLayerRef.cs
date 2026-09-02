// Folder: Keystone
// File: MeshLayerRef.cs
// First-layer hierarchy selection payload. PropertiesPanel inspects this;
// hide still lives on ModelComponent / ModelViewerScene.
using SiegeEngine.Core.Definitions;
using SiegeEngine.Scenes;

namespace Keystone
{
    public class MeshLayerRef
    {
        public int EntityId { get; set; } = -1;
        public int MeshIndex { get; set; }
        public string Label { get; set; }
        public Entity Entity { get; set; }
        public ModelViewerScene Viewer { get; set; }
    }
}
