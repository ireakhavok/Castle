// Folder: SiegeEngine.PlayerSystem
// File: PlayerMovement.cs
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using SiegeEngine.Systems;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.AssetParsing.Model;
namespace SiegeEngine.PlayerSystem
{
    public class PlayerMovement
    {
        private readonly float _maxSpeed = 8.0f;
        private readonly float _acceleration = 40.0f;
        private readonly float _deceleration = 50.0f;
        private readonly float _gridWidth = 12500.0f;
        private readonly float _gridHeight = 7500.0f;
        private readonly ClientPredictionSystem _predictionSystem;
        private readonly InputHandler _inputHandler;
        private readonly EventBus _eventBus;
        private Vector2 _movementInput;
        private readonly HashSet<Key> _activeKeys = new HashSet<Key>();
        private Key? _lastPressedKey;
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
        private void OnKeyInput(Key key, InputAction action)
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
                    if (_movementInput.Y > 0 && _activeKeys.Contains(Key.W)) _lastPressedKey = Key.W;
                    else if (_movementInput.Y < 0 && _activeKeys.Contains(Key.S)) _lastPressedKey = Key.S;
                    else if (_movementInput.X < 0 && _activeKeys.Contains(Key.A)) _lastPressedKey = Key.A;
                    else if (_movementInput.X > 0 && _activeKeys.Contains(Key.D)) _lastPressedKey = Key.D;
                    else _lastPressedKey = _activeKeys.FirstOrDefault();
                }
            }
            float x = 0f, y = 0f;
            if (_activeKeys.Contains(Key.W)) y += 1f;
            if (_activeKeys.Contains(Key.S)) y -= 1f;
            if (_activeKeys.Contains(Key.A)) x -= 1f;
            if (_activeKeys.Contains(Key.D)) x += 1f;
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
                    if (_movementInput.Y > 0 && _activeKeys.Contains(Key.W)) _lastPressedKey = Key.W;
                    else if (_movementInput.Y < 0 && _activeKeys.Contains(Key.S)) _lastPressedKey = Key.S;
                    else if (_movementInput.X < 0 && _activeKeys.Contains(Key.A)) _lastPressedKey = Key.A;
                    else if (_movementInput.X > 0 && _activeKeys.Contains(Key.D)) _lastPressedKey = Key.D;
                    else _lastPressedKey = _activeKeys.FirstOrDefault();
                }
            }
            float x = 0f, y = 0f;
            if (_activeKeys.Contains(Key.W)) y += 1f;
            if (_activeKeys.Contains(Key.S)) y -= 1f;
            if (_activeKeys.Contains(Key.A)) x -= 1f;
            if (_activeKeys.Contains(Key.D)) x += 1f;
            _movementInput = new Vector2(x, y);
            Console.WriteLine($"PlayerMovement: Networked Key {e.Key}, Action {e.Action}, ActiveKeys=[{string.Join(",", _activeKeys)}], MovementInput={_movementInput}, LastKey={_lastPressedKey}");
        }
        public virtual void Update(Player player, float deltaTime, Action<int, Vector2, Quaternion> sendMovementRequest, CameraController camera)
        {
            if (player == null || camera == null) return;
            float yawRad = camera.Yaw * (float)(Math.PI / 180);
            Vector3 forward = new Vector3((float)Math.Sin(yawRad), (float)Math.Cos(yawRad), 0);
            forward = Vector3.Normalize(forward);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));

            Vector3 desiredVelocity = Vector3.Zero;
            if (_movementInput != Vector2.Zero)
            {
                Vector2 normalizedMovement = Vector2.Normalize(_movementInput);
                Vector3 moveDirection = forward * normalizedMovement.Y + right * normalizedMovement.X;
                desiredVelocity = moveDirection * _maxSpeed;
            }

            Vector3 currentVel = player.Physics.Velocity;
            if (desiredVelocity.LengthSquared() > 0.001f)
            {
                Vector3 delta = desiredVelocity - currentVel;
                float maxDelta = _acceleration * deltaTime;
                if (delta.LengthSquared() > maxDelta * maxDelta)
                    delta = Vector3.Normalize(delta) * maxDelta;
                currentVel += delta;
            }
            else
            {
                float speed = currentVel.Length();
                if (speed > 0.001f)
                {
                    float newSpeed = Math.Max(0f, speed - _deceleration * deltaTime);
                    currentVel = (newSpeed > 0.001f) ? Vector3.Normalize(currentVel) * newSpeed : Vector3.Zero;
                }
                else
                {
                    currentVel = Vector3.Zero;
                }
            }
            player.Physics.Velocity = currentVel;

            Vector3 newPosition = player.Physics.Position + currentVel * deltaTime;
            newPosition = new Vector3(
                Math.Clamp(newPosition.X, 0, _gridWidth),
                Math.Clamp(newPosition.Y, 0, _gridHeight),
                player.Physics.Position.Z);
            player.Physics.Position = newPosition;

            Quaternion newRotation = player.Physics.Rotation;
            if (currentVel.LengthSquared() > 0.1f)
            {
                float moveYawRad = MathF.Atan2(currentVel.X, currentVel.Y);
                newRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -moveYawRad);
            }
            else if (camera.CurrentPerspective != Perspective.ThirdPerson)
            {
                newRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -yawRad);
            }
            player.Physics.Rotation = newRotation;

            // Only enqueue / log when there is actual movement or active input
            if (currentVel.LengthSquared() > 0.001f || _movementInput != Vector2.Zero)
            {
                Vector2 requestedPos = new Vector2(newPosition.X, newPosition.Y);
                _predictionSystem.EnqueueMovementRequest(player.EntityId, requestedPos, newRotation, player.SteamId);
                sendMovementRequest(player.EntityId, requestedPos, newRotation);
                Console.WriteLine($"PlayerMovement: Requested movement to: X={newPosition.X}, Y={newPosition.Y}, Velocity={currentVel}, Rotation={newRotation}");
            }

            // Entity-relative blend drive: feed the same local input that produced velocity
            if (player.BlendComponent != null && player.BlendComponent.Pack != null)
            {
                Vector2 localInputForBlend = (currentVel.LengthSquared() < 0.01f) ? Vector2.Zero : _movementInput;
                var stack = player.BlendComponent.Pack.CreateBlendStack();
                player.BlendComponent.CurrentBlendParams = stack.MapPlayerInputToBlendCoord(localInputForBlend);
            }
        }
        public static void ResetFrame() { }
    }
}