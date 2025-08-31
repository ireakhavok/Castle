using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System;
using SiegeEngine.Definitions;

namespace SiegeEngine.Events
{
    public class WeatherChangedEvent : IEvent
    {
        public string Type => "WeatherChanged";
        public WeatherState Weather { get; private set; }

        public WeatherChangedEvent(WeatherState weather)
        {
            Weather = weather;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, Weather = Weather.ToString() });
            return Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            Weather = Enum.Parse<WeatherState>(obj["Weather"]);
        }
    }
}