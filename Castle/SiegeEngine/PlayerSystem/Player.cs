using SiegeEngine.AssetParsing;
using SiegeEngine.Definitions;
using SiegeEngine.Managers;
using Silk.NET.GLFW;
using System;
using System.Numerics;

namespace SiegeEngine.PlayerSystem
{
    public class Player : IComponent
    {
        public int EntityId { get; private set; }
        public Vector3 Position { get; set; }
        private CameraController _camera;
        private readonly PhysicsComponent _physics;
        public ulong SteamId { get; set; }
        public FBXModel Model { get; private set; }

        public Player(int entityId, Vector3 position, Glfw glfw = null, ulong steamId = 0, ModelManager modelLoader = null)
        {
            EntityId = entityId;
            Position = position;
            _physics = new PhysicsComponent();
            _physics.Position = position;
            _physics.Size = new Vector3(10f, 10f, 10f);
            _camera = glfw != null ? new CameraController(glfw, this) : null;
            SteamId = steamId;
            if (modelLoader != null && modelLoader.TryGetModel("man_mesh", out var model))
            {
                Model = model;
            }
            else
            {
                Console.WriteLine("Player: Error: Failed to load man_mesh model, using default cube");
                Model = FBXParserBase.CreateDefaultCubeModel();
            }
        }

        public CameraController Camera => _camera;
        public PhysicsComponent Physics => _physics;

        public void InitializeCamera(Glfw glfw)
        {
            if (_camera == null)
                _camera = new CameraController(glfw, this);
        }

        public unsafe void Update(float deltaTime, WindowHandle* window, float scrollDelta, PlayerMovement movement, bool isGameActive)
        {
            if (_camera != null)
            {
                _camera.Update(deltaTime, window, scrollDelta, isGameActive);
                if (isGameActive)
                {
                    movement.Update(this, deltaTime, (id, pos, rotation) => { }, window, _camera);
                    Position = _physics.Position;
                    Console.WriteLine($"Player: Updated, Position={Position}, Perspective={_camera.CurrentPerspective}");
                }
            }
        }
    }
}