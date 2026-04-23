using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace ToolChest
{
    public class AnimationTimelinePanel : BasePanel, IDataAwarePanel
    {
        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new AnimationTimelinePanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Replace });
        }

        private class TimelineUIOverlay : UIOverlay
        {
            private readonly AnimationTimelinePanel _parent;
            public TimelineUIOverlay(AnimationTimelinePanel parent, IRenderContext rc, IControlContext cc, nint w, EventBus eb)
                : base(rc, cc, w, eb) { _parent = parent; }
            public override bool HandleUIClick(HtmlElement elem)
            {
                bool h = base.HandleUIClick(elem);
                _parent.HandleUIClick(elem);
                return h;
            }
        }

        private string _currentClipPath = "";
        private float _startFrame = 0f;
        private float _endFrame = 10f;
        private float _speed = 1f;
        private bool _loop = true;
        private float _scrubTime = 0f;

        public AnimationTimelinePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            DockingMode = SiegeEngine.Core.Definitions.DockingMode.IDE;
            BaseWidth = 900f;
            BaseHeight = 280f;
        }

        protected override UIOverlay CreateUIOverlay() => new TimelineUIOverlay(this, _renderContext, _controlContext, _window, _eventBus);

        public override void Init()
        {
            base.Init();
            LoadUIFromFile("AnimationTimelineUI.html");
            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
            _uiOverlay.RefreshUI();
        }

        private void LoadUIFromFile(string filename)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            if (File.Exists(path)) _uiOverlay.LoadUI(File.ReadAllText(path));
        }

        private void OnGenericEvent(GenericEvent e)
        {
            if (e.Hook == "OpenTimelineForClip")
            {
                if (e.Data.TryGetValue("path", out string path))
                {
                    _currentClipPath = path;
                    _uiOverlay.RefreshUI();
                }
            }
            else if (e.Hook == "TimelineCut")
            {
                var startEl = _uiOverlay.FindElementById("startFrame") as RangeElement;
                var endEl = _uiOverlay.FindElementById("endFrame") as RangeElement;
                var speedEl = _uiOverlay.FindElementById("speed") as RangeElement;
                if (startEl != null) _startFrame = startEl.Value;
                if (endEl != null) _endFrame = endEl.Value;
                if (speedEl != null) _speed = speedEl.Value;
                _eventBus.Publish(new GenericEvent { Hook = "TimelineMetadataUpdated", Data = new Dictionary<string, string> { { "path", _currentClipPath }, { "start", _startFrame.ToString() }, { "end", _endFrame.ToString() }, { "speed", _speed.ToString() } } });
            }
        }

        public void HandleUIClick(HtmlElement elem) { }

        public override bool WantsContinuousUpdate => true;

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            if (mouseDown && IsMouseOver(absMousePos))
            {
                float relX = (absMousePos.X - Position.X) / Size.X;
                _scrubTime = _startFrame + relX * (_endFrame - _startFrame);
            }
        }

        protected override void RenderInnerContent()
        {
            _quadRenderer.DrawQuad(Position.X + 20f, Position.Y + 60f, Size.X - 40f, 8f, new Vector4(0.2f, 0.2f, 0.2f, 1f), Size.X, Size.Y);
            float progress = (_endFrame - _startFrame > 0) ? (_scrubTime - _startFrame) / (_endFrame - _startFrame) : 0f;
            _quadRenderer.DrawQuad(Position.X + 20f, Position.Y + 60f, Size.X * progress, 8f, new Vector4(0.4f, 0.8f, 0.4f, 1f), Size.X, Size.Y);
        }

        public string DataKey => "AnimationTimelinePanel";
        public JsonElement SavePanelState() => JsonSerializer.SerializeToElement(new { Path = _currentClipPath, Start = _startFrame, End = _endFrame, Speed = _speed, Loop = _loop });
        public void LoadPanelState(JsonElement state)
        {
            if (!state.ValueKind.HasFlag(JsonValueKind.Undefined))
            {
                var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(state.GetRawText());
                _currentClipPath = obj.GetValueOrDefault("Path", "").ToString();
                _startFrame = float.Parse(obj.GetValueOrDefault("Start", "0").ToString());
                _endFrame = float.Parse(obj.GetValueOrDefault("End", "10").ToString());
                _speed = float.Parse(obj.GetValueOrDefault("Speed", "1").ToString());
            }
        }
    }
}