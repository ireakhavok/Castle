// Folder: SiegeEngine/PlayerSystem
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
        }
        private void OnNetworkKeyInput(KeyInputEvent e)
        {
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
        }
        public virtual void Update(Player player, float deltaTime, Action<int, Vector2, Quaternion> sendMovementRequest, CameraController camera)
        {
            if (player == null || camera == null) return;
            float yawRad = camera.Yaw * (float)(Math.PI / 180);
            Vector3 forward = new Vector3((float)Math.Sin(yawRad), (float)Math.Cos(yawRad), 0);
            forward = Vector3.Normalize(forward);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
            // Horizontal ownership only — Physics owns Position and Velocity.Z
            Vector2 desiredVelocityXY = Vector2.Zero;
            if (_movementInput != Vector2.Zero)
            {
                Vector2 normalizedMovement = Vector2.Normalize(_movementInput);
                Vector3 moveDirection = forward * normalizedMovement.Y + right * normalizedMovement.X;
                desiredVelocityXY = new Vector2(moveDirection.X, moveDirection.Y) * _maxSpeed;
            }
            Vector3 currentVel = player.Physics.Velocity;
            Vector2 currentVelXY = new Vector2(currentVel.X, currentVel.Y);
            if (desiredVelocityXY.LengthSquared() > 0.001f)
            {
                Vector2 delta = desiredVelocityXY - currentVelXY;
                float maxDelta = _acceleration * deltaTime;
                if (delta.LengthSquared() > maxDelta * maxDelta)
                    delta = Vector2.Normalize(delta) * maxDelta;
                currentVelXY += delta;
            }
            else
            {
                float speed = currentVelXY.Length();
                if (speed > 0.001f)
                {
                    float newSpeed = Math.Max(0f, speed - _deceleration * deltaTime);
                    currentVelXY = (newSpeed > 0.001f) ? Vector2.Normalize(currentVelXY) * newSpeed : Vector2.Zero;
                }
                else
                {
                    currentVelXY = Vector2.Zero;
                }
            }
            // Preserve Velocity.Z exactly — Physics owns vertical motion and all Position
            player.Physics.Velocity = new Vector3(currentVelXY.X, currentVelXY.Y, currentVel.Z);
            // Rotation rules by perspective (facing only – no Position write)
            Quaternion newRotation = player.Physics.Rotation;
            if (camera.CurrentPerspective == Perspective.ThirdPerson)
            {
                if (currentVelXY.LengthSquared() > 0.1f)
                {
                    float moveYawRad = MathF.Atan2(currentVelXY.X, currentVelXY.Y);
                    newRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -moveYawRad);
                }
            }
            else
            {
                newRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -yawRad);
            }
            player.Physics.Rotation = newRotation;
            // Only enqueue / log when there is actual movement or active input
            if (currentVelXY.LengthSquared() > 0.001f || _movementInput != Vector2.Zero)
            {
                Vector2 requestedPos = new Vector2(player.Physics.Position.X, player.Physics.Position.Y);
                _predictionSystem.EnqueueMovementRequest(player.EntityId, requestedPos, newRotation, player.SteamId);
                sendMovementRequest(player.EntityId, requestedPos, newRotation);
            }
            // Blend drive
            if (player.BlendComponent != null && player.BlendComponent.Pack != null)
            {
                Vector2 localInputForBlend;
                if (currentVelXY.LengthSquared() < 0.01f)
                {
                    localInputForBlend = Vector2.Zero;
                }
                else if (camera.CurrentPerspective == Perspective.ThirdPerson)
                {
                    localInputForBlend = new Vector2(0f, 1f);
                }
                else
                {
                    localInputForBlend = _movementInput;
                }
                var stack = player.BlendComponent.Pack.CreateBlendStack();
                player.BlendComponent.CurrentBlendParams = stack.MapPlayerInputToBlendCoord(localInputForBlend);
            }
        }
        public static void ResetFrame() { }
    }
}