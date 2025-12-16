using System;
using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    public class PlayerExitedEditorEvent : IEvent
    {
        public string Type => "PlayerExitedEditor";
        public ulong PlayerId { get; set; }

        public PlayerExitedEditorEvent(ulong playerId)
        {
            PlayerId = playerId;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, PlayerId });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<PlayerExitedEditorEvent>(json);
            PlayerId = obj.PlayerId;
        }
    }
}