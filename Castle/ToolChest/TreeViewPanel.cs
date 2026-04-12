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
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Keystone;

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

        private readonly Dictionary<string, OutlinerNode> _nodes = new Dictionary<string, OutlinerNode>();
        private string _selectedNodeId = null;
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
                RefreshHierarchy();
            }
        }

        private void RefreshHierarchy()
        {
            var hierarchy = OutlinerCoordinator.Instance.GetCurrentHierarchy(out string[] expanded, out string[] selected);

            _nodes.Clear();
            _expandedNodeIds.Clear();
            _expandedNodeIds.UnionWith(expanded);

            foreach (var node in hierarchy)
            {
                _nodes[node.Id] = node;
                if (_expandedNodeIds.Contains(node.Id))
                    node.IsExpanded = true;
            }

            if (selected.Length > 0)
                _selectedNodeId = selected[0];

            RebuildTreeUI();
        }

        private void LoadTreeUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TreeViewPanelUI.html");
            if (!File.Exists(htmlPath)) return;
            string html = File.ReadAllText(htmlPath);
            _uiOverlay.LoadUI(html);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private void RebuildTreeUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TreeViewPanelUI.html");
            if (!File.Exists(htmlPath)) return;

            string template = File.ReadAllText(htmlPath);
            string nodesHtml = BuildTreeHtmlString();

            string finalHtml = template.Replace("<!--TREE_NODES-->", nodesHtml);

            _uiOverlay.LoadUI(finalHtml);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private string BuildTreeHtmlString()
        {
            var sb = new StringBuilder();
            foreach (var node in _nodes.Values.Where(n => string.IsNullOrEmpty(n.ParentId)))
            {
                sb.Append(BuildTreeHtmlStringRecursive(node.Id));
            }
            return sb.ToString();
        }

        private string BuildTreeHtmlStringRecursive(string nodeId)
        {
            if (!_nodes.TryGetValue(nodeId, out var node)) return "";

            string toggle = node.Children.Count > 0 ? (node.IsExpanded ? "▼" : "▶") : " ";
            string selectedClass = node.Id == _selectedNodeId ? " selected" : "";

            var sb = new StringBuilder();
            sb.Append($"<li class=\"node{selectedClass}\" data-node-id=\"{node.Id}\" data-hook=\"Select:{node.Id}\">");
            sb.Append($"<span class=\"toggle\">{toggle}</span>");
            sb.Append($"<span class=\"label\">{node.Icon} {node.Label}</span>");

            if (node.IsExpanded && node.Children.Count > 0)
            {
                sb.Append("<ul class=\"children\">");
                foreach (var childId in node.Children)
                {
                    sb.Append(BuildTreeHtmlStringRecursive(childId));
                }
                sb.Append("</ul>");
            }

            sb.Append("</li>");
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

                    OutlinerCoordinator.Instance.SaveExpandedState(
                        OutlinerCoordinator.Instance.GetLastActiveProvider()?.ContentType ?? "", _expandedNodeIds);

                    RebuildTreeUI();
                }
            }
            else if (hook.StartsWith("Select:"))
            {
                string id = hook.Substring(7);
                _selectedNodeId = id;

                OutlinerCoordinator.Instance.NotifySelectionChanged(id);

                OutlinerCoordinator.Instance.SaveSelectedState(
                    OutlinerCoordinator.Instance.GetLastActiveProvider()?.ContentType ?? "", new[] { id });

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

        public string DataKey => "TreeViewPanel";

        public JsonElement SavePanelState()
        {
            var state = new Dictionary<string, object>
            {
                ["expandedNodes"] = _expandedNodeIds.ToList(),
                ["selectedNodeId"] = _selectedNodeId ?? ""
            };
            return JsonSerializer.SerializeToElement(state);
        }

        public void LoadPanelState(JsonElement state)
        {
        }
    }
}