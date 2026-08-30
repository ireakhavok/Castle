// Folder: CastleBuilder
// File: PlayHostPanel.cs
using Keystone;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Lighting;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.UI;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Scenes;
using SiegeEngine.Systems;
using System;
using System.IO;
using System.Numerics;

namespace CastleBuilder
{
    public class PlayHostPanel : BasePanel
    {
        private class PlayHostOverlay : UIOverlay
        {
            private readonly PlayHostPanel _parent;
            public PlayHostOverlay(PlayHostPanel parent, IRenderContext rc, IControlContext cc, nint window)
                : base(rc, cc, window) { _parent = parent; }
            protected override void HandleDataHook(string hook) { _parent.HandleDataHook(hook); }
        }

        private GameScene _runtime;
        private IGameServer _runtimeServer;
        private bool _playing;
        private bool _lookCaptured;
        private bool _muted;
        private bool _wasLive;

        public override bool WantsContinuousUpdate => _playing;

        public PlayHostPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            ChromeStyle = PanelChromeStyle.Editor;
            Scaling = ScalingMode.BestFit;
            DockingMode = DockingMode.IDE;
            BaseWidth = 1280f;
            BaseHeight = 720f;
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            eventBus.Publish(new OpenPanelEvent(new PlayHostPanel(renderContext, controlContext, window, eventBus)) { Mode = OpenMode.Overlay });
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new PlayHostOverlay(this, _renderContext, _controlContext, _window);
        }

        public override void Init()
        {
            base.Init();
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlayHostUI.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine("[PlayHostPanel] PlayHostUI.html not found next to the exe");
                return;
            }
            _uiOverlay.LoadUI(File.ReadAllText(htmlPath), Path.GetDirectoryName(htmlPath) ?? "");
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        private AudioSystem HostedAudio =>
            (_runtime as RuntimeGameplayScene)?.HostedAudio ?? _runtimeServer?.GetSystem<AudioSystem>();

        private void HandleDataHook(string hook)
        {
            if (hook == "PlayHost.Play") StartPlay();
            else if (hook == "PlayHost.Stop") StopPlay();
            else if (hook == "PlayHost.Build") BuildScripts();
            else if (hook == "PlayHost.Mute") ToggleMute();
        }

        private void StartPlay()
        {
            if (_playing) return;
            EditorScene.Current?.FlushActiveSceneData();
            string projectPath = ProjectSettings.Current?.ActiveProject ?? string.Empty;
            if (string.IsNullOrEmpty(projectPath))
            {
                Console.WriteLine("[PlayHostPanel] no project");
                return;
            }
            try
            {
                ScriptLoader.BuildProjectScripts(projectPath);
                ScriptLoader.CopyProjectScripts(projectPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PlayHostPanel] Build failed: " + ex.Message);
                return;
            }

            var snap = BlueprintManager.BuildCurrentPlaySnapshot();
            var input = new InputHandler(_controlContext, _window, null);
            var ctx = RuntimePlayStart.BuildContext(
                _renderContext, _controlContext, _window, _eventBus, input,
                projectPath, snap.LevelName, snap.Level, snap.SceneData, snap.Heightmap,
                panelHosted: true);
            _runtimeServer = ctx.Server;

            int w = Math.Max(64, (int)Size.X);
            int h = Math.Max(64, (int)Size.Y);
            _runtime = RuntimePlayStart.CreateScene(ctx, snap.LevelName);
            _runtime.Initialize(w, h);

            _playing = true;
            _lookCaptured = true;
            _wasLive = true;
            PanelManager.Current?.CapturePanel(this);
            (_runtime as RuntimeGameplayScene)?.SetInputLive(true);
            (_runtime as RuntimeGameplayScene)?.SetPaused(false);
            if (_muted) HostedAudio?.SetMuted(true);
            Console.WriteLine("[PlayHostPanel] playing " + snap.LevelName);
        }

        private void StopPlay()
        {
            if (_lookCaptured)
            {
                PanelManager.Current?.ReleasePanelCapture();
                _lookCaptured = false;
            }
            try { HostedAudio?.StopAll(); } catch { }
            try { HostedAudio?.Dispose(); } catch { }
            _playing = false;
            _wasLive = false;
            (_runtime as RuntimeGameplayScene)?.SetInputLive(false);
            _runtime?.Dispose();
            _runtime = null;
            _runtimeServer = null;
            Console.WriteLine("[PlayHostPanel] stopped");
        }

        private void ToggleMute()
        {
            _muted = !_muted;
            HostedAudio?.SetMuted(_muted);
            Console.WriteLine("[PlayHostPanel] muted=" + _muted);
        }

        private void BuildScripts()
        {
            string projectPath = ProjectSettings.Current?.ActiveProject ?? string.Empty;
            if (string.IsNullOrEmpty(projectPath))
            {
                Console.WriteLine("[PlayHostPanel] no project");
                return;
            }
            try
            {
                ScriptLoader.BuildProjectScripts(projectPath);
                Console.WriteLine("[PlayHostPanel] built");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PlayHostPanel] Build failed: " + ex.Message);
            }
        }

        public override void ToggleCameraMode()
        {
            if (!_playing) return;
            _lookCaptured = !_lookCaptured;
            if (_lookCaptured) PanelManager.Current.CapturePanel(this);
            else PanelManager.Current.ReleasePanelCapture();
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            if (_playing && _runtime != null)
            {
                bool focused = true;
                try { focused = _controlContext.GetWindowAttrib(_window, WindowAttribute.Focused); }
                catch { }
                bool live = _lookCaptured && focused;
                var hosted = _runtime as RuntimeGameplayScene;
                hosted?.SetPaused(!live);
                hosted?.SetInputLive(live);
                if (live)
                {
                    if (!_wasLive) HostedAudio?.SetMuted(_muted);
                    hosted?.SetScrollDelta(scrollDelta);
                    _runtime.Update(deltaTime);
                }
                else if (_wasLive)
                {
                    HostedAudio?.PauseAll();
                }
                _wasLive = live;
            }
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
        }

        protected override void RenderInnerContent()
        {
            if (_runtime == null) return;
            var env = ProjectSettings.Current?.CurrentLevel?.Environment;
            if (env != null)
                LightingSettings.BindAuthored(env);
            _runtime.Render(_runtime.GetEntities());
        }

        public override void OnLiveResize(float w, float h)
        {
            _runtime?.Resize((int)w, (int)h);
            base.OnLiveResize(w, h);
        }

        public override void Dispose()
        {
            StopPlay();
            base.Dispose();
        }
    }
}
