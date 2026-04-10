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
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace ToolChest
{
    public class TreeViewPanel : BasePanel, IDataAwarePanel
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
            public override bool HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
                return true;
            }
        }

        private readonly Dictionary<string, TreeNode> _nodes = new Dictionary<string, TreeNode>();
        private string _selectedNodeId = null;
        private string _currentContentType = null;
        private readonly HashSet<string> _expandedNodeIds = new HashSet<string>();

        public TreeViewPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            AllowDragging = true;
            DockState = DockState.Floating;
            DockingMode = SiegeEngine.Core.Definitions.DockingMode.IDE;

            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new TreeViewUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            chrome.close_color = new Vector4(0.486f, 1.0f, 0.796f, 1.0f);
            LoadTreeUI();
        }

        private void OnGenericEvent(GenericEvent e)
        {
            if (e.Hook == "OutlinerHierarchyUpdate")
            {
                Console.WriteLine($"[TreeViewPanel] *** HIERARCHY UPDATE RECEIVED *** contentType={e.Data.GetValueOrDefault("contentType", "unknown")}");
                HandleOutlinerHierarchyUpdate(e);
            }
        }

        private void HandleOutlinerHierarchyUpdate(GenericEvent e)
        {
            string newContentType = e.Data.GetValueOrDefault("contentType", "default");
            string hierarchyJson = e.Data.GetValueOrDefault("hierarchy", "");

            if (string.IsNullOrEmpty(hierarchyJson))
            {
                Console.WriteLine("[TreeViewPanel] WARNING: Empty hierarchy JSON received");
                return;
            }

            if (_currentContentType != newContentType)
            {
                _currentContentType = newContentType;
                _nodes.Clear();
                _expandedNodeIds.Clear();
                Console.WriteLine($"[TreeViewPanel] Content type changed to {newContentType} - full rebuild");
            }

            var rootNodes = JsonSerializer.Deserialize<List<TreeNode>>(hierarchyJson);
            foreach (var node in rootNodes ?? Enumerable.Empty<TreeNode>())
            {
                _nodes[node.Id] = node;
                if (_expandedNodeIds.Contains(node.Id))
                    node.IsExpanded = true;
            }

            RebuildTreeUI();
            Console.WriteLine($"[TreeViewPanel] Hierarchy rebuilt with {_nodes.Count} nodes for type {newContentType}");
        }

        private void LoadTreeUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TreeViewPanelUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine($"[TreeViewPanel] ERROR: TreeViewPanelUI.html not found at {htmlPath}");
                return;
            }
            string html = File.ReadAllText(htmlPath);
            _uiOverlay.LoadUI(html);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private void RebuildTreeUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TreeViewPanelUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine("[TreeViewPanel] RebuildTreeUI failed - no HTML template");
                return;
            }

            string template = File.ReadAllText(htmlPath);
            string nodesHtml = BuildTreeHtmlString();

            Console.WriteLine($"[TreeViewPanel] Generated nodes HTML (first 400 chars):\n{nodesHtml.Substring(0, Math.Min(400, nodesHtml.Length))}");
            Console.WriteLine($"[TreeViewPanel] Template contains <!--TREE_NODES--> ? {template.Contains("<!--TREE_NODES-->")}");

            string finalHtml = template.Replace("<!--TREE_NODES-->", nodesHtml);

            _uiOverlay.LoadUI(finalHtml);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();

            Console.WriteLine($"[TreeViewPanel] RebuildTreeUI complete - UIOverlay refreshed with {nodesHtml.Length} chars of tree HTML");
        }

        // FIXED: No longer assumes a single "root" node. Builds ALL top-level nodes (those with no ParentId).
        // This works for any hierarchy we send from TerrainCreatorPanel or any future panel.
        private string BuildTreeHtmlString()
        {
            var sb = new StringBuilder();
            // Find all top-level nodes (no ParentId or ParentId is null/empty)
            foreach (var node in _nodes.Values.Where(n => string.IsNullOrEmpty(n.ParentId)))
            {
                sb.Append(BuildTreeHtmlStringRecursive(node.Id, 0));
            }
            return sb.ToString();
        }

        private string BuildTreeHtmlStringRecursive(string nodeId, int indent)
        {
            if (!_nodes.TryGetValue(nodeId, out var node)) return "";
            string indentStr = new string(' ', indent * 4);
            string toggle = node.Children.Count > 0 ? (node.IsExpanded ? "▼" : "▶") : " ";
            string selected = node.Id == _selectedNodeId ? "selected" : "";
            var sb = new StringBuilder();
            sb.AppendLine($"{indentStr}<li class=\"node {selected}\" data-node-id=\"{node.Id}\">");
            sb.AppendLine($"{indentStr} <span data-hook=\"Toggle:{node.Id}\" class=\"toggle\">{toggle}</span>");
            sb.AppendLine($"{indentStr} <span data-hook=\"Select:{node.Id}\" class=\"label\">{node.Icon} {node.Label}</span>");
            if (node.IsExpanded && node.Children.Count > 0)
            {
                sb.AppendLine($"{indentStr} <ul class=\"children\">");
                foreach (var childId in node.Children)
                {
                    sb.Append(BuildTreeHtmlStringRecursive(childId, indent + 1));
                }
                sb.AppendLine($"{indentStr} </ul>");
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
                    if (node.IsExpanded) _expandedNodeIds.Add(id);
                    else _expandedNodeIds.Remove(id);
                    RebuildTreeUI();
                }
            }
            else if (hook.StartsWith("Select:"))
            {
                string id = hook.Substring(7);
                _selectedNodeId = id;
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

        public string DataKey => "TreeViewPanel";

        public JsonElement SavePanelState()
        {
            var state = new Dictionary<string, object>
            {
                ["selectedNodeId"] = _selectedNodeId ?? "",
                ["expandedNodes"] = _expandedNodeIds.ToList(),
                ["currentContentType"] = _currentContentType ?? ""
            };
            return JsonSerializer.SerializeToElement(state);
        }

        public void LoadPanelState(JsonElement state)
        {
            try
            {
                if (state.TryGetProperty("selectedNodeId", out var selected))
                    _selectedNodeId = selected.GetString();
                if (state.TryGetProperty("expandedNodes", out var expanded))
                {
                    var list = expanded.Deserialize<List<string>>();
                    _expandedNodeIds.Clear();
                    if (list != null) foreach (var id in list) _expandedNodeIds.Add(id);
                }
                if (state.TryGetProperty("currentContentType", out var ct))
                    _currentContentType = ct.GetString();
                RebuildTreeUI();
            }
            catch { }
            Console.WriteLine($"[TreeViewPanel] Loaded panel state for DataKey '{DataKey}'");
        }
    }
}