using SiegeEngine.Core.Events;
using System.Text;
using System.Text.Json;

namespace ToolChest
{
    public class ConsoleLogEvent : IEvent
    {
        public string Type => "ConsoleLog";
        public string Message { get; set; }

        public ConsoleLogEvent() { }
        public ConsoleLogEvent(string message) { Message = message; }

        public byte[] Serialize() => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this));
        public void Deserialize(byte[] data)
        {
            var obj = JsonSerializer.Deserialize<ConsoleLogEvent>(Encoding.UTF8.GetString(data));
            Message = obj.Message;
        }
    }
}