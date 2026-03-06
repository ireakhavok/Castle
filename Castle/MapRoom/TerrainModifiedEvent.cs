// Folder: MapRoom
// File: TerrainModifiedEvent.cs
using SiegeEngine.Core.Events;
using System.Numerics;
using System.Text.Json;

namespace MapRoom
{
    public class TerrainModifiedEvent : IEvent
    {
        public string Type => "TerrainModified";

        public Vector3 WorldPos { get; set; }
        public float Radius { get; set; }
        public float Strength { get; set; }
        public string Operation { get; set; } // "raise" or "lower"
        public ulong PlayerId { get; set; }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new
            {
                Type,
                WorldPos,
                Radius,
                Strength,
                Operation,
                PlayerId
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<TerrainModifiedEvent>(json);
            WorldPos = obj.WorldPos;
            Radius = obj.Radius;
            Strength = obj.Strength;
            Operation = obj.Operation;
            PlayerId = obj.PlayerId;
        }
    }
}