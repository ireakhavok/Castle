// Folder: SiegeEngine.Core.Events
// File: SceneActivatedEvent.cs
using System.Text;
using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    public class SceneActivatedEvent : IEvent
    {
        public string Type => "SceneActivated";
        public string SceneName { get; private set; }

        public SceneActivatedEvent(string sceneName)
        {
            SceneName = sceneName;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, SceneName });
            return Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<SceneActivatedEvent>(json);
            SceneName = obj.SceneName;
        }
    }
}