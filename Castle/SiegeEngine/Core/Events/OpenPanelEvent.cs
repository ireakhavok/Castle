// Folder: SiegeEngine.Events
// File: OpenPanelEvent.cs
using SiegeEngine.Core.Interfaces;
namespace SiegeEngine.Core.Events
{
    public enum OpenMode
    {
        Replace,
        Overlay
    }
    public class OpenPanelEvent : IEvent
    {
        public string Type => "OpenPanel";
        public IPanel Panel { get; private set; }
        public OpenMode Mode { get; set; } = OpenMode.Replace;
        public OpenPanelEvent(IPanel panel)
        {
            Panel = panel;
        }
        public byte[] Serialize()
        {
            // Serialization not needed for local event
            return null;
        }
        public void Deserialize(byte[] data)
        {
            // Deserialization not needed for local event
        }
    }
}