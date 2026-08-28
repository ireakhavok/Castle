// Folder: ToolChest
// File: PostProcessPanel.cs
using Keystone;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
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
            IsModal = true;
            RenderOrder = 1200;
            Scaling = ScalingMode.Fill;
            Size = new Vector2(440, 560);
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

        private void Prefill()
        {
            var env = ProjectSettings.Current?.CurrentLevel?.Environment;
            if (env == null)
                return;
            SetInput("pp-sun-direction", FormatVec3(env.SunDirection.LengthSquared() > 1e-8f ? env.SunDirection : new Vector3(-0.85f, 0.10f, -0.52f)));
            SetInput("pp-sun-intensity", env.SunIntensity.ToString(CultureInfo.InvariantCulture));
            SetInput("pp-fog-density", env.FogDensity.ToString(CultureInfo.InvariantCulture));
            SetInput("pp-fog-start", env.FogStart.ToString(CultureInfo.InvariantCulture));
            SetSelect("pp-fog-mode", string.IsNullOrWhiteSpace(env.FogMode) ? "Off" : env.FogMode);
            SetSelect("pp-fog-quality", string.IsNullOrWhiteSpace(env.FogQuality) ? "Off" : env.FogQuality);
            SetSelect("pp-shadow-quality", string.IsNullOrWhiteSpace(env.ShadowQuality) ? "Medium" : env.ShadowQuality);
            SetCheckbox("pp-sun-enabled", env.SunEnabled);
            SetCheckbox("pp-sun-cast-shadows", env.SunCastShadows);
        }

        private void Apply()
        {
            var dirElem = _uiOverlay.FindElementById("pp-sun-direction") as InputElement;
            var intensityElem = _uiOverlay.FindElementById("pp-sun-intensity") as InputElement;
            var shadowElem = _uiOverlay.FindElementById("pp-shadow-quality") as SelectElement;
            var fogModeElem = _uiOverlay.FindElementById("pp-fog-mode") as SelectElement;
            var fogQualityElem = _uiOverlay.FindElementById("pp-fog-quality") as SelectElement;
            var densityElem = _uiOverlay.FindElementById("pp-fog-density") as InputElement;
            var startElem = _uiOverlay.FindElementById("pp-fog-start") as InputElement;
            var sunEnabledElem = _uiOverlay.FindElementById("pp-sun-enabled") as InputElement;
            var sunCastElem = _uiOverlay.FindElementById("pp-sun-cast-shadows") as InputElement;

            Vector3 direction = ParseVec3(dirElem?.Value, new Vector3(-0.85f, 0.10f, -0.52f));
            if (direction.LengthSquared() < 1e-8f)
                direction = new Vector3(-0.85f, 0.10f, -0.52f);
            direction = Vector3.Normalize(direction);

            _eventBus?.Publish(new GenericEvent
            {
                Hook = "PostProcessSet",
                Data = new Dictionary<string, string>
                {
                    { "sunEnabled", IsChecked(sunEnabledElem?.Value) ? "true" : "false" },
                    { "sunDirection", FormatVec3(direction) },
                    { "sunIntensity", ParseFloat(intensityElem?.Value, 1f).ToString(CultureInfo.InvariantCulture) },
                    { "sunCastShadows", IsChecked(sunCastElem?.Value) ? "true" : "false" },
                    { "shadowQuality", shadowElem?.Value ?? "Medium" },
                    { "fogMode", fogModeElem?.Value ?? "Off" },
                    { "fogQuality", fogQualityElem?.Value ?? "Off" },
                    { "fogDensity", ParseFloat(densityElem?.Value, 0.003f).ToString(CultureInfo.InvariantCulture) },
                    { "fogStart", ParseFloat(startElem?.Value, 40f).ToString(CultureInfo.InvariantCulture) }
                }
            });
            _eventBus?.Publish(new ClosePanelEvent(this));
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new PostProcessPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }

        private void SetInput(string id, string value)
        {
            if (_uiOverlay.FindElementById(id) is InputElement input)
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
                input.Value = value ? "true" : "false";
        }

        private static bool IsChecked(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value == "on" || value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static float ParseFloat(string raw, float fallback)
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return v;
            return fallback;
        }

        private static Vector3 ParseVec3(string raw, Vector3 fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            var parts = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return fallback;
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                return new Vector3(x, y, z);
            return fallback;
        }

        private static string FormatVec3(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", v.X, v.Y, v.Z);
        }
    }
}
