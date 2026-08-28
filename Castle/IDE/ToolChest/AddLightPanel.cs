// Folder: ToolChest
// File: AddLightPanel.cs
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
    public class AddLightPanel : BasePanel
    {
        private class AddLightUIOverlay : UIOverlay
        {
            private readonly AddLightPanel _parent;
            public AddLightUIOverlay(AddLightPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }

            protected override void HandleDataHook(string hook)
            {
                if (hook == "AddLightConfirm")
                {
                    _parent.Confirm();
                    return;
                }
                if (hook == "CancelAddLight")
                {
                    _parent._eventBus?.Publish(new ClosePanelEvent(_parent));
                }
            }
        }

        private readonly EventBus _eventBus;

        public AddLightPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            _eventBus = eventBus;
            HasTitleBar = true;
            IsClosable = true;
            IsModal = true;
            RenderOrder = 1200;
            Scaling = ScalingMode.Fill;
            Size = new Vector2(420, 460);
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new AddLightUIOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AddLight.html");
            if (File.Exists(htmlPath))
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private void Confirm()
        {
            var typeElem = _uiOverlay.FindElementById("light-type") as SelectElement;
            var colorElem = _uiOverlay.FindElementById("light-color") as InputElement;
            var intensityElem = _uiOverlay.FindElementById("light-intensity") as InputElement;
            var dirElem = _uiOverlay.FindElementById("light-direction") as InputElement;
            var rangeElem = _uiOverlay.FindElementById("light-range") as InputElement;
            var castElem = _uiOverlay.FindElementById("light-cast-shadows") as InputElement;

            string type = typeElem?.Value ?? "Directional";
            Vector3 color = ParseVec3(colorElem?.Value, Vector3.One);
            float intensity = ParseFloat(intensityElem?.Value, 1f);
            Vector3 direction = ParseVec3(dirElem?.Value, new Vector3(-0.85f, 0.10f, -0.52f));
            if (direction.LengthSquared() < 1e-8f)
                direction = new Vector3(-0.85f, 0.10f, -0.52f);
            direction = Vector3.Normalize(direction);
            float range = ParseFloat(rangeElem?.Value, 25f);
            bool castShadows = castElem == null || IsChecked(castElem.Value);

            _eventBus?.Publish(new GenericEvent
            {
                Hook = "AddLight",
                Data = new Dictionary<string, string>
                {
                    { "type", type },
                    { "color", $"{color.X.ToString(CultureInfo.InvariantCulture)},{color.Y.ToString(CultureInfo.InvariantCulture)},{color.Z.ToString(CultureInfo.InvariantCulture)}" },
                    { "intensity", intensity.ToString(CultureInfo.InvariantCulture) },
                    { "direction", $"{direction.X.ToString(CultureInfo.InvariantCulture)},{direction.Y.ToString(CultureInfo.InvariantCulture)},{direction.Z.ToString(CultureInfo.InvariantCulture)}" },
                    { "range", range.ToString(CultureInfo.InvariantCulture) },
                    { "castShadows", castShadows ? "true" : "false" }
                }
            });
            _eventBus?.Publish(new ClosePanelEvent(this));
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new AddLightPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }

        private static bool IsChecked(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
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
    }
}
