using SiegeEngine.Core.Events;
using System.Text;
using System.Text.Json;

public class FileSelectedEvent : IEvent
{
    public string Type => "FileSelected";
    public string Path { get; private set; }
    public object UserData { get; set; }
    public FileSelectedEvent(string path)
    {
        Path = path;
    }
    public byte[] Serialize()
    {
        var json = JsonSerializer.Serialize(new { Type, Path });
        return Encoding.UTF8.GetBytes(json);
    }
    public void Deserialize(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        var obj = JsonSerializer.Deserialize<FileSelectedEvent>(json);
        Path = obj.Path;
    }
}