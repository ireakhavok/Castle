using System.Numerics;
using System.Text.Json;
using System.Text;

namespace SiegeEngine.Core.Events
{
    public class TimeOfDayChangedEvent : IEvent
    {
        public string Type => "TimeOfDayChanged";
        public float TimeOfDay { get; private set; }
        public Vector3 AmbientColor { get; private set; }

        public TimeOfDayChangedEvent(float timeOfDay, Vector3 ambientColor)
        {
            TimeOfDay = timeOfDay;
            AmbientColor = ambientColor;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, TimeOfDay, AmbientColor });
            return Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<TimeOfDayChangedEvent>(json);
            TimeOfDay = obj.TimeOfDay;
            AmbientColor = obj.AmbientColor;
        }
    }
}