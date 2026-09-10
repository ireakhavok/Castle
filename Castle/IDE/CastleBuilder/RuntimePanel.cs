// Folder: CastleBuilder
// File: RuntimePanel.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace CastleBuilder
{
    public class RuntimePanel : BasePanel
    {
        private class RuntimeOverlay : UIOverlay
        {
            private readonly RuntimePanel _parent;
            public RuntimeOverlay(RuntimePanel parent, IRenderContext rc, IControlContext cc, nint window)
                : base(rc, cc, window) { _parent = parent; }
            protected override void HandleDataHook(string hook)
            {
                if (hook == "RuntimeApply")
                    _parent.ApplyFromForm();
            }
        }

        private bool _seeded;
        private string _lastLookXText = "";
        private string _lastLookYText = "";
        private string _lastSignature = "";

        public override bool WantsContinuousUpdate => true;

        public RuntimePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            ChromeStyle = PanelChromeStyle.Editor;
            Scaling = ScalingMode.Fill;
            this.DockingMode = SiegeEngine.Core.Definitions.DockingMode.IDE;
            BaseWidth = 420f;
            BaseHeight = 460f;
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            eventBus.Publish(new OpenPanelEvent(new RuntimePanel(renderContext, controlContext, window, eventBus)) { Mode = OpenMode.Overlay });
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new RuntimeOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuntimeUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine("[RuntimePanel] RuntimeUI.html not found next to the exe");
                return;
            }
            _uiOverlay.LoadUI(File.ReadAllText(htmlPath), Path.GetDirectoryName(htmlPath) ?? "");
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            PrefillFromDraft();
            SyncLookReadouts();
            _lastSignature = FormSignature();
            _uiOverlay.RefreshUI();
            _seeded = true;
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            if (!_seeded || !IsFormLive()) return;
            SyncLookReadouts();
            string signature = FormSignature();
            if (signature != _lastSignature)
                ApplyFromForm();
        }

        public void FlushDraft()
        {
            if (_seeded)
                ApplyFromForm();
        }

        public override void Dispose()
        {
            FlushDraft();
            base.Dispose();
        }

        private bool IsFormLive()
        {
            return Visible && _uiOverlay != null && _uiOverlay.FindElementById("runtime-look-x") != null;
        }

        private void PrefillFromDraft()
        {
            var draft = RuntimeSettings.Current;
            var fixedStep = _uiOverlay.FindElementById("runtime-fixed-step") as InputElement;
            if (fixedStep != null)
            {
                fixedStep.Checked = draft.UseFixedTimestep;
                fixedStep.Value = draft.UseFixedTimestep ? "true" : "false";
                if (fixedStep.Attributes != null)
                    fixedStep.Attributes["checked"] = draft.UseFixedTimestep ? "checked" : "";
            }
            SetInput("runtime-step-rate", draft.StepRateHz.ToString(CultureInfo.InvariantCulture));
            SetInput("runtime-gravity-z", draft.GravityZ.ToString(CultureInfo.InvariantCulture));
            SetInput("runtime-frame-cap", draft.FrameCapHz.ToString(CultureInfo.InvariantCulture));
            SetInput("runtime-look-x", RuntimeSettings.LookToSlider(draft.MouseSensitivityX).ToString("0", CultureInfo.InvariantCulture));
            SetInput("runtime-look-y", RuntimeSettings.LookToSlider(draft.MouseSensitivityY).ToString("0", CultureInfo.InvariantCulture));
            var mode = _uiOverlay.FindElementById("runtime-mode") as SelectElement;
            if (mode != null)
                mode.Value = RuntimeSettings.NormalizeMode(string.IsNullOrEmpty(draft.Mode) ? RuntimeSettings.ModeSinglePlayer : draft.Mode);
        }

        private void ApplyFromForm()
        {
            if (!IsFormLive()) return;
            var draft = RuntimeSettings.Current;
            draft.UseFixedTimestep = GetChecked("runtime-fixed-step");
            if (!AnyTextFieldFocused())
            {
                draft.StepRateHz = ParseFloat(GetInputValue("runtime-step-rate"), draft.StepRateHz);
                draft.GravityZ = ParseFloat(GetInputValue("runtime-gravity-z"), draft.GravityZ);
                float cap = ParseFloat(GetInputValue("runtime-frame-cap"), draft.FrameCapHz);
                if (cap >= 0f) draft.FrameCapHz = cap;
            }
            else
            {
                var capEl = _uiOverlay.FindElementById("runtime-frame-cap") as InputElement;
                if (capEl != null && !capEl.IsFocused)
                {
                    float cap = ParseFloat(capEl.Value, draft.FrameCapHz);
                    if (cap >= 0f) draft.FrameCapHz = cap;
                }
            }
            draft.MouseSensitivityX = RuntimeSettings.SliderToLook(ParseFloat(GetInputValue("runtime-look-x"), RuntimeSettings.LookToSlider(draft.MouseSensitivityX)));
            draft.MouseSensitivityY = RuntimeSettings.SliderToLook(ParseFloat(GetInputValue("runtime-look-y"), RuntimeSettings.LookToSlider(draft.MouseSensitivityY)));
            string mode = GetSelectValue("runtime-mode");
            if (!string.IsNullOrEmpty(mode))
                draft.Mode = mode;
            _lastSignature = FormSignature();
        }

        private bool AnyTextFieldFocused()
        {
            return IsFocused("runtime-step-rate") || IsFocused("runtime-gravity-z") || IsFocused("runtime-frame-cap");
        }

        private bool IsFocused(string id)
        {
            var el = _uiOverlay.FindElementById(id) as InputElement;
            return el != null && el.IsFocused;
        }

        private void SyncLookReadouts()
        {
            SyncIntReadout("runtime-look-x", "runtime-look-x-val", ref _lastLookXText);
            SyncIntReadout("runtime-look-y", "runtime-look-y-val", ref _lastLookYText);
        }

        private void SyncIntReadout(string sliderId, string readoutId, ref string last)
        {
            float v = ParseFloat(GetInputValue(sliderId), 10f);
            string text = ((int)MathF.Round(v)).ToString(CultureInfo.InvariantCulture);
            if (text == last) return;
            last = text;
            SetReadout(readoutId, text);
        }

        private string FormSignature()
        {
            return string.Join("\n",
                GetChecked("runtime-fixed-step") ? "1" : "0",
                GetInputValue("runtime-step-rate") ?? "",
                GetInputValue("runtime-gravity-z") ?? "",
                GetInputValue("runtime-frame-cap") ?? "",
                GetInputValue("runtime-look-x") ?? "",
                GetInputValue("runtime-look-y") ?? "",
                GetSelectValue("runtime-mode") ?? "");
        }

        private void SetInput(string id, string value)
        {
            var elem = _uiOverlay.FindElementById(id);
            if (elem is RangeElement range)
            {
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                {
                    range.Min = RuntimeSettings.LookSliderMin;
                    range.Max = RuntimeSettings.LookSliderMax;
                    range.Step = 1f;
                    range.Value = parsed;
                    range.Attributes["min"] = "0";
                    range.Attributes["max"] = "100";
                    range.Attributes["step"] = "1";
                    range.Attributes["value"] = parsed.ToString(CultureInfo.InvariantCulture);
                    ((InputElement)range).Value = parsed.ToString(CultureInfo.InvariantCulture);
                }
                return;
            }
            if (elem is InputElement input && !input.IsFocused)
                input.Value = value;
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
            if (_uiOverlay.FindElementById(id) is SelectElement select)
                return select.Value;
            return null;
        }

        private bool GetChecked(string id)
        {
            if (_uiOverlay.FindElementById(id) is InputElement input)
                return input.Checked;
            return false;
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

        private static float ParseFloat(string text, float fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return v;
            return fallback;
        }
    }
}
