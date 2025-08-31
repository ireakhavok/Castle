using System;
using System.Numerics;
using Silk.NET.GLFW;
using Engine.Core.Definitions;
using System.Collections.Generic;
using System.Linq;
using SiegeEngine.Events;
using SiegeEngine.Systems;

namespace SiegeEngine.PlayerSystem
{
    public class PlayerMovement
    {
        private readonly float _speed = 20.0f;
        private readonly float _gridWidth = 128.0f;
        private readonly float _gridHeight = 72.0f;
        private readonly ClientPredictionSystem _predictionSystem;
        private readonly InputHandler _inputHandler;
        private readonly EventBus _eventBus;
        private Vector2 _movementInput;
        private readonly HashSet<Keys> _activeKeys = new HashSet<Keys>();
        private Keys? _lastPressedKey;
        private readonly string _callbackId = $"PlayerMovement_{Guid.NewGuid()}";

        public PlayerMovement(InputHandler inputHandler, ClientPredictionSystem predictionSystem, EventBus eventBus = null)
        {
            _inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
            _predictionSystem = predictionSystem ?? throw new ArgumentNullException(nameof(predictionSystem));
            _eventBus = eventBus;
            _movementInput = Vector2.Zero;
            _inputHandler.SetKeyCallback(_callbackId, OnKeyInput);
            if (_eventBus != null)
            {
                _eventBus.Subscribe<KeyInputEvent>(OnNetworkKeyInput);
            }
        }

        private void OnKeyInput(Keys key, InputAction action)
        {
            if (action == InputAction.Press || action == InputAction.Repeat)
            {
                _activeKeys.Add(key);
                _lastPressedKey = key;
            }
            else if (action == InputAction.Release)
            {
                _activeKeys.Remove(key);
                if (_activeKeys.Count == 0)
                {
                    _lastPressedKey = null;
                }
                else
                {
                    if (_movementInput.Y > 0 && _activeKeys.Contains(Keys.W))
                        _lastPressedKey = Keys.W;
                    else if (_movementInput.Y < 0 && _activeKeys.Contains(Keys.S))
                        _lastPressedKey = Keys.S;
                    else if (_movementInput.X < 0 && _activeKeys.Contains(Keys.A))
                        _lastPressedKey = Keys.A;
                    else if (_movementInput.X > 0 && _activeKeys.Contains(Keys.D))
                        _lastPressedKey = Keys.D;
                    else
                        _lastPressedKey = _activeKeys.FirstOrDefault();
                }
            }

            float x = 0f, y = 0f;
            if (_activeKeys.Contains(Keys.W)) y += 1f;
            if (_activeKeys.Contains(Keys.S)) y -= 1f;
            if (_activeKeys.Contains(Keys.A)) x -= 1f;
            if (_activeKeys.Contains(Keys.D)) x += 1f;
            _movementInput = new Vector2(x, y);

            Console.WriteLine($"PlayerMovement: Local Key {key}, Action {action}, ActiveKeys=[{string.Join(",", _activeKeys)}], MovementInput={_movementInput}, LastKey={_lastPressedKey}");
        }

        private void OnNetworkKeyInput(KeyInputEvent e)
        {
            Console.WriteLine($"PlayerMovement: Networked Key {e.Key}, Action {e.Action}, SteamID={e.SteamId}");
            if (e.Action == InputAction.Press || e.Action == InputAction.Repeat)
            {
                _activeKeys.Add(e.Key);
                _lastPressedKey = e.Key;
            }
            else if (e.Action == InputAction.Release)
            {
                _activeKeys.Remove(e.Key);
                if (_activeKeys.Count == 0)
                {
                    _lastPressedKey = null;
                }
                else
                {
                    if (_movementInput.Y > 0 && _activeKeys.Contains(Keys.W))
                        _lastPressedKey = Keys.W;
                    else if (_movementInput.Y < 0 && _activeKeys.Contains(Keys.S))
                        _lastPressedKey = Keys.S;
                    else if (_movementInput.X < 0 && _activeKeys.Contains(Keys.A))
                        _lastPressedKey = Keys.A;
                    else if (_movementInput.X > 0 && _activeKeys.Contains(Keys.D))
                        _lastPressedKey = Keys.D;
                    else
                        _lastPressedKey = _activeKeys.FirstOrDefault();
                }
            }

            float x = 0f, y = 0f;
            if (_activeKeys.Contains(Keys.W)) y += 1f;
            if (_activeKeys.Contains(Keys.S)) y -= 1f;
            if (_activeKeys.Contains(Keys.A)) x -= 1f;
            if (_activeKeys.Contains(Keys.D)) x += 1f;
            _movementInput = new Vector2(x, y);

            Console.WriteLine($"PlayerMovement: Networked Key {e.Key}, Action {e.Action}, ActiveKeys=[{string.Join(",", _activeKeys)}], MovementInput={_movementInput}, LastKey={_lastPressedKey}");
        }

