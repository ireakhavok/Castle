using System;
using System.Numerics;
using System.Text.Json;

namespace SiegeEngine.Core.Events
{
    public class PhysicsCollisionEvent : IEvent
    {
        public string Type => "PhysicsCollision";
        public int SourceId { get; set; }
        public int TargetId { get; set; }
        public Vector3 Force { get; set; }

        public PhysicsCollisionEvent(int sourceId, int targetId, Vector3 force)
        {
            SourceId = sourceId;
            TargetId = targetId;
            Force = force;
        }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(new { Type, SourceId, TargetId, Force });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<PhysicsCollisionEvent>(json);
            SourceId = obj.SourceId;
            TargetId = obj.TargetId;
            Force = obj.Force;
        }
    }
}