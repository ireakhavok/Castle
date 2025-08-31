using System;
using System.Collections.Generic;
using System.Numerics;
using Engine.Core;
using Engine.Core.Events;
using Silk.NET.GLFW;
using SiegeEngine.Interfaces;
using SiegeEngine.Definitions;
using SiegeEngine.Systems;
using SiegeEngine.PlayerSystem;

namespace Citadel.Server
{
    public class ServerValidationSystem : GameSystem, ISoundValidator
    {
        private readonly GameServer _server;
        private readonly float _maxSpeed = 20.0f;
        private readonly float _maxDistance = 20.0f;
        private readonly bool _isAuthoritative;
        private readonly Dictionary<int, Quaternion> _lastRotations = new Dictionary<int, Quaternion>();
        private readonly float _maxYawRate = 12000.0f;

        public ServerValidationSystem(GameServer server, bool isAuthoritative = true) : base(server)
        {
            _isAuthoritative = isAuthoritative;
            _server = server;
        }

        public bool IsAuthoritativeMode()
        {
            return _isAuthoritative;
        }

        public override void Update(float deltaTime)
        {
        }

        public bool ValidateMovement(int entityId, Vector2 position, Quaternion rotation, ulong steamId)
        {
            Entity entity = _server.GetEntityById(entityId);
            if (entity == null) return false;

            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics == null) return false;

            Vector2 currentPosition = new Vector2(physics.Position.X, physics.Position.Y);
            float distance = Vector2.Distance(currentPosition, position);

            if (distance > _maxDistance || position.X < 0 || position.X > 128 || position.Y < 0 || position.Y > 72)
            {
                Console.WriteLine($"ServerValidation: Invalid movement for entity {entityId}: Distance={distance}, MaxAllowed={_maxDistance}, Position={position}");
                _lastRotations[entityId] = rotation;
                return false;
            }

            if (!_lastRotations.ContainsKey(entityId))
            {
                _lastRotations[entityId] = physics.Rotation;
            }
            Quaternion lastRotation = _lastRotations[entityId];
            float cosAngle = Math.Abs(Vector4.Dot(ToVector4(lastRotation), ToVector4(rotation)));
            float angleDelta = (float)Math.Acos(Math.Min(cosAngle, 1.0f)) * (180f / (float)Math.PI);
            float yawRate = angleDelta / 0.016f;

            Console.WriteLine($"ServerValidation: Yaw rate for entity {entityId}: AngleDelta={angleDelta}°, YawRate={yawRate} deg/s, MaxAllowed={_maxYawRate}, LastRotation={lastRotation}, NewRotation={rotation}");

            if (yawRate > _maxYawRate)
            {
                Console.WriteLine($"ServerValidation: Invalid yaw rate for entity {entityId}: {yawRate} deg/s, MaxAllowed={_maxYawRate}");
                _lastRotations[entityId] = rotation;
                return false;
            }

            if (_isAuthoritative)
            {
                physics.Position = new Vector3(position.X, position.Y, physics.Position.Z);
                physics.Rotation = rotation;
                entity.GetComponent<Player>().Position = physics.Position;
                _lastRotations[entityId] = rotation;
                Console.WriteLine($"ServerValidation: Moved entity {entityId} to: X={physics.Position.X}, Y={physics.Position.Y}, Z={physics.Position.Z}, Rotation={physics.Rotation}");
                _server.Publish(new EntityMovedEvent(entityId, position, physics.Rotation));
            }
            else
            {
                Console.WriteLine($"P2PValidation: Movement for entity {entityId} pending peer approval");
                _lastRotations[entityId] = rotation;
            }
            return true;
        }

        public bool ValidateCombat(int entityId, int targetId, float damage)
        {
            Entity entity = _server.GetEntityById(entityId);
            Entity target = _server.GetEntityById(targetId);
            if (entity == null || target == null) return false;

            var physics = target.GetComponent<PhysicsComponent>();
            if (physics == null) return false;

            float distance = Vector2.Distance(
                new Vector2(entity.GetComponent<PhysicsComponent>().Position.X, entity.GetComponent<PhysicsComponent>().Position.Y),
                new Vector2(physics.Position.X, physics.Position.Y)
            );
            if (distance <= 5.0f && damage >= 0 && damage <= 50.0f)
            {
                if (_isAuthoritative)
                {
                    physics.Health -= damage;
                    Console.WriteLine($"ServerValidation: Entity {entityId} dealt {damage} damage to {targetId}, Health={physics.Health}");
                }
                return true;
            }
            return false;
        }

        public bool ValidateInventory(int entityId, string action, object data)
        {
            Entity entity = _server.GetEntityById(entityId);
            if (entity == null) return false;

            if (action == "AddItem" && data is string itemId)
            {
                if (_isAuthoritative)
                {
                    Console.WriteLine($"ServerValidation: Entity {entityId} added item {itemId}");
                }
                return true;
            }
            return false;
        }

        public bool ValidateMouseInput(ulong steamId, Vector2 position, MouseButton button, InputAction action)
        {
            if (position.X < 0 || position.X > 3840 || position.Y < 0 || position.Y > 1080)
            {
                Console.WriteLine($"ServerValidation: Invalid mouse position for SteamID {steamId}: Pos={position}");
                return false;
            }
            Console.WriteLine($"ServerValidation: Valid mouse input for SteamID {steamId}: Pos={position}, Button={button}, Action={action}");
            return true;
        }

        public bool ValidateKeyInput(ulong steamId, Keys key, InputAction action)
        {
            if ((int)key < 0 || (int)key > 512)
            {
                Console.WriteLine($"ServerValidation: Invalid key for SteamID {steamId}: Key={key}");
                return false;
            }
            Console.WriteLine($"ServerValidation: Valid key input for SteamID {steamId}: Key={key}, Action={action}");
            return true;
        }

        private static Vector4 ToVector4(Quaternion q) => new Vector4(q.X, q.Y, q.Z, q.W);

        public bool ValidateSoundSource(SoundSource source)
        {
            if (!source.IsSensitive) return true;

            var entity = _server.GetEntityById(source.EntityId);
            if (entity == null) return false;

            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics == null) return false;

            float distance = Vector3.Distance(physics.Position, source.Position);
            if (distance > _maxDistance)
            {
                return false;
            }

            var player = entity.GetComponent<Player>();
            if (player != null && player.SteamId != source.SteamId)
            {
                return false;
            }

            return true;
        }
    }
}