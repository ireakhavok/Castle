using SiegeEngine.Events;
using System.Text;
using System.Text.Json;

public class SwitchSceneEvent : IEvent
{
    public string Type => "SwitchScene";
    public string SceneName { get; private set; }
    public SwitchSceneEvent(string sceneName)
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
        var obj = JsonSerializer.Deserialize<SwitchSceneEvent>(json);
        SceneName = obj.SceneName;
    }
}