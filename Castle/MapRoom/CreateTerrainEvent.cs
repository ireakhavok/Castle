// Folder: MapRoom
// File: CreateTerrainEvent.cs
using SiegeEngine.Core.Events;
using System.Text.Json;

namespace MapRoom
{
    public class TerrainCreationParams
    {
        public string Name { get; set; } = "NewTerrain";
        public string Type { get; set; } = "Flat";
        public float Width { get; set; } = 2048f;        // physical meters
        public float Depth { get; set; } = 2048f;        // physical meters
        public float Resolution { get; set; } = 1.0f;    // grid spacing = meters per cell (from form select)
        public float InitialHeight { get; set; } = 0f;
        public float VerticalExaggeration { get; set; } = 1f;
        public string ImportPath { get; set; } = null;
    }

    public class CreateTerrainEvent : IEvent
    {
        public string Type => "CreateTerrain";
        public TerrainCreationParams Params { get; private set; }

        public CreateTerrainEvent(TerrainCreationParams parameters)
        {
            Params = parameters;
        }

        public byte[] Serialize()
        {
            return JsonSerializer.SerializeToUtf8Bytes(Params);
        }

        public void Deserialize(byte[] data)
        {
        }
    }
}