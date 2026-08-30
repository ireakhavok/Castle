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
        private bool _playing;
        private bool _lookCaptured;

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

        private void HandleDataHook(string hook)
        {
            if (hook == "PlayHost.Play") StartPlay();
            else if (hook == "PlayHost.Stop") StopPlay();
            else if (hook == "PlayHost.Build") BuildScripts();
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

            int w = Math.Max(64, (int)Size.X);
            int h = Math.Max(64, (int)Size.Y);
            _runtime = RuntimePlayStart.CreateScene(ctx, snap.LevelName);
            _runtime.Initialize(w, h);
            (_runtime as RuntimeGameplayScene)?.SetInputLive(false);
            _playing = true;
            _lookCaptured = false;
            Console.WriteLine("[PlayHostPanel] playing " + snap.LevelName);
        }

        private void StopPlay()
        {
            if (_lookCaptured)
            {
                PanelManager.Current?.ReleasePanelCapture();
                _lookCaptured = false;
            }
            _playing = false;
            (_runtime as RuntimeGameplayScene)?.SetInputLive(false);
            _runtime?.Dispose();
            _runtime = null;
            Console.WriteLine("[PlayHostPanel] stopped");
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
            (_runtime as RuntimeGameplayScene)?.SetInputLive(_lookCaptured);
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            if (_playing && _runtime != null)
            {
                if (!_lookCaptured)
                    (_runtime as RuntimeGameplayScene)?.SetInputLive(false);
                _runtime.Update(deltaTime);
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
