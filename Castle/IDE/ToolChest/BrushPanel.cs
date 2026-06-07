// Folder: ToolChest
// File: BrushPanel.cs
using Keystone;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Core.Managers;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
namespace ToolChest
{
    public class BrushPanel : BasePanel
    {
        private class BrushUIOverlay : UIOverlay
        {
            private readonly BrushPanel _parent;
            public BrushUIOverlay(BrushPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
            }
            protected override void HandleDataHook(string hook)
            {
                _parent.HandleBrushDataHook(hook);
            }
        }
        private Brush _currentBrush = new Brush();
        private string _lastMode = "Raise";
        public BrushPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            DockingMode = SiegeEngine.Core.Definitions.DockingMode.IDE;
            AllowDragging = true;
            DockState = DockState.Floating;
        }
        protected override UIOverlay CreateUIOverlay()
        {
            return new BrushUIOverlay(this, _renderContext, _controlContext, _window);
        }
        public override void Init()
        {
            base.Init();
            chrome.close_color = new Vector4(0.486f, 1.0f, 0.796f, 1.0f);
            LoadBrushUI();
            PublishCurrentBrush();
            RefreshMaterialDropdown();
        }
        private void LoadBrushUI()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BrushPanelUI.html");
            if (File.Exists(htmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            }
            _uiOverlay.RefreshUI();
        }
        private void HandleBrushDataHook(string hook)
        {
            bool changed = false;
            if (hook == "BrushSizeChanged")
            {
                var slider = _uiOverlay.FindElementById("sizeSlider") as InputElement;
                if (slider != null)
                {
                    float size = float.Parse(slider.Value ?? "10");
                    _currentBrush.Size = size;
                    changed = true;
                }
            }
            else if (hook == "BrushIntensityChanged")
            {
                var slider = _uiOverlay.FindElementById("intensitySlider") as InputElement;
                if (slider != null)
                {
                    float intensity = float.Parse(slider.Value ?? "1");
                    _currentBrush.Intensity = intensity;
                    changed = true;
                }
            }
            else if (hook == "BrushShapeChanged")
            {
                var select = _uiOverlay.FindElementsByTag("select").FirstOrDefault(el => el.Attributes.GetValueOrDefault("data-hook", "") == "BrushShapeChanged") as SelectElement;
                if (select != null)
                {
                    string shapeStr = select.Value ?? "Circle";
                    _currentBrush.Shape = (BrushShape)Enum.Parse(typeof(BrushShape), shapeStr, true);
                    changed = true;
                }
            }
            else if (hook == "BrushFalloffChanged")
            {
                var select = _uiOverlay.FindElementsByTag("select").FirstOrDefault(el => el.Attributes.GetValueOrDefault("data-hook", "") == "BrushFalloffChanged") as SelectElement;
                if (select != null)
                {
                    string falloffStr = select.Value ?? "Gaussian";
                    _currentBrush.Falloff = (BrushFalloff)Enum.Parse(typeof(BrushFalloff), falloffStr, true);
                    changed = true;
                }
            }
            else if (hook == "BrushModeChanged")
            {
                var select = _uiOverlay.FindElementsByTag("select").FirstOrDefault(el => el.Attributes.GetValueOrDefault("data-hook", "") == "BrushModeChanged") as SelectElement;
                if (select != null)
                {
                    _lastMode = select.Value ?? "Raise";
                    string modeStr = _lastMode;
                    _currentBrush.Mode = (BrushMode)Enum.Parse(typeof(BrushMode), modeStr, true);
                    changed = true;
                    var materialSection = _uiOverlay.FindElementById("material-section");
                    if (materialSection != null)
                    {
                        string newDisplay = (_currentBrush.Mode == BrushMode.Paint) ? "block" : "none";
                        materialSection.Style.SetProperty("display", newDisplay);
                        materialSection.Attributes["style"] = $"display: {newDisplay};";
                    }
                }
            }
            else if (hook == "BrushPaintLayerChanged")
            {
                var select = _uiOverlay.FindElementsByTag("select").FirstOrDefault(el => el.Attributes.GetValueOrDefault("data-hook", "") == "BrushPaintLayerChanged") as SelectElement;
                if (select != null)
                {
                    _currentBrush.PaintLayer = int.Parse(select.Value ?? "0");
                    changed = true;
                }
            }
            else if (hook == "NewMaterial")
            {
                NewMaterialPanel.Open(_renderContext, _controlContext, _window, _eventBus);
                return;
            }
            else if (hook.StartsWith("Material"))
            {
                HandleMaterialDataHook(hook);
                changed = true;
            }
            if (changed)
            {
                PublishCurrentBrush();
                _uiOverlay.RefreshUI();
                _uiOverlay.RecomputeLayout(_uiOverlay.PanelWidth, _uiOverlay.PanelHeight);
            }
        }
        private void HandleMaterialDataHook(string hook)
        {
            var paintData = ProjectSettings.Current.GetPaintData(ProjectSettings.Current.CurrentSceneName ?? "Untitled");
            if (paintData == null) return;
            if (hook == "SelectMaterial")
            {
                var select = _uiOverlay.FindElementById("materialSelect") as SelectElement;
                if (select != null && int.TryParse(select.Value, out int index) && index >= 0 && index < paintData.Materials.Count)
                {
                    var selectedMat = paintData.Materials[index];
                    _currentBrush.MaterialPath = selectedMat.AlbedoPath ?? string.Empty;
                    Console.WriteLine($"[BrushPanel] SelectMaterial hook - publishing SelectBrushEvent with MaterialPath='{_currentBrush.MaterialPath}' Mode='{_currentBrush.Mode}'");
                    PublishCurrentBrush();
                }
                else
                {
                    Console.WriteLine($"[BrushPanel] SelectMaterial hook failed - select null or invalid index");
                }
            }
            else if (hook == "SaveMaterial")
            {
                RefreshMaterialDropdown();
            }
        }
        public void RefreshMaterialDropdown()
        {
            var paintData = ProjectSettings.Current.GetPaintData(ProjectSettings.Current.CurrentSceneName ?? "Untitled");
            if (paintData == null) return;
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BrushPanelUI.html");
            if (!File.Exists(htmlPath)) return;
            string baseHtml = File.ReadAllText(htmlPath);
            StringBuilder dynamicSelect = new StringBuilder();
            dynamicSelect.Append("<select id=\"materialSelect\" data-hook=\"SelectMaterial\">");
            for (int i = 0; i < paintData.Materials.Count; i++)
            {
                var mat = paintData.Materials[i];
                dynamicSelect.Append($"<option value=\"{i}\">{mat.Name}</option>");
            }
            dynamicSelect.Append("</select>");
            int insertIndex = baseHtml.IndexOf("<select id=\"materialSelect\" data-hook=\"SelectMaterial\">");
            string modifiedHtml;
            if (insertIndex == -1)
            {
                modifiedHtml = baseHtml;
            }
            else
            {
                modifiedHtml = baseHtml.Substring(0, insertIndex) + dynamicSelect.ToString() + baseHtml.Substring(baseHtml.IndexOf("</select>", insertIndex) + 9);
            }
            _uiOverlay.LoadUI(modifiedHtml);
            // Auto-select the last (newest) material after refresh and publish it immediately
            if (paintData.Materials.Count > 0)
            {
                var select = _uiOverlay.FindElementById("materialSelect") as SelectElement;
                if (select != null)
                {
                    select.Value = (paintData.Materials.Count - 1).ToString();
                    var selectedMat = paintData.Materials.Last();
                    _currentBrush.MaterialPath = selectedMat.AlbedoPath ?? string.Empty;
                    Console.WriteLine($"[BrushPanel] RefreshMaterialDropdown auto-selected newest material '{selectedMat.Name}' - publishing SelectBrushEvent with MaterialPath='{_currentBrush.MaterialPath}'");
                    PublishCurrentBrush();
                }
            }
            var modeSelect = _uiOverlay.FindElementsByTag("select").FirstOrDefault(el => el.Attributes.GetValueOrDefault("data-hook", "") == "BrushModeChanged") as SelectElement;
            if (modeSelect != null && _lastMode == "Paint")
            {
                modeSelect.Value = "Paint";
                var materialSection = _uiOverlay.FindElementById("material-section");
                if (materialSection != null)
                {
                    materialSection.Style.SetProperty("display", "block");
                    materialSection.Attributes["style"] = "display: block;";
                }
            }
            _uiOverlay.RefreshUI();
        }
        private void PublishCurrentBrush()
        {
            Console.WriteLine($"[BrushPanel] Publishing SelectBrushEvent - Mode='{_currentBrush.Mode}', MaterialPath='{_currentBrush.MaterialPath}'");
            _eventBus.Publish(new SelectBrushEvent(
                0UL,
                _currentBrush.Mode.ToString(),
                _currentBrush.Size,
                _currentBrush.Intensity,
                _currentBrush.Shape.ToString(),
                _currentBrush.Falloff.ToString(),
                _currentBrush.PaintLayer,
                _currentBrush.MaterialPath), true);
        }
        public override void Detach()
        {
            _eventBus.Publish(new SelectBrushEvent(0UL, "", 0f, 0f, "", "", 0), true);
            base.Detach();
        }
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new BrushPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}