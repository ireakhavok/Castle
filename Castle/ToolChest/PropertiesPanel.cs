// Folder: ToolChest
// File: PropertiesPanel.cs
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
using System.Reflection;
using System.Text;
using System.Text.Json;
using Keystone;

namespace ToolChest
{
    public class PropertiesPanel : BasePanel, IDataAwarePanel
    {
        private class PropertiesUIOverlay : UIOverlay
        {
            private readonly PropertiesPanel _parent;
            public PropertiesUIOverlay(PropertiesPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
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

        private object _currentTarget;

        public PropertiesPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            AllowDragging = true;
            DockState = DockState.Floating;
            DockingMode = SiegeEngine.Core.Definitions.DockingMode.IDE;
            BaseWidth = 460f;
            BaseHeight = 420f;

            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new PropertiesUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            chrome.close_color = new Vector4(0.486f, 1.0f, 0.796f, 1.0f);
            LoadPropertiesUI();
        }

        private void OnGenericEvent(GenericEvent e)
        {
            if (e.Hook == "OutlinerSelectionChanged")
            {
                string nodeId = e.Data.GetValueOrDefault("nodeId", "");
                var provider = OutlinerCoordinator.Instance.GetLastActiveProvider();
                if (provider != null)
                {
                    _currentTarget = provider.GetObjectForNode(nodeId);
                    RebuildPropertiesUI();
                }
            }
        }

        private void LoadPropertiesUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PropertiesPanelUI.html");
            if (!File.Exists(htmlPath)) return;
            string html = File.ReadAllText(htmlPath);
            _uiOverlay.LoadUI(html);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private void RebuildPropertiesUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PropertiesPanelUI.html");
            if (!File.Exists(htmlPath)) return;

            string template = File.ReadAllText(htmlPath);
            string contentHtml = _currentTarget != null
                ? BuildPropertiesHtml(_currentTarget, "", 0)
                : "";

            string finalHtml = template.Replace("<!--PROPERTIES-->", contentHtml);

            _uiOverlay.LoadUI(finalHtml);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private string BuildPropertiesHtml(object obj, string pathPrefix, int depth)
        {
            if (obj == null || depth > 1) return "";

            var sb = new StringBuilder();
            var type = obj.GetType();

            // Public properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .OrderBy(p => p.Name);

            foreach (var prop in properties)
            {
                string fullPath = string.IsNullOrEmpty(pathPrefix) ? prop.Name : $"{pathPrefix}.{prop.Name}";
                object value = prop.GetValue(obj);
                string displayValue = value?.ToString() ?? "[null]";

                sb.Append($"<div class=\"property-row\">");
                sb.Append($"<div class=\"property-name\">{prop.Name}</div>");
                sb.Append($"<input type=\"text\" value=\"{displayValue}\" readonly>");
                sb.Append($"</div>");
            }

            // Public fields (for completeness)
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(f => f.Name);

            foreach (var field in fields)
            {
                string fullPath = string.IsNullOrEmpty(pathPrefix) ? field.Name : $"{pathPrefix}.{field.Name}";
                object value = field.GetValue(obj);
                string displayValue = value?.ToString() ?? "[null]";

                sb.Append($"<div class=\"property-row\">");
                sb.Append($"<div class=\"property-name\">{field.Name}</div>");
                sb.Append($"<input type=\"text\" value=\"{displayValue}\" readonly>");
                sb.Append($"</div>");
            }

            return sb.ToString();
        }

        public void HandleDataHook(string hook) { }
        public void HandleUIClick(HtmlElement elem) { }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new PropertiesPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }

        public string DataKey => "PropertiesPanel";

        public JsonElement SavePanelState()
        {
            return JsonSerializer.SerializeToElement(new Dictionary<string, object>());
        }

        public void LoadPanelState(JsonElement state) { }
    }
}