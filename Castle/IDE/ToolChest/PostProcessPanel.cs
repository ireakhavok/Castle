// Folder: ToolChest
// File: PostProcessPanel.cs
using Keystone;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Lighting;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace ToolChest
{
    public class PostProcessPanel : BasePanel
    {
        private class PostProcessUIOverlay : UIOverlay
        {
            private readonly PostProcessPanel _parent;
            public PostProcessUIOverlay(PostProcessPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }

            protected override void HandleDataHook(string hook)
            {
                if (hook == "PostProcessApply")
                {
                    _parent.Apply();
                    return;
                }
                if (hook == "CancelPostProcess")
                {
                    _parent._eventBus?.Publish(new ClosePanelEvent(_parent));
                }
            }
        }

        private readonly EventBus _eventBus;

        public PostProcessPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            _eventBus = eventBus;
            HasTitleBar = true;
            IsClosable = true;
            AllowDragging = true;
            IsModal = false;
            DockingMode = DockingMode.IDE;
            BaseWidth = 420f;
            BaseHeight = 620f;
            Size = new Vector2(420f, 620f);
            RenderOrder = 0;
            Scaling = ScalingMode.Fill;
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new PostProcessUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PostProcess.html");
            if (File.Exists(htmlPath))
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            Prefill();
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            SyncSliderReadouts();
        }

        private void Prefill()
        {
            var env = ResolveEnvironment() ?? new EnvironmentSettings();
            Vector3 dir = env.SunDirection.LengthSquared() > 1e-8f
                ? Vector3.Normalize(env.SunDirection)
                : LightingFrame.DefaultSunDirection;
            DirectionToAzEl(dir, out float azimuth, out float elevation);
            SetInput("pp-sun-azimuth", azimuth.ToString("0", CultureInfo.InvariantCulture));
            SetInput("pp-sun-elevation", elevation.ToString("0", CultureInfo.InvariantCulture));
            SetReadout("pp-sun-azimuth-val", azimuth.ToString("0", CultureInfo.InvariantCulture));
            SetReadout("pp-sun-elevation-val", elevation.ToString("0", CultureInfo.InvariantCulture));
            SetReadout("pp-sun-vector", FormatVec3(dir));
            SetInput("pp-sun-intensity", env.SunIntensity.ToString(CultureInfo.InvariantCulture));
            SetInput("pp-fog-density", env.FogDensity.ToString(CultureInfo.InvariantCulture));
            SetInput("pp-fog-start", env.FogStart.ToString(CultureInfo.InvariantCulture));
            SetSelect("pp-fog-mode", string.IsNullOrWhiteSpace(env.FogMode) ? "Off" : env.FogMode);
            SetSelect("pp-fog-quality", string.IsNullOrWhiteSpace(env.FogQuality) ? "Off" : env.FogQuality);
            SetSelect("pp-shadow-quality", string.IsNullOrWhiteSpace(env.ShadowQuality) ? "Medium" : env.ShadowQuality);
            SetCheckbox("pp-sun-enabled", env.SunEnabled);
            SetCheckbox("pp-sun-cast-shadows", env.SunCastShadows);
        }

        private string _lastAzimuthText;
        private string _lastElevationText;
        private string _lastVectorText;

        private void SyncSliderReadouts()
        {
            float azimuth = ParseFloat(GetInputValue("pp-sun-azimuth"), 187f);
            float elevation = ParseFloat(GetInputValue("pp-sun-elevation"), 31f);
            string azText = azimuth.ToString("0", CultureInfo.InvariantCulture);
            string elText = elevation.ToString("0", CultureInfo.InvariantCulture);
            string vecText = FormatVec3(AzElToDirection(azimuth, elevation));
            if (azText != _lastAzimuthText)
            {
                _lastAzimuthText = azText;
                SetReadout("pp-sun-azimuth-val", azText);
            }
            if (elText != _lastElevationText)
            {
                _lastElevationText = elText;
                SetReadout("pp-sun-elevation-val", elText);
            }
            if (vecText != _lastVectorText)
            {
                _lastVectorText = vecText;
                SetReadout("pp-sun-vector", vecText);
            }
        }

        private void Apply()
        {
            SyncSliderReadouts();
            float azimuth = ParseFloat(GetInputValue("pp-sun-azimuth"), 187f);
            float elevation = ParseFloat(GetInputValue("pp-sun-elevation"), 31f);
            Vector3 direction = AzElToDirection(azimuth, elevation);

            float intensity = ParseFloat(GetInputValue("pp-sun-intensity"), 1f);
            float density = ParseFloat(GetInputValue("pp-fog-density"), 0.003f);
            float start = ParseFloat(GetInputValue("pp-fog-start"), 0f);
            string shadowQuality = GetSelectValue("pp-shadow-quality") ?? "Medium";
            string fogMode = GetSelectValue("pp-fog-mode") ?? "Off";
            string fogQuality = GetSelectValue("pp-fog-quality") ?? "Off";
            bool sunCast = GetChecked("pp-sun-cast-shadows");
            // Casting shadows requires a sun. The Enable-sun checkbox is easy
            // to miss (the label is not a <label for=>) so treat cast as on.
            bool sunEnabled = GetChecked("pp-sun-enabled") || sunCast;
            if (!string.Equals(shadowQuality, "Off", StringComparison.OrdinalIgnoreCase) && sunCast)
                sunEnabled = true;

            var env = CommitEnvironment(direction, intensity, sunEnabled, sunCast, shadowQuality, fogMode, fogQuality, density, start);
            LightingSettings.BindAuthored(env);
            LightingFrame.Current = null;

            _eventBus?.Publish(new GenericEvent
            {
                Hook = "PostProcessSet",
                Data = new Dictionary<string, string>
                {
                    { "sunEnabled", sunEnabled ? "true" : "false" },
                    { "sunDirection", FormatVec3(direction) },
                    { "sunIntensity", intensity.ToString(CultureInfo.InvariantCulture) },
                    { "sunCastShadows", sunCast ? "true" : "false" },
                    { "shadowQuality", shadowQuality },
                    { "fogMode", fogMode },
                    { "fogQuality", fogQuality },
                    { "fogDensity", density.ToString(CultureInfo.InvariantCulture) },
                    { "fogStart", start.ToString(CultureInfo.InvariantCulture) }
                }
            });
        }

        public static EnvironmentSettings CommitEnvironment(
            Vector3 direction,
            float intensity,
            bool sunEnabled,
            bool sunCastShadows,
            string shadowQuality,
            string fogMode,
            string fogQuality,
            float fogDensity,
            float fogStart)
        {
            var env = ResolveEnvironment() ?? new EnvironmentSettings();
            env.SunEnabled = sunEnabled;
            env.SunDirection = direction.LengthSquared() > 1e-8f ? Vector3.Normalize(direction) : LightingFrame.DefaultSunDirection;
            env.SunIntensity = intensity < 0f ? 0f : intensity;
            env.SunCastShadows = sunCastShadows;
            env.ShadowQuality = string.IsNullOrWhiteSpace(shadowQuality) ? "Medium" : shadowQuality.Trim();
            env.FogMode = string.IsNullOrWhiteSpace(fogMode) ? "Off" : fogMode.Trim();
            env.FogQuality = string.IsNullOrWhiteSpace(fogQuality) ? "Off" : fogQuality.Trim();
            env.FogDensity = fogDensity < 0f ? 0f : fogDensity;
            env.FogStart = fogStart < 0f ? 0f : fogStart;

            var level = ProjectSettings.Current?.CurrentLevel;
            if (level != null)
                level.Environment = env;

            var sceneData = ProjectSettings.Current?.CurrentSceneData;
            if (sceneData != null)
                sceneData.Environment = env;

            return env;
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new PostProcessPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }

        private static EnvironmentSettings ResolveEnvironment()
        {
            var levelEnv = ProjectSettings.Current?.CurrentLevel?.Environment;
            if (levelEnv != null)
                return levelEnv;
            return ProjectSettings.Current?.CurrentSceneData?.Environment;
        }

        public static Vector3 AzElToDirection(float azimuthDeg, float elevationDeg)
        {
            float az = azimuthDeg * MathF.PI / 180f;
            float el = Math.Clamp(elevationDeg, 5f, 85f) * MathF.PI / 180f;
            Vector3 sunPos = new Vector3(
                MathF.Cos(el) * MathF.Cos(az),
                MathF.Cos(el) * MathF.Sin(az),
                MathF.Sin(el));
            Vector3 travel = -sunPos;
            if (travel.LengthSquared() < 1e-8f)
                return LightingFrame.DefaultSunDirection;
            return Vector3.Normalize(travel);
        }

        public static void DirectionToAzEl(Vector3 travel, out float azimuthDeg, out float elevationDeg)
        {
            Vector3 sunPos = travel.LengthSquared() > 1e-8f ? Vector3.Normalize(-travel) : -LightingFrame.DefaultSunDirection;
            float el = MathF.Asin(Math.Clamp(sunPos.Z, -1f, 1f));
            float az = MathF.Atan2(sunPos.Y, sunPos.X);
            if (az < 0f) az += MathF.PI * 2f;
            azimuthDeg = az * 180f / MathF.PI;
            elevationDeg = Math.Clamp(el * 180f / MathF.PI, 5f, 85f);
        }

        private void SetInput(string id, string value)
        {
            var elem = _uiOverlay.FindElementById(id);
            if (elem is RangeElement range)
            {
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                {
                    range.Value = parsed;
                    range.Attributes["value"] = parsed.ToString(CultureInfo.InvariantCulture);
                    ((InputElement)range).Value = parsed.ToString(CultureInfo.InvariantCulture);
                }
                return;
            }
            if (elem is InputElement input)
                input.Value = value;
        }

        private void SetSelect(string id, string value)
        {
            if (_uiOverlay.FindElementById(id) is SelectElement select)
                select.Value = value;
        }

        private void SetCheckbox(string id, bool value)
        {
            if (_uiOverlay.FindElementById(id) is InputElement input)
            {
                input.Value = value ? "true" : "false";
                input.Checked = value;
                if (input.Attributes != null)
                    input.Attributes["checked"] = value ? "checked" : "";
            }
        }

        private void SetReadout(string id, string value)
        {
            var elem = _uiOverlay.FindElementById(id);
            if (elem == null) return;
            if (elem is TextElement selfText)
            {
                selfText.Content = value ?? "";
                return;
            }
            if (elem.Children != null)
            {
                foreach (var child in elem.Children)
                {
                    if (child is TextElement textChild)
                    {
                        textChild.Content = value ?? "";
                        return;
                    }
                }
            }
            if (elem.Attributes != null)
                elem.Attributes["text"] = value ?? "";
        }

        private string GetInputValue(string id)
        {
            var elem = _uiOverlay.FindElementById(id);
            if (elem is RangeElement range)
                return range.Value.ToString(CultureInfo.InvariantCulture);
            if (elem is InputElement input)
                return input.Value;
            return null;
        }

        private string GetSelectValue(string id)
        {
            return (_uiOverlay.FindElementById(id) as SelectElement)?.Value;
        }

        private bool GetChecked(string id)
        {
            if (_uiOverlay.FindElementById(id) is InputElement input)
                return input.Checked;
            return false;
        }

        private static float ParseFloat(string raw, float fallback)
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return v;
            return fallback;
        }

        private static string FormatVec3(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.000}, {1:0.000}, {2:0.000}", v.X, v.Y, v.Z);
        }
    }
}
