// Folder: ToolChest
// File: IOutlinerNode.cs
using System.Collections.Generic;
using SiegeEngine.Core.UI;

namespace ToolChest
{
    public interface IOutlinerNode
    {
        string Id { get; }
        string Label { get; }
        string Icon { get; }
        IReadOnlyList<IOutlinerNode> Children { get; }
        object BackingObject { get; }
        HtmlElement ToHtmlElement(TreeViewPanel treeViewPanel);
    }
}