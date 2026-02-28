// Folder: ToolChest
// File: PropertiesPanel.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace ToolChest
{
    public class PropertiesPanel : CompanionPanel
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

            protected override void HandleUIClick(HtmlElement elem)
            {
                _parent.HandleUIClick(elem);
            }
        }

        private object _currentTarget;
        private readonly Dictionary<string, PropertyInfo> _propertyMap = new Dictionary<string, PropertyInfo>();

        public PropertiesPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new PropertiesUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            LoadPropertiesUI();
        }

        private void LoadPropertiesUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PropertiesPanelUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine($"[PropertiesPanel] ERROR: PropertiesPanelUI.html not found at {htmlPath}");
                Console.WriteLine("Please create the static HTML file in the executable folder for fast iteration.");
                return;
            }

            string html = File.ReadAllText(htmlPath);
            _uiOverlay.LoadUI(html);

            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        public void SetTarget(object target)
        {
            _currentTarget = target;
            _propertyMap.Clear();
            RebuildPropertiesUI();
        }

        private void RebuildPropertiesUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PropertiesPanelUI.html");
            if (!File.Exists(htmlPath) || _currentTarget == null) return;

            string template = File.ReadAllText(htmlPath);
            string contentHtml = BuildPropertiesHtml(_currentTarget, "", 0);
            string finalHtml = template.Replace("<!--PROPERTIES-->", contentHtml);

            _uiOverlay.LoadUI(finalHtml);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private string BuildPropertiesHtml(object obj, string pathPrefix, int depth)
        {
            if (obj == null || depth > 3) return "<div style='color:#666;padding:8px;'>[Null or too deep]</div>";

            var sb = new StringBuilder();
            Type type = obj.GetType();
            string title = type.Name;

            sb.AppendLine($"<details style='margin-bottom:4px;' open='false'>");
            sb.AppendLine($"  <summary style='background:#252526;padding:6px 10px;cursor:pointer;font-weight:bold;'>{title}</summary>");
            sb.AppendLine($"  <div style='padding:8px;background:#1e1e1e;border-left:2px solid #094771;'>");

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false)
                .OrderBy(p => p.Name);

            foreach (var prop in properties)
            {
                string fullPath = string.IsNullOrEmpty(pathPrefix) ? prop.Name : $"{pathPrefix}.{prop.Name}";
                _propertyMap[fullPath] = prop;

                object value = prop.GetValue(obj);
                string displayValue = value?.ToString() ?? "";

                if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType == typeof(Vector2) || prop.PropertyType == typeof(Vector3) || prop.PropertyType == typeof(Vector4) || prop.PropertyType == typeof(Quaternion))
                {
                    string inputType = GetInputType(prop.PropertyType);
                    sb.AppendLine($"    <div style='display:flex;align-items:center;margin:4px 0;padding:2px;'>");
                    sb.AppendLine($"      <span style='width:140px;color:#aaa;'>{prop.Name}</span>");
                    sb.AppendLine($"      <input type='{inputType}' data-hook='SetProperty:{fullPath}' value='{displayValue}' style='flex:1;background:#333;border:1px solid #555;color:#ccc;padding:4px;'>");
                    sb.AppendLine($"    </div>");
                }
                else
                {
                    // Nested object → collapsed accordion
                    sb.AppendLine($"    <div style='margin:6px 0;'>");
                    sb.Append(BuildPropertiesHtml(value, fullPath, depth + 1));
                    sb.AppendLine($"    </div>");
                }
            }

            sb.AppendLine($"  </div>");
            sb.AppendLine($"</details>");
            return sb.ToString();
        }

        private string GetInputType(Type t)
        {
            if (t == typeof(bool)) return "checkbox";
            if (t == typeof(int) || t == typeof(float)) return "number";
            return "text";
        }

        public void HandleDataHook(string hook)
        {
            if (hook.StartsWith("SetProperty:"))
            {
                string path = hook.Substring(12);
                if (_propertyMap.TryGetValue(path, out var prop) && _currentTarget != null)
                {
                    // For simplicity in first version we log - real value parsing added in next iteration if needed
                    Console.WriteLine($"[PropertiesPanel] SetProperty requested: {path} on target {_currentTarget.GetType().Name}");
                    // TODO: parse input value and set via reflection (will be added in next iterative step)
                }
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
            var panel = new PropertiesPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}