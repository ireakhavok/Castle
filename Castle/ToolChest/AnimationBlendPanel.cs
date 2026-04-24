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
        private int _draggingClipIndex = -1;
        private bool _draggingCurrentPoint = false;
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
                if (_draggingCurrentPoint) return; // completely ignore all external updates while dragging green

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
            if (e.UserData?.ToString()?.StartsWith("AddBlendClipAt:") == true && !string.IsNullOrEmpty(e.Path))
            {
                _previewScene.LoadAnimation(e.Path);
                string data = e.UserData.ToString().Substring("AddBlendClipAt:".Length);
                var parts = data.Split(',');
                float x = float.Parse(parts[0]);
                float y = float.Parse(parts[1]);
                Vector3 coord = new Vector3(x, y, _currentBlendPoint.Z);
                if (_currentStack.SnapEnabled) coord = _currentStack.SnapCoordinate(coord);
                _currentStack.AddClip(e.Path, coord);
                _previewScene.SetBlendPreview(_currentStack, _currentBlendPoint);
                _previewScene.TogglePlay();
                UpdateGridMarkers();
                _uiOverlay.RefreshUI();
            }
            else if (e.UserData?.ToString() == "AddBlendClipAtCurrentPoint" && !string.IsNullOrEmpty(e.Path))
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
            if (gridElem != null && gridElem.ComputedWidth > 0 && gridElem.ComputedHeight > 0)
            {
                float gx = gridElem.ComputedPosition.X;
                float gy = gridElem.ComputedPosition.Y;
                float gw = gridElem.ComputedWidth;
                float gh = gridElem.ComputedHeight;
                bool overGrid = relMouse.X >= gx && relMouse.X <= gx + gw && relMouse.Y >= gy && relMouse.Y <= gy + gh;
                if (overGrid && mousePressed)
                {
                    float cx = gx + ((_currentBlendPoint.X + 1f) / 2f * gw);
                    float cy = gy + ((_currentBlendPoint.Y + 1f) / 2f * gh);
                    bool hitGreen = Math.Abs(relMouse.X - cx) < 14 && Math.Abs(relMouse.Y - cy) < 14;
                    if (hitGreen)
                    {
                        if (mouseDown) _draggingCurrentPoint = true;
                    }
                    else
                    {
                        int hitIndex = -1;
                        for (int i = 0; i < _currentStack.Clips.Count; i++)
                        {
                            var clip = _currentStack.Clips[i];
                            float px = gx + ((clip.BlendCoordinate.X + 1f) / 2f * gw);
                            float py = gy + ((clip.BlendCoordinate.Y + 1f) / 2f * gh);
                            if (Math.Abs(relMouse.X - px) < 14 && Math.Abs(relMouse.Y - py) < 14)
                            {
                                hitIndex = i;
                                break;
                            }
                        }
                        if (hitIndex >= 0)
                        {
                            if (mouseDown) _draggingClipIndex = hitIndex;
                            else if (mouseReleased)
                            {
                                if (_draggingClipIndex == hitIndex)
                                {
                                    AnimationTimelinePanel.Open(_renderContext, _controlContext, _window, _eventBus);
                                    _eventBus.Publish(new GenericEvent
                                    {
                                        Hook = "OpenTimelineForClip",
                                        Data = new Dictionary<string, string> { { "path", _currentStack.Clips[hitIndex].AnimationPath }, { "index", hitIndex.ToString() } }
                                    });
                                }
                                _draggingClipIndex = -1;
                            }
                        }
                        else
                        {
                            float normX = (relMouse.X - gx) / gw * 2f - 1f;
                            float normY = (relMouse.Y - gy) / gh * 2f - 1f;
                            _currentBlendPoint = new Vector3(normX, normY, _currentBlendPoint.Z);
                            UpdateGridMarkers();
                            if (mouseDown)
                            {
                                string initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                                var fileSelector = new FileSelectorPanel(_renderContext, _controlContext, _window, _eventBus, initialDir, ".fbx");
                                fileSelector.UserData = $"AddBlendClipAt:{normX},{normY}";
                                fileSelector.IsModal = true;
                                _eventBus.Publish(new OpenPanelEvent(fileSelector) { Mode = OpenMode.Overlay });
                            }
                        }
                    }
                }
                if (_draggingCurrentPoint && !mouseReleased)
                {
                    float normX = (relMouse.X - gx) / gw * 2f - 1f;
                    float normY = (relMouse.Y - gy) / gh * 2f - 1f;
                    _currentBlendPoint = new Vector3(normX, normY, _currentBlendPoint.Z);
                    _previewScene.SetBlendPreview(_currentStack, _currentBlendPoint);
                    UpdateGridMarkers();
                }
                if (_draggingClipIndex >= 0 && !mouseReleased)
                {
                    float normX = (relMouse.X - gx) / gw * 2f - 1f;
                    float normY = (relMouse.Y - gy) / gh * 2f - 1f;
                    var clip = _currentStack.Clips[_draggingClipIndex];
                    clip.BlendCoordinate = new Vector3(normX, normY, clip.BlendCoordinate.Z);
                    UpdateGridMarkers();
                }
                if (mouseReleased)
                {
                    // Final safeguard: keep whatever position the green dot had on release
                    if (_draggingCurrentPoint)
                    {
                        // position is already correct from the last drag frame
                    }
                    _draggingCurrentPoint = false;
                    _draggingClipIndex = -1;
                }
            }
        }
        protected override void RenderInnerContent()
        {
            _previewScene.Render(null);
        }
        public override void Render()
        {
            base.Render();
            var gridElem = _uiOverlay.FindElementById("blendGrid");
            if (gridElem != null && gridElem.ComputedWidth > 0 && gridElem.ComputedHeight > 0)
            {
                float gx = gridElem.ComputedPosition.X;
                float gy = gridElem.ComputedPosition.Y;
                float gw = gridElem.ComputedWidth;
                float gh = gridElem.ComputedHeight;
                float cx = gx + ((_currentBlendPoint.X + 1f) / 2f * gw);
                float cy = gy + ((_currentBlendPoint.Y + 1f) / 2f * gh);
                _quadRenderer.DrawQuad(cx - 6, cy - 6, 12, 12, new Vector4(0.29f, 0.87f, 0.5f, 1f), Size.X, Size.Y);
                _quadRenderer.DrawQuad(cx - 7, cy - 7, 14, 14, new Vector4(1f, 1f, 1f, 1f), Size.X, Size.Y);
                foreach (var clip in _currentStack.Clips)
                {
                    float px = gx + ((clip.BlendCoordinate.X + 1f) / 2f * gw);
                    float py = gy + ((clip.BlendCoordinate.Y + 1f) / 2f * gh);
                    _quadRenderer.DrawQuad(px - 5, py - 5, 10, 10, new Vector4(0.96f, 0.62f, 0.04f, 1f), Size.X, Size.Y);
                    _quadRenderer.DrawQuad(px - 6, py - 6, 12, 12, new Vector4(1f, 1f, 1f, 1f), Size.X, Size.Y);
                }
            }
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