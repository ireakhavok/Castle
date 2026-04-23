using Keystone;
using ReadingChamber;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Scenes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace ToolChest
{
    public class AnimationBlendPanel : BasePanel, IDataAwarePanel
    {
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new AnimationBlendPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }

        private class BlendUIOverlay : UIOverlay
        {
            private readonly AnimationBlendPanel _parent;
            public BlendUIOverlay(AnimationBlendPanel parent, IRenderContext rc, IControlContext cc, nint w, EventBus eb)
                : base(rc, cc, w, eb) { _parent = parent; }

            public override bool HandleUIClick(HtmlElement elem)
            {
                bool handled = base.HandleUIClick(elem);
                _parent.HandleUIClick(elem);
                return handled;
            }
        }

        private AnimationBlendStack _currentStack = new AnimationBlendStack();
        private ModelViewerScene _previewScene;
        private Vector3 _currentBlendPoint = Vector3.Zero;

        public AnimationBlendPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            DockingMode = SiegeEngine.Core.Definitions.DockingMode.IDE;
            BaseWidth = 1100f;
            BaseHeight = 720f;
            _previewScene = new ModelViewerScene(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus);
        }

        protected override UIOverlay CreateUIOverlay() => new BlendUIOverlay(this, _renderContext, _controlContext, _window, _eventBus);

        public override void Init()
        {
            base.Init();
            _previewScene.Initialize((int)Size.Y, (int)Size.X);
            LoadUIFromFile("AnimationBlendUI.html");
            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
            _uiOverlay.RefreshUI();
        }

        private void LoadUIFromFile(string filename)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            if (File.Exists(path))
            {
                string html = File.ReadAllText(path);
                _uiOverlay.LoadUI(html);
            }
        }

        private void OnGenericEvent(GenericEvent e)
        {
            if (e.Hook == "BlendPointChanged")
            {
                var xEl = _uiOverlay.FindElementById("blendX") as InputElement;
                var yEl = _uiOverlay.FindElementById("blendY") as InputElement;
                var zEl = _uiOverlay.FindElementById("blendZ") as RangeElement;
                if (xEl != null && yEl != null && zEl != null)
                {
                    _currentBlendPoint = new Vector3(
                        float.Parse(xEl.Value ?? "0"),
                        float.Parse(yEl.Value ?? "0"),
                        float.Parse(zEl.Value.ToString() ?? "0"));
                }
            }
            else if (e.Hook == "AddClipAtPoint")
            {
                var fs = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, "Assets", ".fbx");
                fs.UserData = "AddBlendClipAtCurrentPoint";
                fs.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fs) { Mode = OpenMode.Overlay });
            }
            else if (e.Hook == "SelectClipForTimeline")
            {
                var select = _uiOverlay.FindElementById("clipList") as SelectElement;
                if (select != null && !string.IsNullOrEmpty(select.Value))
                {
                    int idx = int.Parse(select.Value);
                    if (idx >= 0 && idx < _currentStack.Clips.Count)
                    {
                        var clip = _currentStack.Clips[idx];
                        _eventBus.Publish(new GenericEvent { Hook = "OpenTimelineForClip", Data = new Dictionary<string, string> { { "path", clip.AnimationPath }, { "index", idx.ToString() } } });
                    }
                }
            }
        }

        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook == "CreatePack")
            {
                CreateAnimationPack();
            }
        }

        private void CreateAnimationPack()
        {
            var entity = new Entity { Type = "BlendedAnimation" };
            entity.AddComponent(new ModelComponent { Model = _previewScene._model, Key = _currentStack.Name });
            entity.AddComponent(new BlendedAnimationComponent { BlendStack = _currentStack, CurrentBlendParams = _currentBlendPoint });
            _eventBus.Publish(new EntityPlacedEvent(entity.Id, "BlendedAnimation", entity.Transform.Position, false, null));
            Console.WriteLine("[AnimationBlendPanel] 3D Animation pack entity created and placed");
        }

        public override bool WantsContinuousUpdate => true;

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            Vector2 relMouse = absMousePos - Position;
            Vector2 sceneMouse = new Vector2(relMouse.X, relMouse.Y - HeaderHeight);
            _previewScene.Update(deltaTime, sceneMouse, mouseDown, mousePressed, mouseReleased);
        }

        protected override void RenderInnerContent()
        {
            _previewScene.Render(null);
        }

        public override void OnLiveResize(float w, float h)
        {
            _previewScene.Resize((int)w, (int)h);
            base.OnLiveResize(w, h);
        }

        public string DataKey => "AnimationBlendPanel";
        public JsonElement SavePanelState() => JsonSerializer.SerializeToElement(_currentStack);
        public void LoadPanelState(JsonElement state)
        {
            if (!state.ValueKind.HasFlag(JsonValueKind.Undefined))
                _currentStack = JsonSerializer.Deserialize<AnimationBlendStack>(state.GetRawText()) ?? new AnimationBlendStack();
        }

        public override void Dispose()
        {
            _previewScene.Dispose();
            base.Dispose();
        }
    }
}