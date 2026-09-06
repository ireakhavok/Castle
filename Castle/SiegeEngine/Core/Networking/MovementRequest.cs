using System.Numerics;

namespace SiegeEngine.Core.Networking
{
    public struct MovementRequest
    {
        public Vector2 Position;
        public Quaternion Rotation;
        public ulong SteamId;
        public long Timestamp;
        public uint Tick;

        public MovementRequest(Vector2 position, Quaternion rotation, ulong steamId, long timestamp, uint tick = 0)
        {
            Position = position;
            Rotation = rotation;
            SteamId = steamId;
            Timestamp = timestamp;
            Tick = tick;
        }
    }
}