        public unsafe void Update(Player player, float deltaTime, Action<int, Vector2, Quaternion> sendMovementRequest, WindowHandle* window, CameraController camera)
        {
            if (player == null || camera == null) return;

            float yawRad = camera.Yaw * (float)(Math.PI / 180);
            Vector3 forward = new Vector3((float)Math.Sin(yawRad), (float)Math.Cos(yawRad), 0);
            forward = Vector3.Normalize(forward);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));

            if (_movementInput != Vector2.Zero)
            {
                Vector2 normalizedMovement = Vector2.Normalize(_movementInput);
                Vector3 moveDirection = (forward * normalizedMovement.Y + right * normalizedMovement.X) * _speed * deltaTime;
                Vector3 newPosition = player.Position + moveDirection;

                newPosition = new Vector3(
                    Math.Clamp(newPosition.X, 0, _gridWidth),
                    Math.Clamp(newPosition.Y, 0, _gridHeight),
                    player.Position.Z
                );

                player.Position = newPosition;
                player.Physics.Position = newPosition;

                Quaternion newRotation = player.Physics.Rotation;
                float effectiveYawRad = yawRad;
                float effectiveYawDeg = camera.Yaw;

                bool isDiagonal = _movementInput.X != 0 && _movementInput.Y != 0;
                bool isSingleAxis = _movementInput.X == 0 || _movementInput.Y == 0;

                if (isDiagonal)
                {
                    float moveAngleRad = (float)Math.Atan2(normalizedMovement.X, normalizedMovement.Y);
                    effectiveYawRad = yawRad + moveAngleRad;
                    effectiveYawDeg = camera.Yaw + moveAngleRad * (180f / (float)Math.PI);
                    newRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -effectiveYawRad);
                    Console.WriteLine($"PlayerMovement: Diagonal rotation set: CameraYaw={camera.Yaw}°, MoveAngle={moveAngleRad * (180f / (float)Math.PI)}°, EffectiveYaw={effectiveYawDeg}°, Quaternion={newRotation}, MoveDirection={moveDirection}, Input={_movementInput}");
                }
                else if (isSingleAxis && _lastPressedKey.HasValue)
                {
                    float rotationOffset = _lastPressedKey switch
                    {
                        Keys.W => 0f,
                        Keys.A => -(float)(Math.PI / 2),
                        Keys.S => (float)Math.PI,
                        Keys.D => (float)(Math.PI / 2),
                        _ => 0f
                    };
                    effectiveYawRad = yawRad + rotationOffset;
                    effectiveYawDeg = camera.Yaw + rotationOffset * (180f / (float)Math.PI);
                    newRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -effectiveYawRad);
                    Console.WriteLine($"PlayerMovement: Third-person {_lastPressedKey} rotation set: CameraYaw={camera.Yaw}°, Offset={rotationOffset * (180f / (float)Math.PI)}°, EffectiveYaw={effectiveYawDeg}°, Quaternion={newRotation}, MoveDirection={moveDirection}");
                }
                else if (camera.CurrentPerspective != Perspective.ThirdPerson)
                {
                    newRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -yawRad);
                    effectiveYawDeg = camera.Yaw;
                    Console.WriteLine($"PlayerMovement: {camera.CurrentPerspective} rotation set: Yaw={camera.Yaw}°, Quaternion={newRotation}");
                }

                player.Physics.Rotation = newRotation;

                Vector2 requestedPos = new Vector2(newPosition.X, newPosition.Y);
                _predictionSystem.EnqueueMovementRequest(player.EntityId, requestedPos, newRotation, player.SteamId);
                sendMovementRequest(player.EntityId, requestedPos, newRotation);
                Console.WriteLine($"PlayerMovement: Requested movement to: X={newPosition.X}, Y={newPosition.Y}, MoveDir={moveDirection}, Rotation={newRotation}");
            }
            else if (camera.CurrentPerspective != Perspective.ThirdPerson)
            {
                Quaternion newRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -yawRad);
                player.Physics.Rotation = newRotation;
                Console.WriteLine($"PlayerMovement: {camera.CurrentPerspective} rotation updated without movement: Yaw={camera.Yaw}°, Quaternion={newRotation}");
            }
        }

        public static void ResetFrame() { }
    }
}