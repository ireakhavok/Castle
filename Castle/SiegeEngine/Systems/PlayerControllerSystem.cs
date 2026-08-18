// Folder: SiegeEngine/Systems
// File: PlayerControllerSystem.cs
using System;
using System.Numerics;
using SiegeEngine.Core.Events;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Systems
{
    public class PlayerControllerSystem : GameSystem
    {
        private readonly IControlContext _controlContext;
        private readonly IntPtr _window;
        private readonly PlayerMovement _playerMovement;
        private bool _godMode;

        public PlayerControllerSystem(IGameServer server, IControlContext controlContext, IntPtr window, PlayerMovement playerMovement) : base(server)
        {
            if (controlContext == null) throw new ArgumentNullException(nameof(controlContext));
            if (window == IntPtr.Zero) throw new ArgumentNullException(nameof(window));
            if (playerMovement == null) throw new ArgumentNullException(nameof(playerMovement));
            _controlContext = controlContext;
            _window = window;
            _playerMovement = playerMovement;
            _godMode = false;
        }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _server.GetEntities())
            {
                var player = entity.GetComponent<Player>();
                if (player == null) continue;
                var physics = entity.GetComponent<PhysicsComponent>();
                if (physics == null) continue;

                if (_controlContext.GetKey(_window, Key.G) == InputAction.Press && !_godMode)
                {
                    _godMode = true;
                    Console.WriteLine("PlayerControllerSystem: God mode enabled");
                }
                else if (_controlContext.GetKey(_window, Key.G) == InputAction.Release && _godMode)
                {
                    _godMode = false;
                    Console.WriteLine("PlayerControllerSystem: God mode disabled");
                }

                // Sync from Player's public read-only Position (which delegates to TransformComponent)
                physics.Position = player.Physics.Position;

                if (_godMode)
                {
                    float zMove = 0;
                    if (_controlContext.GetKey(_window, Key.Space) == InputAction.Press) zMove += 20.0f * deltaTime;
                    if (_controlContext.GetKey(_window, Key.LeftControl) == InputAction.Press) zMove -= 20.0f * deltaTime;

                    // Single source of truth via TransformComponent
                    Vector3 newPos = new Vector3(player.Physics.Position.X, player.Physics.Position.Y, player.Physics.Position.Z + zMove);
                    player.Physics.Position = newPos;
                }

                // Use client's predicted rotation
                _server.Publish(new EntityMovedEvent(player.EntityId, new Vector2(player.Physics.Position.X, player.Physics.Position.Y), player.Physics.Rotation, player.SteamId));
                Console.WriteLine($"PlayerControllerSystem: Updated entity {player.EntityId}, Position={player.Physics.Position}, Rotation={player.Physics.Rotation}");
            }
        }
    }
}