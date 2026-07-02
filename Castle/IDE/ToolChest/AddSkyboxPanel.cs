// Folder: ToolChest
// File: AddSkyboxPanel.cs
using Keystone;
using ReadingChamber;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
namespace ToolChest
{
    public class AddSkyboxPanel : BasePanel
    {
        private class AddSkyboxUIOverlay : UIOverlay
        {
            private readonly AddSkyboxPanel _parent;
            private readonly EventBus _eventBus;
            public AddSkyboxUIOverlay(AddSkyboxPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus) : base(renderContext, controlContext, window)
            {
                _parent = parent;
                _eventBus = eventBus;
            }
            protected override void HandleDataHook(string hook)
            {
                if (hook == "AddSkyboxConfirm")
                {
                    var typeSelect = FindElementById("skybox-type") as SelectElement;
                    var cubemapPath = FindElementById("cubemap-path") as InputElement;
                    var face0 = FindElementById("face0") as InputElement;
                    var face1 = FindElementById("face1") as InputElement;
                    var face2 = FindElementById("face2") as InputElement;
                    var face3 = FindElementById("face3") as InputElement;
                    var face4 = FindElementById("face4") as InputElement;
                    var face5 = FindElementById("face5") as InputElement;
                    SkyboxData sky = new SkyboxData
                    {
                        Enabled = true,
                        Type = typeSelect?.Value ?? "Cubemap",
                        CubemapPath = cubemapPath?.Value ?? "",
                        Faces = new List<string> { face0?.Value ?? "", face1?.Value ?? "", face2?.Value ?? "", face3?.Value ?? "", face4?.Value ?? "", face5?.Value ?? "" }
                    };
                    _eventBus.Publish(new GenericEvent { Hook = "SkyboxSet", Data = new Dictionary<string, string> { { "skybox", JsonSerializer.Serialize(sky) } } });
                    _eventBus.Publish(new GenericEvent { Hook = "ProjectSaveRequest" });
                    _eventBus.Publish(new ClosePanelEvent(_parent));
                    return;
                }
                if (hook == "CancelSkybox")
                {
                    _eventBus.Publish(new ClosePanelEvent(_parent));
                }
                if (hook.StartsWith("PickFace"))
                {
                    int idx = int.Parse(hook.Substring(8));
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Skyboxes");
                    Directory.CreateDirectory(dir);
                    var selector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, dir, ".png", ".jpg", ".tga");
                    selector.UserData = $"SkyFace{idx}";
                    selector.IsModal = true;
                    _eventBus.Publish(new OpenPanelEvent(selector) { Mode = OpenMode.Overlay });
                }
                if (hook == "PickCubemap")
                {
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Skyboxes");
                    Directory.CreateDirectory(dir);
                    var selector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, dir, ".dds", ".png", ".jpg", ".tga");
                    selector.UserData = "SkyCubemap";
                    selector.IsModal = true;
                    _eventBus.Publish(new OpenPanelEvent(selector) { Mode = OpenMode.Overlay });
                }
            }
        }
        public AddSkyboxPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus) : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            IsModal = true;
            RenderOrder = 1200;
            Scaling = ScalingMode.Fill;
            Size = new Vector2(520, 420);
        }
        protected override UIOverlay CreateUIOverlay()
        {
            return new AddSkyboxUIOverlay(this, _renderContext, _controlContext, _window, _eventBus);
        }
        public override void Init()
        {
            base.Init();
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AddSkybox.html");
            if (File.Exists(htmlPath))
            {
                _uiOverlay.LoadUI(File.ReadAllText(htmlPath));
            }
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new AddSkyboxPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}