using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Systems;
using System;
using System.IO;
using System.Numerics;

namespace CastleBuilder
{
    public class MusicPlayerPanel : BasePanel
    {
        private class MusicUIOverlay : UIOverlay
        {
            private readonly MusicPlayerPanel _parent;
            private readonly EventBus _eventBus;

            public MusicUIOverlay(MusicPlayerPanel parent, IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
                : base(renderContext, controlContext, window, eventBus)
            {
                _parent = parent;
                _eventBus = eventBus;
            }

            protected override void HandleDataHook(string hook)
            {
                if (hook == "Music.Play")
                {
                    _parent.ResumeMusic();
                    return;
                }
                if (hook == "Music.Pause")
                {
                    _parent.PauseMusic();
                    return;
                }
                if (hook == "Music.Next")
                {
                    _parent.NextTrack();
                    return;
                }
                if (hook == "Music.Prev")
                {
                    _parent.PreviousTrack();
                    return;
                }
                if (hook == "Music.Close" || hook == "Close")
                {
                    _eventBus.Publish(new ClosePanelEvent(_parent));
                    return;
                }
                base.HandleDataHook(hook);
            }
        }

        private AudioSystem _audio;
        private ClientGameServerProxy _serverProxy;

        public MusicPlayerPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            BaseWidth = 380f;
            BaseHeight = 56f;

            HasTitleBar = true;
            IsClosable = true;
            IsModal = false;
            AllowDragging = true;
            Dockable = false;
            DockState = DockState.Floating;
            DockingMode = DockingMode.Dynamic;   // enum value – using SiegeEngine.Core.Definitions brings it into scope cleanly
            RenderOrder = 2000;
            Scaling = ScalingMode.BestFit;
            Size = new Vector2(BaseWidth, BaseHeight);
        }

        protected override UIOverlay CreateUIOverlay()
        {
            return new MusicUIOverlay(this, _renderContext, _controlContext, _window, _eventBus);
        }

        public override void Init()
        {
            base.Init();

            Size = new Vector2(BaseWidth, BaseHeight);

            _controlContext.GetWindowSize(_window, out int winW, out int winH);
            Position = new Vector2((winW - Size.X) * 0.5f, 40f);

            string html = @"
<div style='width:100%;height:100%;background:#1f1f1f;padding:4px 10px;display:flex;align-items:center;gap:8px;'>
  <div id='music-title' style='color:#7CFFCB;font-size:12px;font-weight:600;flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;'>Loading...</div>
  <div data-hook='Music.Prev' style='padding:2px 8px;cursor:pointer;color:#7CFFCB;border:1px solid #383838;border-radius:3px;font-size:11px;'>Prev</div>
  <div data-hook='Music.Play' style='padding:2px 8px;cursor:pointer;color:#7CFFCB;border:1px solid #383838;border-radius:3px;font-size:11px;'>Play</div>
  <div data-hook='Music.Pause' style='padding:2px 8px;cursor:pointer;color:#7CFFCB;border:1px solid #383838;border-radius:3px;font-size:11px;'>Pause</div>
  <div data-hook='Music.Next' style='padding:2px 8px;cursor:pointer;color:#7CFFCB;border:1px solid #383838;border-radius:3px;font-size:11px;'>Next</div>
</div>";

            _uiOverlay.LoadUI(html);
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();

            try
            {
                _serverProxy = new ClientGameServerProxy(_eventBus);
                _audio = new AudioSystem(_serverProxy, _eventBus, false);
                _serverProxy.AddSystem(_audio);

                string musicFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds", "IDE", "Music");
                _audio.PlayFolder(musicFolder, shuffle: false, loopPlaylist: true);
                UpdateTitle(_audio.CurrentTitle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MusicPlayerPanel] Failed to start playlist: {ex.Message}");
                UpdateTitle("No music found");
            }
        }

        private void UpdateTitle(string title)
        {
            string display = string.IsNullOrEmpty(title) ? "No track" : title;
            var titleElem = _uiOverlay.FindElementById("music-title");
            if (titleElem != null)
            {
                titleElem.Attributes["data-text"] = display;
                _uiOverlay.RefreshUI();
            }
            Console.WriteLine($"[MusicPlayerPanel] Now playing: {display}");
        }

        public void PauseMusic()
        {
            _audio?.PauseCurrent();
        }

        public void ResumeMusic()
        {
            _audio?.ResumeCurrent();
        }

        public void NextTrack()
        {
            _audio?.Next(true);
            UpdateTitle(_audio?.CurrentTitle);
        }

        public void PreviousTrack()
        {
            _audio?.Previous(true);
            UpdateTitle(_audio?.CurrentTitle);
        }

        public override void Dispose()
        {
            _audio?.StopAll(musicOnly: true);
            base.Dispose();
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new MusicPlayerPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }
    }
}