// Folder: ToolChest
// File: TreeViewPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ToolChest
{
    public class TreeViewPanel : CompanionPanel
    {
        private class TreeViewUIOverlay : UIOverlay
        {
            private readonly TreeViewPanel _parent;

            public TreeViewUIOverlay(TreeViewPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }

            protected override void HandleDataHook(string hook)
            {
                _parent.HandleDataHook(hook);
            }

            public override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }

        private readonly Dictionary<string, TreeNode> _nodes = new Dictionary<string, TreeNode>();
        private string _selectedNodeId = null;

        public TreeViewPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new TreeViewUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            LoadTreeUI();
        }

        private void LoadTreeUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TreeViewPanelUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine($"[TreeViewPanel] ERROR: TreeViewPanelUI.html not found at {htmlPath}");
                Console.WriteLine("Create the static HTML file in the executable directory for fast iteration and preview.");
                return;
            }

            string html = File.ReadAllText(htmlPath);
            _uiOverlay.LoadUI(html);

            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        public virtual void PopulateTree(IEnumerable<TreeNode> nodes)
        {
            _nodes.Clear();
            foreach (var node in nodes)
            {
                _nodes[node.Id] = node;
            }
            RebuildTreeUI();
        }

        private void RebuildTreeUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TreeViewPanelUI.html");
            if (!File.Exists(htmlPath)) return;

            string template = File.ReadAllText(htmlPath);
            string nodesHtml = BuildTreeHtmlString("root", 0);
            string finalHtml = template.Replace("<!--TREE_NODES-->", nodesHtml);

            _uiOverlay.LoadUI(finalHtml);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private string BuildTreeHtmlString(string nodeId, int indent)
        {
            if (!_nodes.TryGetValue(nodeId, out var node)) return "";

            string indentStr = new string(' ', indent * 4);
            string toggle = node.Children.Count > 0 ? (node.IsExpanded ? "▼" : "▶") : " ";
            string selected = node.Id == _selectedNodeId ? "selected" : "";

            var sb = new StringBuilder();
            sb.AppendLine($"{indentStr}<li class=\"node {selected}\" data-node-id=\"{node.Id}\">");
            sb.AppendLine($"{indentStr}  <span data-hook=\"Toggle:{node.Id}\" class=\"toggle\">{toggle}</span>");
            sb.AppendLine($"{indentStr}  <span data-hook=\"Select:{node.Id}\" class=\"label\">{node.Icon} {node.Label}</span>");

            if (node.IsExpanded && node.Children.Count > 0)
            {
                sb.AppendLine($"{indentStr}  <ul class=\"children\">");
                foreach (var childId in node.Children)
                {
                    sb.Append(BuildTreeHtmlString(childId, indent + 1));
                }
                sb.AppendLine($"{indentStr}  </ul>");
            }
            sb.AppendLine($"{indentStr}</li>");
            return sb.ToString();
        }

        public void HandleDataHook(string hook)
        {
            if (hook.StartsWith("Toggle:"))
            {
                string id = hook.Substring(7);
                if (_nodes.TryGetValue(id, out var node))
                {
                    node.IsExpanded = !node.IsExpanded;
                    RebuildTreeUI();
                }
            }
            else if (hook.StartsWith("Select:"))
            {
                string id = hook.Substring(7);
                _selectedNodeId = id;
                RebuildTreeUI();
                OnNodeSelected(id);
            }
            else if (hook == "RefreshTree")
            {
                Console.WriteLine("[TreeViewPanel] RefreshTree triggered - call PopulateTree from derived panel");
            }
            else if (hook == "CollapseAll")
            {
                foreach (var n in _nodes.Values) if (n.Children.Count > 0) n.IsExpanded = false;
                RebuildTreeUI();
            }
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (!string.IsNullOrEmpty(hook))
            {
                HandleDataHook(hook);
            }
        }

        protected virtual void OnNodeSelected(string nodeId)
        {
            Console.WriteLine($"[TreeViewPanel] Node selected: {nodeId}");
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new TreeViewPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }

        public class TreeNode
        {
            public string Id { get; set; }
            public string Label { get; set; }
            public string ParentId { get; set; }
            public string Icon { get; set; } = "📄";
            public bool IsExpanded { get; set; } = true;
            public List<string> Children { get; set; } = new List<string>();
        }
    }
}