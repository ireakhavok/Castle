using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Scenes;
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
        private FBXModel _loadedAnimModel;
        private int _keyframeCount = 0;
        private float _animDuration = 10f;
        private bool _draggingStart = false;
        private bool _draggingEnd = false;

        private ModelViewerScene _previewScene;

        public AnimationTimelinePanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            DockingMode = SiegeEngine.Core.Definitions.DockingMode.IDE;
            BaseWidth = 1100f;
            BaseHeight = 520f;
            _previewScene = new ModelViewerScene(renderContext, controlContext, window, new ClientGameServerProxy(eventBus), eventBus);
        }

        protected override UIOverlay CreateUIOverlay() => new TimelineUIOverlay(this, _renderContext, _controlContext, _window, _eventBus);

        public override void Init()
        {
            base.Init();
            _previewScene.Initialize((int)Size.Y, (int)Size.X);   // MATCH AnimationBlendPanel: full panel size for correct aspect ratio/viewport (0.55f hack was causing fucked up preview when floating/docked)
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
                    LoadAnimationForTimeline(path);
                    _uiOverlay.RefreshUI();
                }
            }
            else if (e.Hook == "TimelineCut")
            {
                var startEl = _uiOverlay.FindElementById("startFrame") as InputElement;
                var endEl = _uiOverlay.FindElementById("endFrame") as InputElement;
                var speedEl = _uiOverlay.FindElementById("speed") as RangeElement;

                if (startEl != null && float.TryParse(startEl.Value, out float s)) _startFrame = s;
                if (endEl != null && float.TryParse(endEl.Value, out float en)) _endFrame = en;
                if (speedEl != null) _speed = speedEl.Value;

                _eventBus.Publish(new GenericEvent
                {
                    Hook = "TimelineMetadataUpdated",
                    Data = new Dictionary<string, string>
                    {
                        { "path", _currentClipPath },
                        { "start", _startFrame.ToString() },
                        { "end", _endFrame.ToString() },
                        { "speed", _speed.ToString() }
                    }
                });
            }
        }

        private void LoadAnimationForTimeline(string path)
        {
            try
            {
                FBXFileForest forest = FBXParser.Load(path);
                _loadedAnimModel = FBXParser.BuildModelFromForest(forest);
                if (_loadedAnimModel != null && _loadedAnimModel.Animations.Count > 0)
                {
                    var anim = _loadedAnimModel.Animations[0];
                    _animDuration = anim.Duration > 0 ? anim.Duration : 10f;
                    _keyframeCount = anim.Keyframes.Count;
                    _endFrame = _animDuration;
                    _startFrame = 0f;
                    _scrubTime = 0f;

                    _previewScene.LoadAnimation(path);
                    _previewScene.TogglePlay();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AnimationTimelinePanel] Failed to load animation: {ex.Message}");
            }
        }

        public void HandleUIClick(HtmlElement elem) { }

        public override bool WantsContinuousUpdate => true;

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);

            Vector2 relMouse = absMousePos - Position;
            Vector2 sceneMouse = new Vector2(relMouse.X, relMouse.Y - HeaderHeight);
            _previewScene.Update(deltaTime, sceneMouse, mouseDown, mousePressed, mouseReleased);

            float timelineLeft = Position.X + 30f;
            float timelineRight = Position.X + Size.X - 30f;

            if (mousePressed)
            {
                float relX = (absMousePos.X - timelineLeft) / (timelineRight - timelineLeft);
                if (Math.Abs(relX - (_startFrame / _animDuration)) < 0.04f)
                    _draggingStart = true;
                else if (Math.Abs(relX - (_endFrame / _animDuration)) < 0.04f)
                    _draggingEnd = true;
                else if (absMousePos.X > timelineLeft && absMousePos.X < timelineRight)
                {
                    float t = Math.Clamp((absMousePos.X - timelineLeft) / (timelineRight - timelineLeft), 0f, 1f);
                    _scrubTime = t * _animDuration;
                }
            }

            if (_draggingStart && mouseDown)
            {
                float t = Math.Clamp((absMousePos.X - timelineLeft) / (timelineRight - timelineLeft), 0f, 1f);
                _startFrame = t * _animDuration;
                if (_startFrame > _endFrame) _startFrame = _endFrame;
            }

            if (_draggingEnd && mouseDown)
            {
                float t = Math.Clamp((absMousePos.X - timelineLeft) / (timelineRight - timelineLeft), 0f, 1f);
                _endFrame = t * _animDuration;
                if (_endFrame < _startFrame) _endFrame = _startFrame;
            }

            if (mouseReleased)
            {
                _draggingStart = false;
                _draggingEnd = false;
            }
        }

        protected override void RenderInnerContent()
        {
            _previewScene.Render(null);
        }

        public override void OnLiveResize(float w, float h)
        {
            _previewScene.Resize((int)w, (int)h);   // MATCH AnimationBlendPanel: full dimensions for correct aspect ratio/viewport in floating + docked states
            base.OnLiveResize(w, h);
        }

        public string DataKey => "AnimationTimelinePanel";
        public JsonElement SavePanelState() => JsonSerializer.SerializeToElement(new { Path = _currentClipPath, Start = _startFrame, End = _endFrame, Speed = _speed, Loop = _loop, Keyframes = _keyframeCount });
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

        public override void Dispose()
        {
            _previewScene.Dispose();
            base.Dispose();
        }
    }
}