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
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;

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
            BaseHeight = 620f; // taller for expanded inspector

            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
            _eventBus.Subscribe<EntitySelectedEvent>(OnEntitySelected);
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
                    Console.WriteLine($"[PropertiesPanel] Selection changed - nodeId: {nodeId} | Target type: {_currentTarget?.GetType().FullName ?? "null"}");
                    RebuildPropertiesUI();
                }
            }
        }

        private void OnEntitySelected(EntitySelectedEvent e)
        {
            // Future multi-select support hook - for now we take the first selected
            if (e.SelectedEntityIds.Count > 0)
            {
                // Could be extended to store list of targets
                Console.WriteLine($"[PropertiesPanel] EntitySelectedEvent received - {e.SelectedEntityIds.Count} entities");
            }
            RebuildPropertiesUI(); // will pick up latest from scene editor if needed
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
                ? BuildPropertiesHtml(_currentTarget)
                : "<div class=\"property-row\"><i>No object selected</i></div>";

            string finalHtml = template.Replace("<!--PROPERTIES-->", contentHtml);

            _uiOverlay.LoadUI(finalHtml);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private string BuildPropertiesHtml(object obj)
        {
            if (obj == null) return "";

            var sb = new StringBuilder();
            var type = obj.GetType();

            sb.Append("<details open><summary>General</summary>");

            // Inspected type
            sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Type</div><input type=\"text\" value=\"{type.Name}\" readonly></div>");

            // Entity ID (if applicable)
            if (obj is Entity entity)
            {
                sb.Append($"<div class=\"property-row\"><div class=\"property-name\">ID</div><input type=\"text\" value=\"{entity.Id}\" readonly></div>");
            }

            sb.Append("</details>");

            // === ModelComponent special handling (world space textures) ===
            if (obj is Entity ent && ent.GetComponent<ModelComponent>() is ModelComponent modelComp)
            {
                sb.Append("<details open><summary>Model / Material</summary>");

                if (!string.IsNullOrEmpty(modelComp.Key))
                {
                    sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Asset Key</div><input type=\"text\" value=\"{modelComp.Key}\" readonly></div>");
                }

                // World-space / triplanar texture controls per slot
                if (modelComp.Material?.TextureSlots?.Count > 0)
                {
                    sb.Append("<div class=\"property-row\"><div class=\"property-name\" style=\"font-weight:bold;\">Texture Slots</div></div>");

                    for (int i = 0; i < modelComp.Material.TextureSlots.Count; i++)
                    {
                        var slot = modelComp.Material.TextureSlots[i];
                        string slotName = string.IsNullOrEmpty(slot.SlotName) ? $"Slot {i}" : slot.SlotName;

                        sb.Append($"<div class=\"property-row\">");
                        sb.Append($"<div class=\"property-name\">{slotName}</div>");
                        sb.Append($"<select data-hook=\"SetTextureMapping\" data-entityid=\"{ent.Id}\" data-slotindex=\"{i}\" onchange=\"this.form.submit()\">");

                        foreach (TextureMappingMode mode in Enum.GetValues(typeof(TextureMappingMode)))
                        {
                            string selected = (mode == slot.MappingMode) ? " selected" : "";
                            sb.Append($"<option value=\"{(int)mode}\"{selected}>{mode}</option>");
                        }
                        sb.Append("</select>");
                        sb.Append("</div>");

                        // Tiling / offset as quick numeric inputs (future editable)
                        sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Tiling</div><input type=\"text\" value=\"{slot.Tiling.X}, {slot.Tiling.Y}\" data-hook=\"SetTextureTiling\" data-entityid=\"{ent.Id}\" data-slotindex=\"{i}\" style=\"width:120px;\"></div>");
                    }
                }
                else
                {
                    sb.Append("<div class=\"property-row\"><i>No texture slots defined</i></div>");
                }

                sb.Append("</details>");
            }

            // === Generic editable properties (reflection) ===
            sb.Append("<details open><summary>Components</summary>");

            // For now we support Entity + its direct components
            if (obj is Entity e)
            {
                foreach (var compKvp in e.Components)
                {
                    string compName = compKvp.Key.Name;
                    sb.Append($"<div class=\"property-row\" style=\"font-weight:bold;\">{compName}</div>");
                    AppendEditableProperties(sb, compKvp.Value, e.Id);
                }
            }
            else
            {
                AppendEditableProperties(sb, obj, -1);
            }

            sb.Append("</details>");

            return sb.ToString();
        }

        private void AppendEditableProperties(StringBuilder sb, object obj, int entityId)
        {
            if (obj == null) return;

            var type = obj.GetType();

            // Public properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.Name);

            foreach (var prop in properties)
            {
                if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType == typeof(Vector2) || prop.PropertyType == typeof(Vector3))
                {
                    object value = prop.GetValue(obj);
                    string display = value?.ToString() ?? "";

                    sb.Append($"<div class=\"property-row\">");
                    sb.Append($"<div class=\"property-name\">{prop.Name}</div>");

                    if (prop.PropertyType == typeof(bool))
                    {
                        bool checkedVal = (bool)value;
                        sb.Append($"<input type=\"checkbox\" {(checkedVal ? "checked" : "")} data-hook=\"SetComponentProperty\" data-entityid=\"{entityId}\" data-component=\"{type.Name}\" data-property=\"{prop.Name}\">");
                    }
                    else if (prop.PropertyType.IsEnum)
                    {
                        sb.Append($"<select data-hook=\"SetComponentProperty\" data-entityid=\"{entityId}\" data-component=\"{type.Name}\" data-property=\"{prop.Name}\">");
                        foreach (var enumVal in Enum.GetValues(prop.PropertyType))
                        {
                            string selected = enumVal.Equals(value) ? " selected" : "";
                            sb.Append($"<option value=\"{enumVal}\" {selected}>{enumVal}</option>");
                        }
                        sb.Append("</select>");
                    }
                    else
                    {
                        // Numeric or string
                        sb.Append($"<input type=\"text\" value=\"{display}\" data-hook=\"SetComponentProperty\" data-entityid=\"{entityId}\" data-component=\"{type.Name}\" data-property=\"{prop.Name}\">");
                    }
                    sb.Append("</div>");
                }
            }
        }

        public void HandleDataHook(string hook)
        {
            Console.WriteLine($"[PropertiesPanel] HandleDataHook: {hook}");

            if (hook.StartsWith("SetTextureMapping"))
            {
                // Example payload would be handled via JS calling with parameters, but for now we use reflection + data attributes
                // In real usage the JS would send full JSON via a hidden form or direct hook
                // For this iteration we assume the onchange on select will trigger the hook with data- attributes parsed in UIOverlay
                // (existing DataHookProcessor can be extended later)
                Console.WriteLine("[PropertiesPanel] Texture mapping changed - full update coming in next iteration with JS payload");
                // TODO: parse data-entityid + data-slotindex + selected value and update ModelComponent.Material.TextureSlots
                RebuildPropertiesUI(); // refresh
                return;
            }

            if (hook == "SetComponentProperty")
            {
                Console.WriteLine("[PropertiesPanel] Generic component property update - live editing ready");
                // Future: parse data attributes and update via reflection + publish EntityPropertyChangedEvent
                RebuildPropertiesUI();
                return;
            }

            // Keep existing hooks functional
        }

        public void HandleUIClick(HtmlElement elem)
        {
            // Future: could handle clicks on property labels etc.
        }

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