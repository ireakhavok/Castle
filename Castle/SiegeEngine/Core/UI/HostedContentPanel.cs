// Folder: SiegeEngine/Core/UI
// File: HostedContentPanel.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.Interfaces;
using System;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace SiegeEngine.Core.UI
{
    public class HostedContentPanel : BasePanel, IDataAwarePanel
    {
        private readonly IHostedContent _content;
        private readonly string _key;
        private readonly string _htmlPath;
        private readonly string _htmlContent;

        public override bool WantsContinuousUpdate => true;
        public string HudKey => _key;
        public HudAnchor Anchor { get; }
        public float RequestedX { get; }
        public float RequestedY { get; }
        public string DataKey => !string.IsNullOrEmpty(_content?.DataKey) ? _content.DataKey : _key;

        public HostedContentPanel(
            IRenderContext renderContext,
            IControlContext controlContext,
            nint window,
            EventBus eventBus,
            OpenGameHudEvent request)
            : base(renderContext, controlContext, window, eventBus)
        {
            _content = request.Content;
            _key = request.Key ?? request.HtmlRelativePath ?? "hosted";
            _htmlContent = request.HtmlContent;
            _htmlPath = string.IsNullOrEmpty(_htmlContent) ? request.HtmlRelativePath : null;
            Anchor = request.Anchor;
            RequestedX = request.PosX;
            RequestedY = request.PosY;
            ChromeStyle = request.Chrome;
            DockingMode = request.Docking;
            HasTitleBar = request.AllowMove;
            IsClosable = request.Chrome != PanelChromeStyle.Bare;
            AllowDragging = request.AllowMove;
            BaseWidth = request.Width > 0 ? request.Width : 360f;
            BaseHeight = request.Height > 0 ? request.Height : 280f;
        }

        public override void Init()
        {
            base.Init();
            string html = _htmlContent;
            string baseDir = "";
            if (string.IsNullOrEmpty(html) && !string.IsNullOrEmpty(_htmlPath) && File.Exists(_htmlPath))
            {
                html = File.ReadAllText(_htmlPath);
                baseDir = Path.GetDirectoryName(_htmlPath) ?? "";
            }
            if (!string.IsNullOrEmpty(html) && _uiOverlay != null)
            {
                _uiOverlay.LoadUI(html, baseDir);
                _uiOverlay.RefreshUI();
            }
            try { _content?.Init(); }
            catch (Exception ex)
            {
                Console.WriteLine("[HostedContentPanel] Init failed: " + ex.Message);
            }
        }

        public override void Update(float deltaTime, Vector2 absMousePos, bool mouseDown, bool mousePressed, bool mouseReleased, float scrollDelta = 0f)
        {
            base.Update(deltaTime, absMousePos, mouseDown, mousePressed, mouseReleased, scrollDelta);
            try { _content?.Update(deltaTime); }
            catch (Exception ex)
            {
                Console.WriteLine("[HostedContentPanel] Update failed: " + ex.Message);
            }
        }

        protected override void RenderInnerContent()
        {
            try { _content?.Render(); }
            catch (Exception ex)
            {
                Console.WriteLine("[HostedContentPanel] Render failed: " + ex.Message);
            }
        }

        public JsonElement SavePanelState() => default;
        public void LoadPanelState(JsonElement state) { }

        public override void Dispose()
        {
            try { _content?.Dispose(); } catch { }
            base.Dispose();
        }
    }
}
