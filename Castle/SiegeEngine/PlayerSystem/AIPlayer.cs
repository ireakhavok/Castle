// SiegeEngine.PlayerSystem/AIPlayer.cs
using System;
using System.Numerics;
using SiegeEngine.Definitions;

namespace SiegeEngine.PlayerSystem
{
    public class AIPlayer : Player
    {
        private readonly Random _random = new();
        private float _moveTimer;
        private const float MoveInterval = 0.5f; // Move every 0.5s
        public AIPlayer(int entityId, Vector3 position) : base(entityId, position)
        {
            // Initialize with random yaw for testing
            if (Camera != null)
            {
                Camera.SetYaw(_random.Next(0, 360)); // Random facing direction
            }
        }
        public void UpdateAI(float deltaTime, Action<int, Vector2> sendMovementRequest)
        {
            _moveTimer += deltaTime;
            if (_moveTimer >= MoveInterval)
            {
                // Random movement within grid
                Vector2 moveDir = new Vector2(
                    (float)_random.NextDouble() * 2 - 1, // -1 to 1
                    (float)_random.NextDouble() * 2 - 1
                );
                Vector2 newPos = new Vector2(Position.X, Position.Y) + moveDir * 20f * MoveInterval;
                newPos.X = Math.Clamp(newPos.X, 0, 128);
                newPos.Y = Math.Clamp(newPos.Y, 0, 72);
                sendMovementRequest(EntityId, newPos);
                Position = new Vector3(newPos.X, newPos.Y, Position.Z);
                Physics.Position = Position;
                // Random yaw update
                if (Camera != null)
                {
                    Camera.SetYaw(Camera.Yaw + _random.Next(-10, 11)); // Small yaw change
                }
                _moveTimer = 0;
                Console.WriteLine($"AIPlayer {EntityId} moved to {newPos}");
            }
        }
        // Helper to set yaw directly for testing
        public void SetYaw(float yaw)
        {
            if (Camera != null)
            {
                Camera.SetYaw(yaw);
            }
        }
    }
    // Extension to allow yaw setting (minimal impact)
    public static class CameraControllerExtensions
    {
        public static void SetYaw(this CameraController camera, float yaw)
        {
            camera.SetYawInternal(yaw);
        }
        internal static void SetYawInternal(this CameraController camera, float yaw)
        {
            typeof(CameraController).GetField("_yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(camera, yaw);
        }
    }
}