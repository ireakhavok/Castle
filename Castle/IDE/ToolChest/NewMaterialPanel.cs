// Folder: ToolChest
// File: NewMaterialPanel.cs
using Keystone;
using ReadingChamber;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.IO;
using System.Numerics;

namespace ToolChest
{
    public class NewMaterialPanel : BasePanel
    {
        private class NewMaterialUIOverlay : UIOverlay
        {
            private readonly NewMaterialPanel _parent;
            private readonly EventBus _eventBus;

            public NewMaterialUIOverlay(NewMaterialPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
                : base(renderContext, controlContext, window)
            {
                _parent = parent;
                _eventBus = eventBus;
            }

            protected override void HandleDataHook(string hook)
            {
                if (hook == "CreateNewMaterialConfirm")
                {
                    var nameElem = FindElementById("mat-name") as InputElement;
                    var albedoElem = FindElementById("mat-albedo") as InputElement;
                    var normalElem = FindElementById("mat-normal") as InputElement;
                    var roughnessElem = FindElementById("mat-roughness-input") as InputElement;

                    string name = nameElem?.Value?.Trim() ?? "New Material";
                    if (string.IsNullOrEmpty(name)) name = "New Material";

                    string albedo = albedoElem?.Value?.Trim() ?? "";
                    string normal = normalElem?.Value?.Trim() ?? "";
                    float roughness = 0.8f;
                    if (roughnessElem != null && float.TryParse(roughnessElem.Value, out float r)) roughness = r;

                    var paintData = ProjectSettings.Current.GetPaintData(ProjectSettings.Current.CurrentSceneName ?? "Untitled");
                    if (paintData != null)
                    {
                        paintData.Materials.Add(new TerrainMaterial
                        {
                            Name = name,
                            AlbedoPath = albedo,
                            NormalPath = normal,
                            Roughness = roughness
                        });
                        Console.WriteLine($"[NewMaterialPanel] Created material '{name}' and added to PaintData");
                    }

                    var brushPanel = PanelManager.Current?.GetAllPanels().FirstOrDefault(p => p is BrushPanel) as BrushPanel;
                    if (brushPanel != null)
                    {
                        brushPanel.RefreshMaterialDropdown();
                    }

                    _eventBus.Publish(new ClosePanelEvent(_parent));
                    return;
                }

                if (hook == "Cancel")
                {
                    _eventBus.Publish(new ClosePanelEvent(_parent));
                    return;
                }

                if (hook == "PickAlbedo")
                {
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    string texturesDir = Path.Combine(exeDir, "Assets", "Textures");
                    Directory.CreateDirectory(texturesDir);

                    var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, texturesDir, ".png", ".jpg", ".jpeg", ".tga");
                    fileSelector.UserData = "AlbedoField";
                    fileSelector.IsModal = true;
                    _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
                    return;
                }

                if (hook == "PickNormal")
                {
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    string texturesDir = Path.Combine(exeDir, "Assets", "Textures");
                    Directory.CreateDirectory(texturesDir);

                    var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, texturesDir, ".png", ".jpg", ".jpeg", ".tga");
                    fileSelector.UserData = "NormalField";
                    fileSelector.IsModal = true;
                    _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
                    return;
                }
            }
        }

        public NewMaterialPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            IsModal = true;
            RenderOrder = 1100;
            Scaling = ScalingMode.Fill;
            Size = new Vector2(420, 380);

            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
        }

        private void OnFileSelected(FileSelectedEvent e)
        {
            if (e.UserData == null) return;

            string field = e.UserData.ToString();
            string path = e.Path;

            if (field == "AlbedoField")
            {
                var albedoInput = _uiOverlay.FindElementById("mat-albedo") as InputElement;
                if (albedoInput != null)
                {
                    albedoInput.Value = path;
                    Console.WriteLine($"[NewMaterialPanel] Albedo field updated to: {path}");
                    _uiOverlay.RefreshUI();
                }
            }
            else if (field == "NormalField")
            {
                var normalInput = _uiOverlay.FindElementById("mat-normal") as InputElement;
                if (normalInput != null)
                {
                    normalInput.Value = path;
                    Console.WriteLine($"[NewMaterialPanel] Normal field updated to: {path}");
                    _uiOverlay.RefreshUI();
                }
            }
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new NewMaterialUIOverlay(this, _renderContext, _controlContext, _window, _eventBus);
        }

        public override void Init()
        {
            base.Init();

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NewMaterial.html");
            if (File.Exists(htmlPath))
            {
                string html = File.ReadAllText(htmlPath);
                _uiOverlay.LoadUI(html);
            }

            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        public override void Dispose()
        {
            // No Unsubscribe needed - panel is short-lived
            base.Dispose();
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new NewMaterialPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}