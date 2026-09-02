// Folder: ToolChest
// File: PropertiesPanel.cs
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.GPU;
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
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.Managers;
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
                bool handled = base.HandleUIClick(elem);
                _parent.HandleUIClick(elem);
                return handled;
            }
            public override void TriggerChange(HtmlElement elem)
            {
                base.TriggerChange(elem);
                if (elem is InputElement input)
                {
                    string id = input.Attributes.GetValueOrDefault("id", "");
                    if (id.StartsWith("ss-") || id.StartsWith("env-"))
                    {
                        _parent.FlushLiveSettings();
                        return;
                    }
                    string hook = input.Attributes.GetValueOrDefault("data-hook", "");
                    if (hook == "SetComponentProperty")
                    {
                        _parent.ApplyComponentPropertyFromInput(input);
                    }
                    if (hook == "ToggleMeshVisible")
                    {
                        _parent.ApplyMeshVisibleFromInput(input);
                    }
                }
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
                if (_parent._currentTarget is Entity entity && entity.GetComponent<SoundComponent>() != null)
                {
                    var items = new List<ContextMenuItem>
                    {
                        new ContextMenuItem("Play / Pause", "PlayPreviewSound")
                    };
                    ShowContextMenu(mousePos, items);
                    return true;
                }
                return false;
            }
        }
        private object _currentTarget;
        private string _activeSceneSettingsName;
        // Toggle state for Play / Pause
        private bool _previewIsPlaying;
        private int _previewEntityId = -1;
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
        public void FlushLiveSettings()
        {
            FlushSceneSettingsFromUI();
        }
        private void OnGenericEvent(GenericEvent e)
        {
            if (e.Hook == "OutlinerSelectionChanged")
            {
                FlushSceneSettingsFromUI();
                string nodeId = e.Data.GetValueOrDefault("nodeId", "");
                var provider = OutlinerCoordinator.Instance.GetLastActiveProvider();
                if (provider != null)
                {
                    _currentTarget = provider.GetObjectForNode(nodeId);
                    // Selection changed – reset toggle so next click always plays
                    _previewIsPlaying = false;
                    _previewEntityId = -1;
                    Console.WriteLine($"[PropertiesPanel] Selection changed - nodeId: {nodeId} | Target type: {_currentTarget?.GetType().FullName ?? "null"}");
                    RebuildPropertiesUI();
                }
            }
            else if (e.Hook == "SkyboxRotatePreview")
            {
                Console.WriteLine("[PropertiesPanel] SkyboxRotatePreview event received - forwarding to live preview");
            }
            else if (e.Hook == "FlushUIStateBeforeSave")
            {
                FlushSceneSettingsFromUI();
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
        private void FlushSceneSettingsFromUI()
        {
            if (_uiOverlay == null || string.IsNullOrEmpty(_activeSceneSettingsName)) return;
            var avatarElem = _uiOverlay.FindElementById("ss-avatarPackKey") as InputElement;
            var animationElem = _uiOverlay.FindElementById("ss-animationPackKey") as InputElement;
            var controllerElem = _uiOverlay.FindElementById("ss-controllerTypeName") as InputElement;
            var spawnsElem = _uiOverlay.FindElementById("ss-preferredSpawnPointIds") as InputElement;
            var cameraElem = _uiOverlay.FindElementById("ss-cameraMode") as InputElement;
            if (avatarElem == null && animationElem == null && controllerElem == null && spawnsElem == null && cameraElem == null)
                return;
            var settings = ProjectSettings.Current.GetOrCreateSceneSettings(_activeSceneSettingsName);
            if (settings == null) return;
            if (avatarElem != null)
                settings.AvatarPackKey = string.IsNullOrWhiteSpace(avatarElem.Value) ? null : avatarElem.Value.Trim();
            if (animationElem != null)
                settings.AnimationPackKey = string.IsNullOrWhiteSpace(animationElem.Value) ? null : animationElem.Value.Trim();
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
            FlushEnvironmentFromUI();
            Console.WriteLine($"[PropertiesPanel] Flushed Scene Settings for '{_activeSceneSettingsName}': Avatar={settings.AvatarPackKey}, Animation={settings.AnimationPackKey}, Controller={settings.ControllerTypeName}, Camera={settings.CameraMode}, Spawns=[{string.Join(",", settings.PreferredSpawnPointIds ?? new List<int>())}]");
        }

        private void FlushEnvironmentFromUI()
        {
            var level = ProjectSettings.Current.CurrentLevel;
            if (level == null || _uiOverlay == null) return;
            var env = level.Environment ?? new EnvironmentSettings();
            string ReadSelect(string id)
            {
                var el = _uiOverlay.FindElementById(id);
                if (el is InputElement input) return input.Value;
                return el?.Attributes.GetValueOrDefault("value", null);
            }
            string fogMode = ReadSelect("env-fogMode");
            string fogQuality = ReadSelect("env-fogQuality");
            string fogDensity = ReadSelect("env-fogDensity");
            string shadowQuality = ReadSelect("env-shadowQuality");
            string shadowDistance = ReadSelect("env-shadowDistance");
            if (fogMode == null && fogQuality == null && fogDensity == null && shadowQuality == null && shadowDistance == null)
                return;
            if (!string.IsNullOrWhiteSpace(fogMode)) env.FogMode = fogMode.Trim();
            if (!string.IsNullOrWhiteSpace(fogQuality)) env.FogQuality = fogQuality.Trim();
            if (float.TryParse(fogDensity, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float density))
                env.FogDensity = density;
            if (!string.IsNullOrWhiteSpace(shadowQuality)) env.ShadowQuality = shadowQuality.Trim();
            if (float.TryParse(shadowDistance, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dist) && dist > 1f)
                env.ShadowDistance = dist;
            level.Environment = env;
            var sceneData = ProjectSettings.Current.CurrentLevel;
            Console.WriteLine($"[PropertiesPanel] Flushed Environment: Fog={env.FogMode}/{env.FogQuality} density={env.FogDensity} Shadows={env.ShadowQuality} dist={env.ShadowDistance}");
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
                sb.Append($"<div class=\"property-row\" data-context=\"scene-settings-animation\"><div class=\"property-name\">Animation Pack Key</div><input type=\"text\" id=\"ss-animationPackKey\" value=\"{settings.AnimationPackKey ?? ""}\"></div>");
                sb.Append($"<div class=\"property-row\" data-context=\"scene-settings-controller\"><div class=\"property-name\">Controller Type</div><input type=\"text\" id=\"ss-controllerTypeName\" value=\"{settings.ControllerTypeName ?? ""}\"></div>");
                sb.Append($"<div class=\"property-row\" data-context=\"scene-settings-spawns\"><div class=\"property-name\">Preferred Spawn IDs</div><input type=\"text\" id=\"ss-preferredSpawnPointIds\" value=\"{spawnIds}\"></div>");
                sb.Append($"<div class=\"property-row\" data-context=\"scene-settings-camera\"><div class=\"property-name\">Camera Mode</div><input type=\"text\" id=\"ss-cameraMode\" value=\"{settings.CameraMode ?? ""}\"></div>");
                sb.Append("</details>");
                var env = level.Environment ?? new EnvironmentSettings();
                level.Environment = env;
                sb.Append("<details open><summary>Environment / Lighting</summary>");
                sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Fog Mode</div><select id=\"env-fogMode\"><option{(env.FogMode == "Off" ? " selected" : "")}>Off</option><option{(env.FogMode == "Exponential" ? " selected" : "")}>Exponential</option><option{(env.FogMode == "Height" ? " selected" : "")}>Height</option><option{(env.FogMode == "Volumetric" ? " selected" : "")}>Volumetric</option></select></div>");
                sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Fog Quality</div><select id=\"env-fogQuality\"><option{(env.FogQuality == "Off" ? " selected" : "")}>Off</option><option{(env.FogQuality == "Low" ? " selected" : "")}>Low</option><option{(env.FogQuality == "Medium" ? " selected" : "")}>Medium</option><option{(env.FogQuality == "High" ? " selected" : "")}>High</option></select></div>");
                sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Fog Density</div><input type=\"text\" id=\"env-fogDensity\" value=\"{env.FogDensity.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"></div>");
                sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Shadow Quality</div><select id=\"env-shadowQuality\"><option{(env.ShadowQuality == "Off" ? " selected" : "")}>Off</option><option{(env.ShadowQuality == "Low" ? " selected" : "")}>Low</option><option{(env.ShadowQuality == "Medium" ? " selected" : "")}>Medium</option><option{(env.ShadowQuality == "High" ? " selected" : "")}>High</option><option{(env.ShadowQuality == "Ultra" ? " selected" : "")}>Ultra</option></select></div>");
                sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Shadow Distance</div><input type=\"text\" id=\"env-shadowDistance\" value=\"{env.ShadowDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"></div>");
                sb.Append("<div class=\"property-row\"><div class=\"property-name\">Note</div><div>Editor lighting comes from placed Light entities. Play Game uses a 3 o'clock sun if none exist.</div></div>");
                sb.Append("</details>");
            }
            else
            {
                _activeSceneSettingsName = null;
            }
            if (obj is Entity ent)
            {
                var physics = ent.GetComponent<PhysicsComponent>();
                if (physics != null)
                {
                    sb.Append("<details open><summary>Physics</summary>");
                    AppendEditableProperties(sb, physics, ent.Id);
                    sb.Append("</details>");
                }
                var modelComp = ent.GetComponent<ModelComponent>();
                if (modelComp != null)
                {
                    sb.Append("<details open><summary>Model</summary>");
                    if (!string.IsNullOrEmpty(modelComp.Key))
                    {
                        sb.Append($"<div class=\"property-row\" data-context=\"model-key\"><div class=\"property-name\">Asset Key</div><input type=\"text\" value=\"{modelComp.Key}\" readonly></div>");
                    }
                    int meshCount = 0;
                    if (ModelManager.Instance != null && !string.IsNullOrEmpty(modelComp.Key)
                        && ModelManager.Instance.TryGetModelData(modelComp.Key, out var modelData)
                        && modelData?.MeshRenders != null)
                    {
                        meshCount = modelData.MeshRenders.Count;
                    }
                    else if (modelComp.Model?.Meshes != null)
                    {
                        meshCount = modelComp.Model.Meshes.Count;
                    }
                    if (meshCount > 0)
                    {
                        sb.Append("<div class=\"property-row\"><div class=\"property-name\" style=\"font-weight:bold;\">Meshes</div></div>");
                        for (int mi = 0; mi < meshCount; mi++)
                        {
                            bool hidden = modelComp.IsMeshHidden(mi);
                            string chk = hidden ? "" : " checked";
                            sb.Append($"<div class=\"property-row\" data-context=\"mesh-row\" data-index=\"{mi}\">");
                            sb.Append($"<div class=\"property-name\">Mesh {mi}</div>");
                            sb.Append($"<input type=\"checkbox\"{chk} data-hook=\"ToggleMeshVisible\" data-entityid=\"{ent.Id}\" data-index=\"{mi}\">");
                            sb.Append("</div>");
                        }
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
                            sb.Append($"<select data-hook=\"SetTextureMapping\" data-entityid=\"{ent.Id}\" data-slotindex=\"{i}\">");
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
                    sb.Append("</details>");
                }
                var soundComp = ent.GetComponent<SoundComponent>();
                if (soundComp != null)
                {
                    sb.Append("<details open><summary>Sound</summary>");
                    AppendEditableProperties(sb, soundComp, ent.Id);
                    sb.Append("</details>");
                }
                var lightComp = ent.GetComponent<LightComponent>();
                if (lightComp != null)
                {
                    sb.Append("<details open><summary>Light</summary>");
                    AppendEditableProperties(sb, lightComp, ent.Id);
                    sb.Append("</details>");
                }
                var modelShadows = ent.GetComponent<ModelComponent>();
                if (modelShadows != null)
                {
                    sb.Append("<details open><summary>Shadows</summary>");
                    sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Cast Shadows</div><input type=\"checkbox\" {(modelShadows.CastShadows ? "checked" : "")} data-hook=\"SetComponentProperty\" data-entityid=\"{ent.Id}\" data-component=\"ModelComponent\" data-property=\"CastShadows\"></div>");
                    sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Receive Shadows</div><input type=\"checkbox\" {(modelShadows.ReceiveShadows ? "checked" : "")} data-hook=\"SetComponentProperty\" data-entityid=\"{ent.Id}\" data-component=\"ModelComponent\" data-property=\"ReceiveShadows\"></div>");
                    sb.Append("</details>");
                }
                return sb.ToString();
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
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.Name);
            foreach (var prop in properties)
            {
                object value = prop.GetValue(obj);
                var propType = prop.PropertyType;
                if (value == null && propType != typeof(string))
                    continue;
                if (propType.IsPrimitive || propType == typeof(string) || propType == typeof(Vector2) || propType == typeof(Vector3) || propType.IsEnum)
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
                            sb.Append($"<option value=\"{enumVal}\"{selected}>{enumVal}</option>");
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
                object value = prop.GetValue(obj);
                var propType = prop.PropertyType;
                if (value == null && propType != typeof(string))
                    continue;
                if (propType.IsPrimitive || propType == typeof(string) || propType == typeof(Vector2) || propType == typeof(Vector3) || propType.IsEnum)
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
                            sb.Append($"<option value=\"{enumVal}\"{selected}>{enumVal}</option>");
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
        public void ApplyComponentPropertyFromInput(InputElement input)
        {
            if (input == null) return;
            string entityIdStr = input.Attributes.GetValueOrDefault("data-entityid", "-1");
            if (!int.TryParse(entityIdStr, out int entityId) || entityId <= 0) return;
            string componentName = input.Attributes.GetValueOrDefault("data-component", "");
            string propertyName = input.Attributes.GetValueOrDefault("data-property", "");
            if (string.IsNullOrEmpty(componentName) || string.IsNullOrEmpty(propertyName)) return;

            // Checkboxes report state via Checked, not Value (Value is often the static "on"/empty).
            string newValue = input.Value ?? "";
            if (string.Equals(input.Attributes.GetValueOrDefault("type", ""), "checkbox", StringComparison.OrdinalIgnoreCase))
            {
                newValue = input.Checked ? "true" : "false";
            }

            ApplyPropertyToEntity(entityId, componentName, propertyName, newValue);
        }
        private void ApplyComponentPropertyChange(HtmlElement elem)
        {
            if (elem == null) return;
            SelectElement select = elem as SelectElement;
            if (select == null && elem.Tag == "option")
                select = elem.Parent as SelectElement;
            if (select == null) return;
            string entityIdStr = select.Attributes.GetValueOrDefault("data-entityid", "-1");
            if (!int.TryParse(entityIdStr, out int entityId) || entityId <= 0) return;
            string componentName = select.Attributes.GetValueOrDefault("data-component", "");
            string propertyName = select.Attributes.GetValueOrDefault("data-property", "");
            if (string.IsNullOrEmpty(componentName) || string.IsNullOrEmpty(propertyName)) return;
            string newValue = select.Value;
            if (string.IsNullOrEmpty(newValue)) return;
            ApplyPropertyToEntity(entityId, componentName, propertyName, newValue);
        }
        private void ApplyPropertyToEntity(int entityId, string componentName, string propertyName, string newValue)
        {
            var level = ProjectSettings.Current.CurrentLevel;
            if (level == null) return;
            var entity = level.Entities.FirstOrDefault(e => e.Id == entityId);
            if (entity == null) return;
            object target = null;
            if (componentName == "PhysicsComponent" || componentName == "Physics")
            {
                target = entity.GetComponent<PhysicsComponent>();
            }
            else if (componentName == "SoundComponent" || componentName == "Sound")
            {
                target = entity.GetComponent<SoundComponent>();
            }
            else
            {
                foreach (var kvp in entity.Components)
                {
                    if (kvp.Key.Name == componentName || kvp.Key.FullName == componentName)
                    {
                        target = kvp.Value;
                        break;
                    }
                }
            }
            if (target == null) return;
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) return;
            try
            {
                object converted = ConvertPropertyValue(prop.PropertyType, newValue);
                if (converted != null || prop.PropertyType == typeof(string))
                {
                    prop.SetValue(target, converted);
                    Console.WriteLine($"[PropertiesPanel] Applied {componentName}.{propertyName} = {newValue} on entity {entityId}");
                    if (target is PhysicsComponent physics)
                    {
                        physics.IsSleeping = false;
                        physics.SleepTimer = 0f;
                        physics.InvalidateShape();
                        var modelComp = entity.GetComponent<ModelComponent>();
                        physics.RebuildShape(modelComp?.Model);
                        Console.WriteLine($"[PropertiesPanel] Rebuilt physics shape/mass for entity {entityId} (BodyType={physics.BodyType}, InvMass={physics.InvMass}, RollingResistance={physics.RollingResistance})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PropertiesPanel] Failed to set {componentName}.{propertyName}: {ex.Message}");
            }
        }
        private static object ConvertPropertyValue(Type targetType, string raw)
        {
            if (targetType == typeof(string)) return raw;
            if (targetType == typeof(bool))
            {
                if (bool.TryParse(raw, out bool b)) return b;
                return raw == "on" || raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
            if (targetType.IsEnum)
            {
                if (Enum.TryParse(targetType, raw, true, out object enumVal)) return enumVal;
                if (int.TryParse(raw, out int enumInt)) return Enum.ToObject(targetType, enumInt);
                return null;
            }
            if (targetType == typeof(int))
            {
                if (int.TryParse(raw, out int i)) return i;
                return null;
            }
            if (targetType == typeof(float))
            {
                if (float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) return f;
                return null;
            }
            if (targetType == typeof(double))
            {
                if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d)) return d;
                return null;
            }
            if (targetType == typeof(Vector2))
            {
                var parts = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2
                    && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
                    return new Vector2(x, y);
                return null;
            }
            if (targetType == typeof(Vector3))
            {
                var parts = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3
                    && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
                    && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
                    return new Vector3(x, y, z);
                return null;
            }
            return null;
        }
        public void ApplyMeshVisibleFromInput(InputElement input)
        {
            if (input == null) return;
            string entityIdStr = input.Attributes.GetValueOrDefault("data-entityid", "-1");
            string indexStr = input.Attributes.GetValueOrDefault("data-index", "-1");
            if (!int.TryParse(entityIdStr, out int entityId) || entityId <= 0) return;
            if (!int.TryParse(indexStr, out int meshIndex) || meshIndex < 0) return;
            bool visible = input.Checked;
            var level = ProjectSettings.Current.CurrentLevel;
            var entity = level?.Entities.FirstOrDefault(e => e.Id == entityId);
            var modelComp = entity?.GetComponent<ModelComponent>();
            if (modelComp == null) return;
            modelComp.SetMeshHidden(meshIndex, !visible);
            Console.WriteLine($"[PropertiesPanel] ToggleMeshVisible index={meshIndex} visible={visible} hidden=[{string.Join(",", modelComp.HiddenMeshIndices)}]");
        }

        public void HandleDataHook(string hook)
        {
            Console.WriteLine($"[PropertiesPanel] HandleDataHook: {hook}");
            FlushSceneSettingsFromUI();
            if (hook.StartsWith("SetTextureMapping"))
            {
                RebuildPropertiesUI();
                return;
            }
            if (hook == "SetComponentProperty")
            {
                return;
            }
            if (hook == "RotateSkybox")
            {
                SkyboxRotatePanel.Open(_renderContext, _controlContext, _window, _eventBus);
                return;
            }
            if (hook == "PlayPreviewSound")
            {
                if (_currentTarget is Entity entity)
                {
                    var soundComp = entity.GetComponent<SoundComponent>();
                    var physics = entity.GetComponent<PhysicsComponent>();
                    if (soundComp != null && physics != null)
                    {
                        // TOGGLE: if already playing this exact entity → STOP
                        if (_previewIsPlaying && _previewEntityId == entity.Id)
                        {
                            _eventBus.Publish(new GenericEvent { Hook = "StopSoundPreview" });
                            _previewIsPlaying = false;
                            _previewEntityId = -1;
                            Console.WriteLine($"[PropertiesPanel] StopSoundPreview for entity {entity.Id}");
                        }
                        else
                        {
                            // START
                            var emission = new SoundEmissionEvent
                            {
                                Source = new SoundSource
                                {
                                    EntityId = entity.Id,
                                    Position = physics.Position,
                                    Type = soundComp.Type ?? "SoundSource",
                                    IsSensitive = false,
                                    AudioClip = soundComp.AudioClip ?? "",
                                    SteamId = 0
                                }
                            };
                            _eventBus.Publish(emission);
                            _previewIsPlaying = true;
                            _previewEntityId = entity.Id;
                            Console.WriteLine($"[PropertiesPanel] PlayPreviewSound emitted for entity {entity.Id} at {physics.Position}");
                        }
                    }
                }
                return;
            }
        }
        public void HandleUIClick(HtmlElement elem)
        {
            if (elem == null) return;
            if (elem.Tag != "option") return;
            var select = elem.Parent as SelectElement;
            if (select != null && select.Attributes.GetValueOrDefault("data-hook", "") == "SetComponentProperty")
            {
                ApplyComponentPropertyChange(select);
                RebuildPropertiesUI();
            }
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