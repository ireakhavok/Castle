// Folder: SiegeEngine/Core/Events
// File: OpenHostedContentEvent.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Interfaces;

namespace SiegeEngine.Core.Events
{
    public class OpenHostedContentEvent : IEvent
    {
        public string Type => "OpenHostedContent";
        public string Key { get; set; }
        public string Title { get; set; } = "HUD";
        public IHostedContent Content { get; set; }
        public string HtmlRelativePath { get; set; }
        public string HtmlContent { get; set; }
        public PanelChromeStyle Chrome { get; set; } = PanelChromeStyle.Game;
        public DockingMode Docking { get; set; } = DockingMode.Desktop;
        public bool Open { get; set; } = true;
        public float Width { get; set; } = 360f;
        public float Height { get; set; } = 280f;
        public HudAnchor Anchor { get; set; } = HudAnchor.None;
        public float PosX { get; set; } = float.NaN;
        public float PosY { get; set; } = float.NaN;
        public bool AllowMove { get; set; } = true;

        public byte[] Serialize() => null;
        public void Deserialize(byte[] data) { }
    }
}
