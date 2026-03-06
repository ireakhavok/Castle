// Folder: MapRoom
// File: TerrainModifiedEvent.cs
using SiegeEngine.Core.Events;
using System;
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
        public Guid Id { get; set; }
        public TerrainModifiedEvent()
        {
            Id = Guid.NewGuid();
        }
        public TerrainModifiedEvent(Vector3 worldPos, float radius, float strength, string operation, ulong playerId)
        {
            WorldPos = worldPos;
            Radius = radius;
            Strength = strength;
            Operation = operation;
            PlayerId = playerId;
            Id = Guid.NewGuid();
        }
        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new
            {
                Type,
                WorldPos,
                Radius,
                Strength,
                Operation,
                PlayerId,
                Id = Id.ToString()
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }
        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            WorldPos = JsonSerializer.Deserialize<Vector3>(obj["WorldPos"].ToString());
            Radius = float.Parse(obj["Radius"].ToString());
            Strength = float.Parse(obj["Strength"].ToString());
            Operation = obj["Operation"].ToString();
            PlayerId = ulong.Parse(obj["PlayerId"].ToString());
            Id = Guid.Parse(obj["Id"].ToString());
        }
    }
}