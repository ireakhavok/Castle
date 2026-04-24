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
        private bool _linkToPlayer = false;
        private bool _snapEnabled = true;
        private bool _spaceWasDown = false;
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
            _eventBus.Subscribe<FileSelectedEvent>(OnFileSelected);
            _uiOverlay.RefreshUI();
            _snapEnabled = _currentStack.SnapEnabled;
            UpdateGridMarkers();
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
            if (e.Hook == "BlendPointChanged" || e.Hook == "GridClicked")
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
                if (e.Hook == "GridClicked" && e.Data != null)
                {
                    if (e.Data.TryGetValue("x", out string xs) && float.TryParse(xs, out float gx) &&
                        e.Data.TryGetValue("y", out string ys) && float.TryParse(ys, out float gy))
                    {
                        _currentBlendPoint = new Vector3(gx, gy, _currentBlendPoint.Z);
                        UpdateGridMarkers();
                        if (e.Data.TryGetValue("button", out string btn) && btn == "right")
                        {
                            _previewScene.SetBlendPreview(_currentStack, _currentBlendPoint);
                        }
                        else
                        {
                            string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                            var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                            fileSelector.UserData = "AddBlendClipAtCurrentPoint";
                            fileSelector.IsModal = true;
                            _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
                        }
                    }
                }
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
                        AnimationTimelinePanel.Open(_renderContext, _controlContext, _window, _eventBus);
                        _eventBus.Publish(new GenericEvent { Hook = "OpenTimelineForClip", Data = new Dictionary<string, string> { { "path", clip.AnimationPath }, { "index", idx.ToString() } } });
                    }
                }
            }
            else if (e.Hook == "TimelineMetadataUpdated")
            {
                if (e.Data.TryGetValue("path", out string path))
                {
                    var clip = _currentStack.Clips.FirstOrDefault(c => c.AnimationPath == path);
                    if (clip != null)
                    {
                        if (e.Data.TryGetValue("start", out string s)) clip.StartFrame = float.Parse(s);
                        if (e.Data.TryGetValue("end", out string en)) clip.EndFrame = float.Parse(en);
                        if (e.Data.TryGetValue("speed", out string sp)) clip.PlaybackSpeed = float.Parse(sp);
                        _previewScene.SetBlendPreview(_currentStack, _currentBlendPoint);
                        UpdateGridMarkers();
                        _uiOverlay.RefreshUI();
                    }
                }
            }
            else if (e.Hook == "ToggleSnap")
            {
                _snapEnabled = !_snapEnabled;
                _currentStack.SnapEnabled = _snapEnabled;
                _uiOverlay.RefreshUI();
            }
            else if (e.Hook == "LinkToPlayer")
            {
                _linkToPlayer = !_linkToPlayer;
                _uiOverlay.RefreshUI();
            }
        }
        private void OnFileSelected(FileSelectedEvent e)
        {
            if (e.UserData?.ToString() == "AddBlendClipAtCurrentPoint" && !string.IsNullOrEmpty(e.Path))
            {
                _previewScene.LoadAnimation(e.Path);
                Vector3 coord = _currentBlendPoint;
                if (_currentStack.SnapEnabled) coord = _currentStack.SnapCoordinate(coord);
                _currentStack.AddClip(e.Path, coord);
                _previewScene.SetBlendPreview(_currentStack, _currentBlendPoint);
                _previewScene.TogglePlay();
                UpdateGridMarkers();
                _uiOverlay.RefreshUI();
            }
        }
        public void HandleUIClick(HtmlElement elem)
        {
            string hook = elem.Attributes.GetValueOrDefault("data-hook", "");
            if (hook == "AddClipAtPoint")
            {
                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                fileSelector.UserData = "AddBlendClipAtCurrentPoint";
                fileSelector.IsModal = true;
                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
            }
            else if (hook == "CreatePack")
            {
                CreateAnimationPack();
            }
            else if (hook.StartsWith("SetConfig"))
            {
                var xMaxEl = _uiOverlay.FindElementById("configXMax") as InputElement;
                var yMaxEl = _uiOverlay.FindElementById("configYMax") as InputElement;
                var zMaxEl = _uiOverlay.FindElementById("configZMax") as InputElement;
                if (xMaxEl != null) _currentStack.BlendConfig.XMax = float.Parse(xMaxEl.Value ?? "1");
                if (yMaxEl != null) _currentStack.BlendConfig.YMax = float.Parse(yMaxEl.Value ?? "1");
                if (zMaxEl != null) _currentStack.BlendConfig.ZMax = float.Parse(zMaxEl.Value ?? "2");
                _previewScene.SetBlendPreview(_currentStack, _currentBlendPoint);
                UpdateGridMarkers();
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
        private void UpdateGridMarkers()
        {
            var grid = _uiOverlay.FindElementById("blendGrid");
            if (grid == null) return;
            grid.Children.Clear();
            float cx = (_currentBlendPoint.X + 1f) / 2f * 100f;
            float cy = (_currentBlendPoint.Y + 1f) / 2f * 100f;
            var cur = new DivElement();
            cur.Style.SetProperty("position", "absolute");
            cur.Style.SetProperty("left", cx + "%");
            cur.Style.SetProperty("top", cy + "%");
            cur.Style.SetProperty("width", "12px");
            cur.Style.SetProperty("height", "12px");
            cur.Style.SetProperty("background", "#4ade80");
            cur.Style.SetProperty("border", "2px solid #fff");
            cur.Style.SetProperty("border-radius", "50%");
            cur.Style.SetProperty("transform", "translate(-50%, -50%)");
            cur.Style.SetProperty("box-shadow", "0 0 10px #4ade80");
            cur.Style.SetProperty("pointer-events", "none");
            cur.Style.SetProperty("z-index", "5");
            grid.Children.Add(cur);
            foreach (var clip in _currentStack.Clips)
            {
                float px = (clip.BlendCoordinate.X + 1f) / 2f * 100f;
                float py = (clip.BlendCoordinate.Y + 1f) / 2f * 100f;
                var dot = new DivElement();
                dot.Style.SetProperty("position", "absolute");
                dot.Style.SetProperty("left", px + "%");
                dot.Style.SetProperty("top", py + "%");
                dot.Style.SetProperty("width", "10px");
                dot.Style.SetProperty("height", "10px");
                dot.Style.SetProperty("background", "#f59e0b");
                dot.Style.SetProperty("border", "1px solid #fff");
                dot.Style.SetProperty("border-radius", "50%");
                dot.Style.SetProperty("transform", "translate(-50%, -50%)");
                dot.Style.SetProperty("z-index", "4");
                grid.Children.Add(dot);
            }
            _uiOverlay.RefreshUI();
        }
        public override bool WantsContinuousUpdate => true;
        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            bool spaceDown = _controlContext.GetKey(_window, Key.Space) == InputAction.Press;
            if (!spaceDown && _spaceWasDown)
            {
                _previewScene.TogglePlay();
            }
            _spaceWasDown = spaceDown;
            Vector2 relMouse = absMousePos - Position;
            Vector2 sceneMouse = new Vector2(relMouse.X, relMouse.Y - HeaderHeight);
            _previewScene.Update(deltaTime, sceneMouse, mouseDown, mousePressed, mouseReleased);
            if (_linkToPlayer)
            {
                Vector2 simulatedInput = new Vector2(
                    (float)Math.Sin(_controlContext.GetTime() * 0.8f) * 0.7f,
                    (float)Math.Cos(_controlContext.GetTime() * 0.6f) * 0.9f);
                _currentBlendPoint = _currentStack.MapPlayerInputToBlendCoord(simulatedInput, 0f);
                _previewScene.SetBlendPreview(_currentStack, _currentBlendPoint);
                UpdateGridMarkers();
            }
            var gridElem = _uiOverlay.FindElementById("blendGrid");
            if (gridElem != null && mousePressed)
            {
                float gx = gridElem.ComputedPosition.X;
                float gy = gridElem.ComputedPosition.Y;
                float gw = gridElem.ComputedWidth;
                float gh = gridElem.ComputedHeight;
                if (relMouse.X >= gx && relMouse.X <= gx + gw &&
                    relMouse.Y >= gy && relMouse.Y <= gy + gh)
                {
                    float normX = (relMouse.X - gx) / gw * 2f - 1f;
                    float normY = (relMouse.Y - gy) / gh * 2f - 1f;
                    _currentBlendPoint = new Vector3(normX, normY, _currentBlendPoint.Z);
                    UpdateGridMarkers();
                    if (mouseDown)
                    {
                        string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                        var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                        fileSelector.UserData = "AddBlendClipAtCurrentPoint";
                        fileSelector.IsModal = true;
                        _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
                    }
                }
            }
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