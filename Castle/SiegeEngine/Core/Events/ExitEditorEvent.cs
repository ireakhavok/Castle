using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    public class ExitEditorEvent : IEvent
    {
        public string Type => "ExitEditor";
        public ulong PlayerId { get; set; }

        public ExitEditorEvent(ulong playerId) => PlayerId = playerId;

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, PlayerId });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<ExitEditorEvent>(json);
            PlayerId = obj.PlayerId;
        }
    }
}