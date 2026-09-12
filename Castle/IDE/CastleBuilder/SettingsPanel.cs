using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.UI;
using System.Collections.Generic;
using System.Text;

namespace CastleBuilder
{
    public sealed class SettingsPanel : BasePanel
    {
        public SettingsPanel(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
            : base(renderContext, controlContext, window, eventBus)
        {
            HasTitleBar = true;
            IsClosable = true;
            IsModal = true;
            AllowDragging = true;
            Size = new System.Numerics.Vector2(420f, 360f);
        }

        protected override UIOverlay CreateUIOverlay() => new SettingsOverlay(this, _renderContext, _controlContext, _window, _eventBus);

        public override void Init()
        {
            base.Init();
            var settings = new UISettingsManager();
            settings.LoadSettings();
            string html = BuildHtml(settings);
            _uiOverlay.LoadUI(html, "");
            _uiOverlay.PanelWidth = Size.X;
            _uiOverlay.PanelHeight = Size.Y;
            _uiOverlay.RefreshUI();
        }

        static string BuildHtml(UISettingsManager settings)
        {
            var sb = new StringBuilder();
            sb.Append("<html><body style=\"background:#1e1e1e;color:#e0e0e0;font-family:Segoe UI,sans-serif;padding:16px;\">");
            sb.Append("<h3 style=\"color:#7CFFCB;margin:0 0 12px 0;\">Renderer</h3>");
            sb.Append("<p style=\"margin:0 0 12px 0;\">Current: <b>");
            sb.Append(System.Net.WebUtility.HtmlEncode(settings.CurrentRenderer));
            sb.Append("</b></p>");
            sb.Append("<p style=\"margin:0 0 12px 0;font-size:12px;color:#aaa;\">Changing renderer saves and restarts the IDE.</p>");
            foreach (string name in DistinctRenderers(settings.AvailableRenderers))
            {
                string hook = name switch
                {
                    "DirectX11" => "CastleBuilder.MenuCommands.SetRendererDirectX11",
                    "DirectX12" => "CastleBuilder.MenuCommands.SetRendererDirectX12",
                    "Vulkan" => "CastleBuilder.MenuCommands.SetRendererVulkan",
                    _ => "CastleBuilder.MenuCommands.SetRendererOpenGL"
                };
                bool current = string.Equals(name, settings.CurrentRenderer, System.StringComparison.OrdinalIgnoreCase);
                sb.Append("<div data-hook=\"");
                sb.Append(hook);
                sb.Append("\" style=\"padding:8px 12px;margin:6px 0;background:");
                sb.Append(current ? "#2a4a3a" : "#2a2a2a");
                sb.Append(";border:1px solid #7CFFCB;cursor:pointer;\">");
                sb.Append(System.Net.WebUtility.HtmlEncode(name));
                if (current) sb.Append(" (current)");
                sb.Append("</div>");
            }
            sb.Append("</body></html>");
            return sb.ToString();
        }

        static IEnumerable<string> DistinctRenderers(List<string> detected)
        {
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string n in new[] { "OpenGL", "DirectX11", "DirectX12", "Vulkan" })
            {
                if (seen.Add(n)) yield return n;
            }
            if (detected == null) yield break;
            foreach (string n in detected)
            {
                if (!string.IsNullOrWhiteSpace(n) && seen.Add(n))
                    yield return n;
            }
        }

        public static void Open(IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus)
        {
            var panel = new SettingsPanel(renderContext, controlContext, window, eventBus);
            eventBus.Publish(new OpenPanelEvent(panel) { Mode = OpenMode.Overlay });
        }

        sealed class SettingsOverlay : UIOverlay
        {
            public SettingsOverlay(SettingsPanel parent, IRenderContext rc, IControlContext cc, nint window, EventBus bus)
                : base(rc, cc, window, bus) { }
        }
    }
}
