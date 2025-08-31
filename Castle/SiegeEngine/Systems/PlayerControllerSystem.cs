using System;
using System.Numerics;
using Silk.NET.GLFW;
using SiegeEngine.Events;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Interfaces;
using SiegeEngine.Definitions;

namespace SiegeEngine.Systems
{
    public unsafe class PlayerControllerSystem : GameSystem
    {
        private readonly Glfw _glfw;
        private readonly PlayerMovement _playerMovement;
        private readonly WindowHandle* _window;
        private bool _godMode;

        public PlayerControllerSystem(IGameServer server, Glfw glfw, WindowHandle* window, PlayerMovement playerMovement) : base(server)
        {
            if (glfw == null) throw new ArgumentNullException(nameof(glfw));
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (playerMovement == null) throw new ArgumentNullException(nameof(playerMovement));
            _glfw = glfw;
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

                if (_glfw.GetKey(_window, Keys.G) == 1 && !_godMode)
                {
                    _godMode = true;
                    Console.WriteLine("PlayerControllerSystem: God mode enabled");
                }
                else if (_glfw.GetKey(_window, Keys.G) == 0 && _godMode)
                {
                    _godMode = false;
                    Console.WriteLine("PlayerControllerSystem: God mode disabled");
                }

                physics.Position = player.Position;

                if (_godMode)
                {
                    float zMove = 0;
                    if (_glfw.GetKey(_window, Keys.Space) == 1) zMove += 20.0f * deltaTime;
                    if (_glfw.GetKey(_window, Keys.ControlLeft) == 1) zMove -= 20.0f * deltaTime;
                    player.Position = new Vector3(player.Position.X, player.Position.Y, player.Position.Z + zMove);
                    physics.Position = player.Position;
                }

                // Use client's predicted rotation
                _server.Publish(new EntityMovedEvent(player.EntityId, new Vector2(player.Position.X, player.Position.Y), player.Physics.Rotation, player.SteamId));
                Console.WriteLine($"PlayerControllerSystem: Updated entity {player.EntityId}, Position={player.Position}, Rotation={player.Physics.Rotation}");
            }
        }
    }
}