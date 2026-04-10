// Folder: Keystone
// File: IOutlinerProvider.cs
using System.Collections.Generic;

namespace Keystone
{
    // Pure minimal opt-in interface for any panel that wants to drive the Outliner + Properties system.
    // Lives ONLY in Keystone (shared editor layer). No core engine files are touched.
    public interface IOutlinerProvider
    {
        string ContentType { get; }
        List<OutlinerNode> GetCurrentHierarchy();
        object GetObjectForNode(string nodeId);
        void NotifyHierarchyChanged();
    }
}