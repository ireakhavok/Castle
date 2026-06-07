// Folder: SiegeEngine/Core/Events
// File: SceneStateChangedEvent.cs
using SiegeEngine.Core.Events;
using System;
using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    /// <summary>
    /// New event for live state changes (painter → preview sync, network collab).
    /// Fully contained in Core - no dependencies on MapRoom.
    /// </summary>
    public class SceneStateChangedEvent : IEvent
    {
        public string Type => "SceneStateChanged";
        public string SceneName { get; set; }
        public string ChangeType { get; set; } // "Height", "Color", etc.
        public Guid Id { get; set; } = Guid.NewGuid();

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(this);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<SceneStateChangedEvent>(json);
            SceneName = obj.SceneName;
            ChangeType = obj.ChangeType;
            Id = obj.Id;
        }
    }
}