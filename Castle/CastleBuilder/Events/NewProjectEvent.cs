// Folder: CastleBuilder/Events
// File: NewProjectEvent.cs
using System.Text.Json;
using System.Text;
namespace CastleBuilder.Events
{
    public class NewProjectEvent : SiegeEngine.Events.IEvent
    {
        public string Type => "NewProject";
        public string Name { get; set; }
        public string ProjectType { get; set; }
        public string Mode { get; set; }
        public bool AllowMods { get; set; }
        public string Path { get; set; }
        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(this);
            return Encoding.UTF8.GetBytes(json);
        }
        public void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<NewProjectEvent>(json);
            Name = obj.Name;
            ProjectType = obj.ProjectType;
            Mode = obj.Mode;
            AllowMods = obj.AllowMods;
            Path = obj.Path;
        }
    }
}