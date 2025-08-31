using System.Numerics;

namespace SiegeEngine.Networking
{
    public struct MovementRequest
    {
        public Vector2 Position;
        public Quaternion Rotation;
        public ulong SteamId;
        public long Timestamp; // Using ticks for time

        public MovementRequest(Vector2 position, Quaternion rotation, ulong steamId, long timestamp)
        {
            Position = position;
            Rotation = rotation;
            SteamId = steamId;
            Timestamp = timestamp;
        }
    }
}