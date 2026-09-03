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
using SiegeEngine.Scenes;
using ReadingChamber;
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
                        if (!_parent._rebuildingUI)
                            _parent.ApplyMeshVisibleFromInput(input);
                    }
                    if (hook == "SetMaterialOpacity" || hook == "SetMaterialOption")
                    {
                        // TriggerChange for text now only fires on blur/Enter (see UIInteractionLayer).
                        // Treat that as a commit so an emptied field actually clears the mask.
                        if (!_parent._ignoreMaterialInput)
                            _parent.ApplyMaterialOpacityFromInput(input, allowClear: true);
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
                HtmlElement pathElem = sourceElement;
                string matCtx = pathElem.Attributes.GetValueOrDefault("data-context", "");
                if (matCtx != "mat-opt-path" && pathElem.Parent != null)
                {
                    pathElem = pathElem.Parent;
                    matCtx = pathElem.Attributes.GetValueOrDefault("data-context", "");
                }
                if (matCtx == "mat-opt-path")
                {
                    HtmlElement attrs = sourceElement;
                    if (!attrs.Attributes.ContainsKey("data-mesh") && !attrs.Attributes.ContainsKey("data-index") && sourceElement.Parent != null)
                        attrs = sourceElement.Parent;
                    if (!int.TryParse(attrs.Attributes.GetValueOrDefault("data-entityid", "-1"), out int entityId))
                        entityId = -1;
                    if (!int.TryParse(attrs.Attributes.GetValueOrDefault("data-mesh", attrs.Attributes.GetValueOrDefault("data-index", "-1")), out int meshIndex))
                        meshIndex = -1;
                    if (!int.TryParse(attrs.Attributes.GetValueOrDefault("data-mat", "0"), out int matIndex))
                        matIndex = 0;
                    _parent.StashOpacityBrowseTarget(entityId, meshIndex, matIndex);
                    ShowContextMenu(mousePos, new List<ContextMenuItem>
                    {
                        new ContextMenuItem("Browse...", "BrowseMaterialPath")
                    });
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
        private int _browseEntityId = -1;
        private int _browseMesh = -1;
        private int _browseMat = 0;
        private bool _pendingOpacityBrowse;
        private bool _allowEmptyOpacityClear;
        private bool _ignoreMaterialInput;
        private bool _rebuildingUI;
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
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
        }
        protected override UIOverlay CreateUIOverlay()
        {
            return new PropertiesUIOverlay(this, _renderContext, _controlContext, _window);
        }
        public override void Init()
        {
            base.Init();
            chrome.close_color = new Vector4(0.486f, 1.0f, 0.796f, 1.0f);
            BindProjectTexturesDirectory();
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
                    int selId = -1;
                    if (_currentTarget is Entity selEnt) selId = selEnt.Id;
                    else if (_currentTarget is MeshLayerRef selRef) selId = selRef.EntityId;
                    BindProjectTexturesDirectory();
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
                FlushMaterialOptionsFromUI();
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
            _ignoreMaterialInput = true;
            _rebuildingUI = true;
            try
            {
                _uiOverlay.LoadUI(finalHtml);
                _uiOverlay.PanelWidth = Size.X;
                _uiOverlay.PanelHeight = Size.Y;
                _uiOverlay.RefreshUI();
            }
            finally
            {
                _ignoreMaterialInput = false;
                _rebuildingUI = false;
            }
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
            if (obj is MeshLayerRef meshRef)
            {
                AppendMeshLayerHtml(sb, meshRef);
                sb.Append("</details>");
                return sb.ToString();
            }
            if (obj is ModelViewerScene viewer)
            {
                AppendViewerMeshesHtml(sb, viewer);
                sb.Append("</details>");
                return sb.ToString();
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
                            var meshMats = GetEntityMeshMaterials(modelComp, mi);
                            if (meshMats != null && meshMats.Count > 0)
                            {
                                for (int m = 0; m < meshMats.Count; m++)
                                    AppendMaterialBlock(sb, meshMats[m], $"Mesh {mi} Material {m}", ent.Id, mi, m);
                            }
                            else
                            {
                                AppendMaterialBlock(sb, null, $"Mesh {mi} Material 0", ent.Id, mi, 0);
                            }
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
            if (!int.TryParse(entityIdStr, out int entityId)) entityId = -1;
            if (!int.TryParse(indexStr, out int meshIndex) || meshIndex < 0) return;
            bool visible = input.Checked;
            if (entityId > 0)
            {
                int applied = 0;
                foreach (var modelComp in EnumerateModelComponents(entityId))
                {
                    modelComp.SetMeshHidden(meshIndex, !visible);
                    applied++;
                    Console.WriteLine($"[PropertiesPanel] ToggleMeshVisible entity={entityId} index={meshIndex} visible={visible} hidden=[{string.Join(",", modelComp.HiddenMeshIndices)}]");
                }
                if (applied == 0)
                    Console.WriteLine($"[PropertiesPanel] ToggleMeshVisible missed live/blueprint entity={entityId} index={meshIndex}");
                return;
            }
            ModelViewerScene viewer = ResolveViewer();
            if (viewer == null) return;
            viewer.SetMeshHidden(meshIndex, !visible);
            Console.WriteLine($"[PropertiesPanel] ToggleMeshVisible viewer index={meshIndex} visible={visible} hidden=[{string.Join(",", viewer.HiddenMeshIndices)}]");
        }

        private void AppendMeshLayerHtml(StringBuilder sb, MeshLayerRef meshRef)
        {
            sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Mesh</div><input type=\"text\" value=\"{meshRef.Label ?? ("Mesh " + meshRef.MeshIndex)}\" readonly></div>");
            bool hidden = false;
            int entityId = meshRef.EntityId;
            if (meshRef.Entity != null)
            {
                var modelComp = meshRef.Entity.GetComponent<ModelComponent>();
                hidden = modelComp != null && modelComp.IsMeshHidden(meshRef.MeshIndex);
                entityId = meshRef.Entity.Id;
            }
            else if (meshRef.Viewer != null)
            {
                hidden = meshRef.Viewer.HiddenMeshIndices != null && meshRef.Viewer.HiddenMeshIndices.Contains(meshRef.MeshIndex);
            }
            string chk = hidden ? "" : " checked";
            sb.Append($"<div class=\"property-row\" data-context=\"mesh-row\" data-index=\"{meshRef.MeshIndex}\">");
            sb.Append($"<div class=\"property-name\">Visible</div>");
            sb.Append($"<input type=\"checkbox\"{chk} data-hook=\"ToggleMeshVisible\" data-entityid=\"{entityId}\" data-index=\"{meshRef.MeshIndex}\">");
            sb.Append("</div>");
            var mats = GetMaterialsForLayer(meshRef);
            if (mats == null || mats.Count == 0)
                AppendMaterialBlock(sb, null, "Material 0", entityId, meshRef.MeshIndex, 0);
            else
            {
                for (int i = 0; i < mats.Count; i++)
                    AppendMaterialBlock(sb, mats[i], $"Material {i}", entityId, meshRef.MeshIndex, i);
            }
        }

        private void AppendViewerMeshesHtml(StringBuilder sb, ModelViewerScene viewer)
        {
            int count = viewer.GetMeshCount();
            sb.Append($"<div class=\"property-row\"><div class=\"property-name\">Meshes</div><input type=\"text\" value=\"{count}\" readonly></div>");
            for (int mi = 0; mi < count; mi++)
            {
                bool hidden = viewer.HiddenMeshIndices != null && viewer.HiddenMeshIndices.Contains(mi);
                string chk = hidden ? "" : " checked";
                sb.Append($"<div class=\"property-row\" data-context=\"mesh-row\" data-index=\"{mi}\">");
                sb.Append($"<div class=\"property-name\">Mesh {mi}</div>");
                sb.Append($"<input type=\"checkbox\"{chk} data-hook=\"ToggleMeshVisible\" data-entityid=\"-1\" data-index=\"{mi}\">");
                sb.Append("</div>");
                var mats = viewer.GetMeshMaterials(mi);
                if (mats != null && mats.Count > 0)
                {
                    for (int m = 0; m < mats.Count; m++)
                        AppendMaterialBlock(sb, mats[m], $"Mesh {mi} Material {m}", -1, mi, m);
                }
                else
                {
                    AppendMaterialBlock(sb, null, $"Mesh {mi} Material 0", -1, mi, 0);
                }
            }
        }

        private static MeshData GetRenderableMesh(FBXModel model, int gpuIndex)
        {
            if (model?.Meshes == null || gpuIndex < 0) return null;
            int i = 0;
            for (int m = 0; m < model.Meshes.Count; m++)
            {
                var mesh = model.Meshes[m];
                if (mesh == null || mesh.Indices == null || mesh.Indices.Count == 0) continue;
                if (i == gpuIndex) return mesh;
                i++;
            }
            return null;
        }

        private static List<Material> GetEntityMeshMaterials(ModelComponent modelComp, int meshIndex)
        {
            var model = modelComp?.Model;
            if (model == null && modelComp != null && !string.IsNullOrEmpty(modelComp.Key) && ModelManager.Instance != null)
                ModelManager.Instance.TryGetModel(modelComp.Key, out model);
            var mesh = GetRenderableMesh(model, meshIndex);
            return mesh?.Materials;
        }

        private static List<Material> GetMaterialsForLayer(MeshLayerRef meshRef)
        {
            if (meshRef.Viewer != null)
                return meshRef.Viewer.GetMeshMaterials(meshRef.MeshIndex)?.ToList();
            if (meshRef.Entity != null)
                return GetEntityMeshMaterials(meshRef.Entity.GetComponent<ModelComponent>(), meshRef.MeshIndex);
            return null;
        }

        private void AppendMaterialBlock(StringBuilder sb, Material mat, string heading, int entityId, int meshIndex, int matIndex)
        {
            string name = mat == null || string.IsNullOrEmpty(mat.Name) ? heading : mat.Name;
            sb.Append($"<div class=\"property-row\"><div class=\"property-name\">{heading}</div><input type=\"text\" value=\"{name}\" readonly></div>");
            if (mat?.TextureSlots != null && mat.TextureSlots.Count > 0)
            {
                for (int i = 0; i < mat.TextureSlots.Count; i++)
                {
                    var slot = mat.TextureSlots[i];
                    string slotName = string.IsNullOrEmpty(slot.SlotName) ? $"Slot {i}" : slot.SlotName;
                    string path = slot.TexturePath ?? "";
                    sb.Append($"<div class=\"property-row\"><div class=\"property-name\">{slotName}</div><input type=\"text\" value=\"{path}\" readonly></div>");
                }
            }
            var opt = FindMaterialOption(entityId, meshIndex, matIndex);
            string opacity = opt?.OpacityPath ?? "";
            sb.Append($"<div class=\"property-row\" data-context=\"mat-opt-path\" data-entityid=\"{entityId}\" data-mesh=\"{meshIndex}\" data-mat=\"{matIndex}\">");
            sb.Append($"<div class=\"property-name\">Opacity</div>");
            sb.Append($"<input type=\"text\" value=\"{opacity}\" placeholder=\"\" data-hook=\"SetMaterialOption\" data-context=\"mat-opt-path\" data-entityid=\"{entityId}\" data-mesh=\"{meshIndex}\" data-index=\"{meshIndex}\" data-mat=\"{matIndex}\">");
            sb.Append("</div>");
        }

        private MeshMaterialOption FindMaterialOption(int entityId, int meshIndex, int matIndex)
        {
            var list = GetOptionsList(entityId);
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o != null && o.MeshIndex == meshIndex && o.MaterialIndex == matIndex)
                    return o;
            }
            return null;
        }

        private List<ModelComponent> EnumerateModelComponents(int entityId)
        {
            var list = new List<ModelComponent>();
            void Add(ModelComponent mc)
            {
                if (mc != null && !list.Contains(mc))
                    list.Add(mc);
            }
            if (_currentTarget is Entity liveEnt && (entityId <= 0 || liveEnt.Id == entityId))
                Add(liveEnt.GetComponent<ModelComponent>());
            if (_currentTarget is MeshLayerRef meshRef && meshRef.Entity != null && (entityId <= 0 || meshRef.Entity.Id == entityId))
                Add(meshRef.Entity.GetComponent<ModelComponent>());
            if (entityId > 0)
            {
                var level = ProjectSettings.Current?.CurrentLevel;
                var blueprint = level?.Entities?.FirstOrDefault(e => e.Id == entityId);
                Add(blueprint?.GetComponent<ModelComponent>());
            }
            return list;
        }

        private List<MeshMaterialOption> GetOptionsList(int entityId)
        {
            if (entityId > 0)
            {
                // Prefer the live viewport entity so the first browse/toggle is visible
                // without waiting for a reload. Also keep the blueprint list in sync.
                var comps = EnumerateModelComponents(entityId);
                List<MeshMaterialOption> primary = null;
                for (int i = 0; i < comps.Count; i++)
                {
                    if (comps[i].MaterialOptions == null)
                        comps[i].MaterialOptions = new List<MeshMaterialOption>();
                    if (primary == null)
                        primary = comps[i].MaterialOptions;
                    else if (!object.ReferenceEquals(primary, comps[i].MaterialOptions))
                    {
                        // Share one list so live + blueprint stay identical.
                        comps[i].MaterialOptions = primary;
                    }
                }
                return primary;
            }
            ModelViewerScene viewer = ResolveViewer();
            if (viewer == null) return null;
            return viewer.MaterialOptions;
        }

        private ModelViewerScene ResolveViewer()
        {
            if (_currentTarget is ModelViewerScene v) return v;
            if (_currentTarget is MeshLayerRef meshRef && meshRef.Viewer != null) return meshRef.Viewer;
            if (_browseViewer != null) return _browseViewer;
            return null;
        }

        private MeshMaterialOption GetOrCreateOption(int entityId, int meshIndex, int matIndex)
        {
            var list = GetOptionsList(entityId);
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o != null && o.MeshIndex == meshIndex && o.MaterialIndex == matIndex)
                    return o;
            }
            var created = new MeshMaterialOption { MeshIndex = meshIndex, MaterialIndex = matIndex, OpacityPath = "" };
            list.Add(created);
            return created;
        }

        private ModelViewerScene _browseViewer;

        public void StashOpacityBrowseTarget(int entityId, int meshIndex, int matIndex)
        {
            _browseEntityId = entityId;
            _browseMesh = meshIndex;
            _browseMat = matIndex;
            _browseViewer = ResolveViewer();
        }

        public void ApplyMaterialOpacityFromInput(InputElement input, bool allowClear = false)
        {
            if (input == null) return;
            if (!int.TryParse(input.Attributes.GetValueOrDefault("data-entityid", "-1"), out int entityId)) entityId = -1;
            string meshStr = input.Attributes.GetValueOrDefault("data-mesh", input.Attributes.GetValueOrDefault("data-index", "-1"));
            if (!int.TryParse(meshStr, out int meshIndex) || meshIndex < 0) return;
            if (!int.TryParse(input.Attributes.GetValueOrDefault("data-mat", "0"), out int matIndex)) matIndex = 0;
            string raw = input.Value ?? "";
            if (string.IsNullOrWhiteSpace(raw) && !allowClear)
                return;
            ApplyOpacityPath(entityId, meshIndex, matIndex, raw, allowClear);
        }

        private void ApplyOpacityPath(int entityId, int meshIndex, int matIndex, string path, bool allowClear = false)
        {
            path = (path ?? "").Trim();
            BindProjectTexturesDirectory(entityId);
            if (string.IsNullOrWhiteSpace(path))
            {
                if (!allowClear && !_allowEmptyOpacityClear)
                    return;
                var existing = FindMaterialOption(entityId, meshIndex, matIndex);
                if (existing != null)
                    existing.OpacityPath = "";
                ModelViewerScene emptyViewer = ResolveViewer();
                if (entityId <= 0 && emptyViewer != null)
                    emptyViewer.SaveMaterialOptionsSidecar();
                WriteOpacityIntoMeshPack(entityId, meshIndex, matIndex, "");
                SyncOpacityInputValue(entityId, meshIndex, matIndex, "");
                return;
            }
            var opt = GetOrCreateOption(entityId, meshIndex, matIndex);
            if (opt == null)
                return;
            string stored = ImportOpacityIntoProject(path, entityId);
            bool changed = !string.Equals(opt.OpacityPath ?? "", stored ?? "", StringComparison.Ordinal);
            opt.OpacityPath = stored;
            ModelViewerScene viewer = ResolveViewer();
            if (entityId <= 0 && viewer != null)
                viewer.SaveMaterialOptionsSidecar();
            WriteOpacityIntoMeshPack(entityId, meshIndex, matIndex, opt.OpacityPath);
            string modelKey = ResolveModelKey(entityId);
            SiegeEngine.Core.GPU.Renderers.ModelRenderer.PreloadOpacity(stored, modelKey);
            // Update the existing field in place. Rebuilding here ClearFocus()s the input
            // and races EditorHistory Backspace into deleting the selected entity.
            if (changed)
                SyncOpacityInputValue(entityId, meshIndex, matIndex, opt.OpacityPath);
        }

        private string ProjectTexturesDirectory(int entityId = -1)
        {
            string project = ProjectSettings.Current?.ActiveProject;
            if (!string.IsNullOrEmpty(project))
                return Path.Combine(project, "Textures");

            string fbxDir = TryResolveFbxDirectoryFromContext(entityId);
            if (!string.IsNullOrEmpty(fbxDir))
                return Path.GetFullPath(Path.Combine(fbxDir, "..", "..", "Textures"));

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Textures");
        }

        private string TryResolveFbxDirectoryFromContext(int entityId)
        {
            try
            {
                if (entityId > 0)
                {
                    var level = ProjectSettings.Current?.CurrentLevel;
                    var entity = level?.Entities.FirstOrDefault(e => e.Id == entityId);
                    string key = entity?.GetComponent<ModelComponent>()?.Key;
                    string dir = null;
                    if (!string.IsNullOrEmpty(key) && ModelManager.Instance != null
                        && ModelManager.Instance.TryGetFbxDirectory(key, out dir)
                        && !string.IsNullOrEmpty(dir))
                        return dir;
                }
                ModelViewerScene viewer = ResolveViewer();
                string meshPath = viewer?.MeshPath;
                if (!string.IsNullOrEmpty(meshPath))
                    return Path.GetDirectoryName(meshPath);
            }
            catch { }
            return null;
        }

        private void BindProjectTexturesDirectory(int entityId = -1)
        {
            string dir = ProjectTexturesDirectory(entityId);
            Directory.CreateDirectory(dir);
            SiegeEngine.Core.GPU.Renderers.ModelRenderer.ProjectTexturesDirectory = dir;
        }

        // Copy the file we already have a handle for into {project}/Textures
        // and return the pack-relative reference used by FBX albedo slots.
        // No directory search — the incoming path IS the handle.
        private string ImportOpacityIntoProject(string handle, int entityId = -1)
        {
            BindProjectTexturesDirectory(entityId);
            string destDir = ProjectTexturesDirectory(entityId);
            Directory.CreateDirectory(destDir);

            string normalized = (handle ?? "").Trim().Replace('\\', '/');
            string fileName = Path.GetFileName(normalized.Replace('/', Path.DirectorySeparatorChar));
            string dest = string.IsNullOrEmpty(fileName) ? null : Path.Combine(destDir, fileName);
            string packRef = string.IsNullOrEmpty(fileName) ? normalized : "../../Textures/" + fileName;

            // Already the project handle and the file is sitting in Textures.
            if (!string.IsNullOrEmpty(dest) && File.Exists(dest)
                && (normalized.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalized, fileName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFullPath(normalized), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)))
            {
                SiegeEngine.Core.GPU.Renderers.ModelRenderer.LastImportedOpacityAbsolute = Path.GetFullPath(dest);
                return packRef;
            }

            string source = null;
            if (File.Exists(handle))
                source = handle;
            else
            {
                try
                {
                    string full = Path.GetFullPath(handle);
                    if (File.Exists(full))
                        source = full;
                }
                catch { }
            }

            if (string.IsNullOrEmpty(source) || !File.Exists(source))
            {
                // Already-imported handle with the file sitting in Textures.
                if (!string.IsNullOrEmpty(dest) && File.Exists(dest))
                    return packRef;
                return normalized;
            }

            string destName = Path.GetFileName(source);
            dest = Path.Combine(destDir, destName);
            try
            {
                string srcFull = Path.GetFullPath(source);
                string destFull = Path.GetFullPath(dest);
                if (!string.Equals(srcFull, destFull, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(srcFull, destFull, overwrite: true);
                }
                SiegeEngine.Core.GPU.Renderers.ModelRenderer.LastImportedOpacityAbsolute = destFull;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PropertiesPanel] Failed to import opacity into Textures: {ex.Message}");
                return handle.Replace('\\', '/');
            }

            return "../../Textures/" + destName;
        }

        private void FlushMaterialOptionsFromUI()
        {
            if (_uiOverlay == null) return;
            var inputs = _uiOverlay.FindElementsByTag("input");
            if (inputs == null) return;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inputs[i] is InputElement input)
                {
                    string hook = input.Attributes.GetValueOrDefault("data-hook", "");
                    if (hook == "SetMaterialOption" || hook == "SetMaterialOpacity")
                        ApplyMaterialOpacityFromInput(input, allowClear: true);
                }
            }
        }

        private void OnFileSelected(FileSelectedEvent e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Path)) return;
            string token = e.UserData != null ? e.UserData.ToString() : "";
            bool ours = _pendingOpacityBrowse
                || string.Equals(token, "MaterialOpacityPath", StringComparison.Ordinal)
                || _browseMesh >= 0 && _pendingOpacityBrowse;
            if (!_pendingOpacityBrowse && !string.Equals(token, "MaterialOpacityPath", StringComparison.Ordinal))
                return;
            _pendingOpacityBrowse = false;
            if (_browseMesh < 0)
                return;
            _ignoreMaterialInput = true;
            try
            {
                ApplyOpacityPath(_browseEntityId, _browseMesh, _browseMat, e.Path);
            }
            finally
            {
                _ignoreMaterialInput = false;
            }
        }


        private void WriteOpacityIntoMeshPack(int entityId, int meshIndex, int matIndex, string opacityPath)
        {
            if (ModelManager.Instance == null) return;
            string key = null;
            if (entityId > 0)
            {
                var level = ProjectSettings.Current?.CurrentLevel;
                var entity = level?.Entities.FirstOrDefault(e => e.Id == entityId);
                key = entity?.GetComponent<ModelComponent>()?.Key;
            }
            else
            {
                var viewer = ResolveViewer();
                if (!string.IsNullOrEmpty(viewer?.MeshPath))
                    key = Path.GetFileNameWithoutExtension(viewer.MeshPath);
            }
            if (string.IsNullOrEmpty(key)) return;
            AnimationPack pack = null;
            string packKey = key.ToLowerInvariant();
            if (!packKey.EndsWith("_pack"))
            {
                if (!ModelManager.Instance.TryGetAnimationPack(packKey + "_pack", out pack))
                    ModelManager.Instance.TryGetAnimationPack(packKey, out pack);
            }
            else
                ModelManager.Instance.TryGetAnimationPack(packKey, out pack);
            if (pack == null) return;
            if (pack.MaterialOptions == null)
                pack.MaterialOptions = new List<MeshMaterialOption>();
            MeshMaterialOption existing = null;
            for (int i = 0; i < pack.MaterialOptions.Count; i++)
            {
                var o = pack.MaterialOptions[i];
                if (o != null && o.MeshIndex == meshIndex && o.MaterialIndex == matIndex)
                {
                    existing = o;
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(opacityPath))
            {
                if (existing != null) pack.MaterialOptions.Remove(existing);
            }
            else
            {
                if (existing == null)
                {
                    existing = new MeshMaterialOption { MeshIndex = meshIndex, MaterialIndex = matIndex };
                    pack.MaterialOptions.Add(existing);
                }
                existing.OpacityPath = opacityPath;
            }
            string project = ProjectSettings.Current?.ActiveProject;
            string packId = pack.Id ?? packKey;
            if (!string.IsNullOrEmpty(project) && !string.IsNullOrEmpty(packId))
            {
                string jsonPath = Path.Combine(project, "Assets", packId, "assetpack.json");
                if (!File.Exists(jsonPath))
                    jsonPath = Path.Combine(project, "Assets", packId.ToLowerInvariant(), "assetpack.json");
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        File.WriteAllText(jsonPath, JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PropertiesPanel] Failed writing mesh pack: {ex.Message}");
                    }
                }
            }
        }


        private string ResolveModelKey(int entityId)
        {
            foreach (var mc in EnumerateModelComponents(entityId))
            {
                if (!string.IsNullOrEmpty(mc.Key))
                    return mc.Key;
            }
            var viewer = ResolveViewer();
            if (!string.IsNullOrEmpty(viewer?.MeshPath))
                return Path.GetFileNameWithoutExtension(viewer.MeshPath);
            return null;
        }

        private void SyncOpacityInputValue(int entityId, int meshIndex, int matIndex, string stored)
        {
            stored = stored ?? "";
            if (_uiOverlay == null) return;
            var inputs = _uiOverlay.FindElementsByTag("input");
            if (inputs == null) return;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inputs[i] is not InputElement input) continue;
                string hook = input.Attributes.GetValueOrDefault("data-hook", "");
                if (hook != "SetMaterialOption" && hook != "SetMaterialOpacity") continue;
                if (!int.TryParse(input.Attributes.GetValueOrDefault("data-entityid", "-1"), out int id)) id = -1;
                string meshStr = input.Attributes.GetValueOrDefault("data-mesh", input.Attributes.GetValueOrDefault("data-index", "-1"));
                if (!int.TryParse(meshStr, out int mi)) mi = -1;
                if (!int.TryParse(input.Attributes.GetValueOrDefault("data-mat", "0"), out int mat)) mat = 0;
                if (id != entityId || mi != meshIndex || mat != matIndex) continue;
                input.Value = stored;
                input.CommittedValue = stored;
                input.Attributes["value"] = stored;
            }
            _uiOverlay.RefreshUI();
        }

        private void OpenOpacityFileBrowser()
        {
            string startDir = null;
            string project = ProjectSettings.Current?.ActiveProject;
            if (!string.IsNullOrEmpty(project))
            {
                string projectTextures = Path.Combine(project, "Textures");
                string projectAssets = Path.Combine(project, "Assets");
                if (Directory.Exists(projectTextures)) startDir = projectTextures;
                else if (Directory.Exists(projectAssets)) startDir = projectAssets;
                else if (Directory.Exists(project)) startDir = project;
            }
            if (string.IsNullOrEmpty(startDir))
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string texturesDir = Path.Combine(exeDir, "Assets");
                startDir = Directory.Exists(texturesDir) ? texturesDir : exeDir;
            }
            _pendingOpacityBrowse = true;
            var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, startDir, ".png", ".jpg", ".jpeg", ".tga");
            fileSelector.UserData = "MaterialOpacityPath";
            fileSelector.IsModal = true;
            _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
        }

        public void HandleDataHook(string hook)
        {
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
            if (hook == "SetMaterialOption" || hook == "SetMaterialOpacity")
            {
                return;
            }
            if (hook == "BrowseMaterialPath")
            {
                OpenOpacityFileBrowser();
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
            if (select == null) return;
            string hook = select.Attributes.GetValueOrDefault("data-hook", "");
            if (hook == "SetComponentProperty")
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