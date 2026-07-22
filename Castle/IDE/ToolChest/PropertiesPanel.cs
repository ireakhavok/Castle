// Folder: ToolChest
// File: PropertiesPanel.cs
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
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.UI.Elements;
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
            // CRITICAL: must call base so InputElement focus is set.
            // Previous override swallowed the click and never focused any text field,
            // which is why typing did nothing in this panel (keys reached InputHandler
            // but _currentFocused was never an InputElement).
            public override bool HandleUIClick(HtmlElement elem)
            {
                bool handled = base.HandleUIClick(elem);
                _parent.HandleUIClick(elem);
                return handled;
            }
            protected override bool OnContextMenuRequested(HtmlElement sourceElement, Vector2 mousePos)
            {
                if (sourceElement == null) return false;
                string context = sourceElement.Attributes.GetValueOrDefault("data-context", "");
                if (context.StartsWith("skybox"))
                {
                    var items = new List<ContextMenuItem>
                    {
                        new ContextMenuItem("Rotate Skybox", "RotateSkybox")
                    };
                    ShowContextMenu(mousePos, items);
                    return true;
                }
                return false;
            }
        }
        private object _currentTarget;
        private string _activeSceneSettingsName;
        public PropertiesPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            AllowDragging = true;
            DockState = DockState.Floating;
            DockingMode = SiegeEngine.Core.Definitions.DockingMode.IDE;
            BaseWidth = 460f;
            BaseHeight = 620f;
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
                // Flush any in-progress Scene Settings typing before the target changes.
                FlushSceneSettingsFromUI();
                string nodeId = e.Data.GetValueOrDefault("nodeId", "");
                var provider = OutlinerCoordinator.Instance.GetLastActiveProvider();
                if (provider != null)
                {
                    _currentTarget = provider.GetObjectForNode(nodeId);
                    Console.WriteLine($"[PropertiesPanel] Selection changed - nodeId: {nodeId} | Target type: {_currentTarget?.GetType().FullName ?? "null"}");
                    RebuildPropertiesUI();
                }
            }
            else if (e.Hook == "SkyboxRotatePreview")
            {
                Console.WriteLine("[PropertiesPanel] SkyboxRotatePreview event received - forwarding to live preview");
            }
        }
        private void OnEntitySelected(EntitySelectedEvent e)
        {
            if (e.SelectedEntityIds.Count > 0)
            {
                Console.WriteLine($"[PropertiesPanel] EntitySelectedEvent received - {e.SelectedEntityIds.Count} entities");
            }
            FlushSceneSettingsFromUI();
            RebuildPropertiesUI();
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
            // Always capture live typed values before the HTML (and InputElements) are destroyed.
            FlushSceneSettingsFromUI();

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
        /// <summary>
        /// Reads the live InputElement.Value for the four Scene Settings fields (same pattern as
        /// NewProjectPanel) and writes them into the per-scene Settings buffer. Safe to call even
        /// when the inputs are not present.
        /// </summary>
        private void FlushSceneSettingsFromUI()
        {
            if (_uiOverlay == null || string.IsNullOrEmpty(_activeSceneSettingsName)) return;

            var avatarElem = _uiOverlay.FindElementById("ss-avatarPackKey") as InputElement;
            var controllerElem = _uiOverlay.FindElementById("ss-controllerTypeName") as InputElement;
            var spawnsElem = _uiOverlay.FindElementById("ss-preferredSpawnPointIds") as InputElement;
            var cameraElem = _uiOverlay.FindElementById("ss-cameraMode") as InputElement;

            // Nothing to flush if the Scene Settings section is not currently mounted.
            if (avatarElem == null && controllerElem == null && spawnsElem == null && cameraElem == null)
                return;

            var settings = ProjectSettings.Current.GetOrCreateSceneSettings(_activeSceneSettingsName);
            if (settings == null) return;

            if (avatarElem != null)
                settings.AvatarPackKey = string.IsNullOrWhiteSpace(avatarElem.Value) ? null : avatarElem.Value.Trim();
            if (controllerElem != null)
                settings.ControllerTypeName = string.IsNullOrWhiteSpace(controllerElem.Value) ? null : controllerElem.Value.Trim();
            if (spawnsElem != null)
            {
                settings.PreferredSpawnPointIds = new List<int>();
                if (!string.IsNullOrWhiteSpace(spawnsElem.Value))
                {
                    foreach (var part in spawnsElem.Value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(part.Trim(), out int id))
                            settings.PreferredSpawnPointIds.Add(id);
                    }
                }
            }
            if (cameraElem != null)
                settings.CameraMode = string.IsNullOrWhiteSpace(cameraElem.Value) ? null : cameraElem.Value.Trim();

            ProjectSettings.Current.SetSceneSettings(_activeSceneSettingsName, settings);
        }
        private string BuildPropertiesHtml(object obj)
        {
            if (obj == null) return "";
            var sb = new StringBuilder();
            var type = obj.GetType();
            sb.Append("<details open><summary>General</summary>");
            sb.Append($"<div class=\"property-row\" data-context=\"object-type\"><div class=\"property-name\">Type</div><input type=\"text\" value=\"{type.Name}\" readonly></div>");
            if (obj is Entity entity)
            {
                sb.Append($"<div class=\"property-row\" data-context=\"entity-id\"><div class=\"property-name\">ID</div><input type=\"text\" value=\"{entity.Id}\" readonly></div>");
            }
            sb.Append("</details>");

            // Scene Settings authoring surface when the scene root / level-info node is selected.
            // Each scene owns its own Settings entry (keyed by scene name) so swaps stay coherent.
            // Inputs use stable ids so we can read live InputElement.Value exactly like NewProjectPanel.
            if (obj is Level level)
            {
                string sceneName = level.Name ?? ProjectSettings.Current.CurrentSceneName ?? "Main";
                _activeSceneSettingsName = sceneName;
                var settings = ProjectSettings.Current.GetOrCreateSceneSettings(sceneName);
                string spawnIds = settings.PreferredSpawnPointIds != null && settings.PreferredSpawnPointIds.Count > 0
                    ? string.Join(", ", settings.PreferredSpawnPointIds)
                    : "";
                sb.Append("<details open><summary>Scene Settings</summary>");
                sb.Append($"<div class=\"property-row\" data-context=\"scene-settings-avatar\"><div class=\"property-name\">Avatar Pack Key</div><input type=\"text\" id=\"ss-avatarPackKey\" value=\"{settings.AvatarPackKey ?? ""}\"></div>");
                sb.Append($"<div class=\"property-row\" data-context=\"scene-settings-controller\"><div class=\"property-name\">Controller Type</div><input type=\"text\" id=\"ss-controllerTypeName\" value=\"{settings.ControllerTypeName ?? ""}\"></div>");
                sb.Append($"<div class=\"property-row\" data-context=\"scene-settings-spawns\"><div class=\"property-name\">Preferred Spawn IDs</div><input type=\"text\" id=\"ss-preferredSpawnPointIds\" value=\"{spawnIds}\"></div>");
                sb.Append($"<div class=\"property-row\" data-context=\"scene-settings-camera\"><div class=\"property-name\">Camera Mode</div><input type=\"text\" id=\"ss-cameraMode\" value=\"{settings.CameraMode ?? ""}\"></div>");
                sb.Append("</details>");
            }
            else
            {
                _activeSceneSettingsName = null;
            }

            sb.Append("<details open><summary>Properties</summary>");
            BuildObjectHtml(sb, obj, -1);
            sb.Append("</details>");
            return sb.ToString();
        }
        private void BuildObjectHtml(StringBuilder sb, object obj, int entityId)
        {
            if (obj == null) return;
            var type = obj.GetType();
            if (obj is SkyboxData skybox)
            {
                AppendEditableProperties(sb, skybox, entityId);
                if (skybox.Faces != null && skybox.Faces.Count > 0)
                {
                    sb.Append("<div class=\"property-row\" data-context=\"skybox-section\"><div class=\"property-name\" style=\"font-weight:bold;\">Faces (6 PNGs)</div></div>");
                    for (int i = 0; i < skybox.Faces.Count; i++)
                    {
                        string facePath = skybox.Faces[i] ?? "";
                        sb.Append($"<div class=\"property-row\" data-context=\"skybox-face\" data-index=\"{i}\"><div class=\"property-name\">Face {i}</div><input type=\"text\" value=\"{facePath}\" data-hook=\"SetSkyboxFace\" data-index=\"{i}\"></div>");
                    }
                }
                return;
            }
            if (obj is Entity ent && ent.GetComponent<ModelComponent>() is ModelComponent modelComp)
            {
                if (!string.IsNullOrEmpty(modelComp.Key))
                {
                    sb.Append($"<div class=\"property-row\" data-context=\"model-key\"><div class=\"property-name\">Asset Key</div><input type=\"text\" value=\"{modelComp.Key}\" readonly></div>");
                }
                if (modelComp.Material?.TextureSlots?.Count > 0)
                {
                    sb.Append("<div class=\"property-row\" data-context=\"texture-slots\"><div class=\"property-name\" style=\"font-weight:bold;\">TextureSlots</div></div>");
                    for (int i = 0; i < modelComp.Material.TextureSlots.Count; i++)
                    {
                        var slot = modelComp.Material.TextureSlots[i];
                        string slotName = string.IsNullOrEmpty(slot.SlotName) ? $"Slot {i}" : slot.SlotName;
                        sb.Append($"<div class=\"property-row\" data-context=\"texture-slot\" data-index=\"{i}\">");
                        sb.Append($"<div class=\"property-name\">{slotName}</div>");
                        sb.Append($"<select data-hook=\"SetTextureMapping\" data-entityid=\"{ent.Id}\" data-slotindex=\"{i}\" onchange=\"this.form.submit()\">");
                        foreach (TextureMappingMode mode in Enum.GetValues(typeof(TextureMappingMode)))
                        {
                            string selected = (mode == slot.MappingMode) ? " selected" : "";
                            sb.Append($"<option value=\"{(int)mode}\"{selected}>{mode}</option>");
                        }
                        sb.Append("</select>");
                        sb.Append("</div>");
                        sb.Append($"<div class=\"property-row\" data-context=\"texture-tiling\" data-index=\"{i}\"><div class=\"property-name\">Tiling</div><input type=\"text\" value=\"{slot.Tiling.X}, {slot.Tiling.Y}\" data-hook=\"SetTextureTiling\" data-entityid=\"{ent.Id}\" data-slotindex=\"{i}\" style=\"width:120px;\"></div>");
                    }
                }
                else
                {
                    sb.Append("<div class=\"property-row\"><i>No texture slots defined</i></div>");
                }
                return;
            }
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.Name);
            foreach (var prop in properties)
            {
                object value = prop.GetValue(obj);
                if (value == null) continue;
                var propType = prop.PropertyType;
                if (propType.IsPrimitive || propType == typeof(string) || propType == typeof(Vector2) || propType == typeof(Vector3))
                {
                    string display = value?.ToString() ?? "";
                    sb.Append($"<div class=\"property-row\" data-context=\"prop-{prop.Name}\">");
                    sb.Append($"<div class=\"property-name\">{prop.Name}</div>");
                    if (propType == typeof(bool))
                    {
                        bool checkedVal = (bool)value;
                        sb.Append($"<input type=\"checkbox\" {(checkedVal ? "checked" : "")} data-hook=\"SetComponentProperty\" data-entityid=\"{entityId}\" data-component=\"{type.Name}\" data-property=\"{prop.Name}\">");
                    }
                    else if (propType.IsEnum)
                    {
                        sb.Append($"<select data-hook=\"SetComponentProperty\" data-entityid=\"{entityId}\" data-component=\"{type.Name}\" data-property=\"{prop.Name}\">");
                        foreach (var enumVal in Enum.GetValues(propType))
                        {
                            string selected = enumVal.Equals(value) ? " selected" : "";
                            sb.Append($"<option value=\"{enumVal}\" {selected}>{enumVal}</option>");
                        }
                        sb.Append("</select>");
                    }
                    else
                    {
                        sb.Append($"<input type=\"text\" value=\"{display}\" data-hook=\"SetComponentProperty\" data-entityid=\"{entityId}\" data-component=\"{type.Name}\" data-property=\"{prop.Name}\">");
                    }
                    sb.Append("</div>");
                }
                else if (!propType.IsPrimitive && !propType.IsEnum && propType != typeof(string))
                {
                    sb.Append($"<div class=\"property-row\" data-context=\"nested-{prop.Name}\" style=\"font-weight:bold;\">{prop.Name}</div>");
                    BuildObjectHtml(sb, value, entityId);
                }
                else if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    sb.Append($"<div class=\"property-row\" data-context=\"list-{prop.Name}\"><div class=\"property-name\">{prop.Name}</div></div>");
                    var list = value as System.Collections.IList;
                    if (list != null)
                    {
                        sb.Append($"<div class=\"property-row\"><i>Count: {list.Count}</i></div>");
                    }
                }
            }
        }
        private void AppendEditableProperties(StringBuilder sb, object obj, int entityId)
        {
            if (obj == null) return;
            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.Name);
            foreach (var prop in properties)
            {
                if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType == typeof(Vector2) || prop.PropertyType == typeof(Vector3))
                {
                    object value = prop.GetValue(obj);
                    string display = value?.ToString() ?? "";
                    sb.Append($"<div class=\"property-row\" data-context=\"prop-{prop.Name}\">");
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
                        sb.Append($"<input type=\"text\" value=\"{display}\" data-hook=\"SetComponentProperty\" data-entityid=\"{entityId}\" data-component=\"{type.Name}\" data-property=\"{prop.Name}\">");
                    }
                    sb.Append("</div>");
                }
            }
        }
        public void HandleDataHook(string hook)
        {
            Console.WriteLine($"[PropertiesPanel] HandleDataHook: {hook}");
            // Capture any in-progress typing before we react to the hook.
            FlushSceneSettingsFromUI();

            if (hook.StartsWith("SetTextureMapping"))
            {
                Console.WriteLine("[PropertiesPanel] Texture mapping changed - full update coming in next iteration with JS payload");
                RebuildPropertiesUI();
                return;
            }
            if (hook == "SetComponentProperty")
            {
                Console.WriteLine("[PropertiesPanel] Generic component property update - live editing ready");
                RebuildPropertiesUI();
                return;
            }
            if (hook == "RotateSkybox")
            {
                _eventBus.Publish(new GenericEvent { Hook = "SkyboxRotatePreview" });
                RebuildPropertiesUI();
                return;
            }
        }
        public void HandleUIClick(HtmlElement elem)
        {
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