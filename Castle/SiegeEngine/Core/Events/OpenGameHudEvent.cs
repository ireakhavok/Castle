// Folder: SiegeEngine/Core/Events
// File: OpenGameHudEvent.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Interfaces;

namespace SiegeEngine.Core.Events
{
    public class OpenGameHudEvent : IEvent
    {
        public string Type => "OpenGameHud";
        public string HtmlRelativePath { get; set; }
        public string Title { get; set; } = "HUD";
        public PanelChromeStyle Chrome { get; set; } = PanelChromeStyle.Game;
        public DockingMode Docking { get; set; } = DockingMode.Dynamic;
        public bool Open { get; set; } = true;
        public float Width { get; set; } = 360f;
        public float Height { get; set; } = 280f;

        public byte[] Serialize() => null;
        public void Deserialize(byte[] data) { }
    }
}
