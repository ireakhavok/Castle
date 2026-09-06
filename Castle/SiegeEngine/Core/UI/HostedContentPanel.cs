// Folder: SiegeEngine/Core/UI
// File: HostedContentPanel.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.Interfaces;
using System;
using System.Numerics;
using System.Text.Json;

namespace SiegeEngine.Core.UI
{
    public class HostedContentPanel : BasePanel, IDataAwarePanel
    {
        private readonly IHostedContent _content;
        private readonly string _key;

        public override bool WantsContinuousUpdate => true;

        public string DataKey => !string.IsNullOrEmpty(_content?.DataKey) ? _content.DataKey : _key;

        public HostedContentPanel(
            IRenderContext renderContext,
            IControlContext controlContext,
            nint window,
            EventBus eventBus,
            OpenHostedContentEvent request)
            : base(renderContext, controlContext, window, eventBus)
        {
            _content = request.Content;
            _key = request.Key ?? "hosted";
            ChromeStyle = request.Chrome;
            DockingMode = request.Docking;
            HasTitleBar = request.Chrome != PanelChromeStyle.Bare;
            IsClosable = request.Chrome != PanelChromeStyle.Bare;
            AllowDragging = request.Chrome != PanelChromeStyle.Bare;
            BaseWidth = request.Width > 0 ? request.Width : 360f;
            BaseHeight = request.Height > 0 ? request.Height : 280f;
        }

        public override void Init()
        {
            base.Init();
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

        public JsonElement SavePanelState()
        {
            return default;
        }

        public void LoadPanelState(JsonElement state)
        {
        }

        public override void Dispose()
        {
            try { _content?.Dispose(); }
            catch { }
            base.Dispose();
        }
    }
}
