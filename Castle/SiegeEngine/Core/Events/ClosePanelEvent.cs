// Folder: SiegeEngine.Events
// File: ClosePanelEvent.cs
using SiegeEngine.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    public class ClosePanelEvent : IEvent
    {
        public string Type => "ClosePanelEvent";

        public IPanel Panel { get; }

        public ClosePanelEvent(IPanel panel)
        {
            Panel = panel;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type });
            return Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            // For local events, perhaps not needed; or reconstruct if possible
        }
    }
}