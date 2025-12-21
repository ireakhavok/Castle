// SiegeEngine.PlayerSystem/Player.cs
using System;
using System.Numerics;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.AssetParsing;
using SiegeEngine.Core.Definitions;

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
        public Player(int entityId, Vector3 position, ulong steamId = 0, ModelManager modelLoader = null)
        {
            EntityId = entityId;
            Position = position;
            _physics = new PhysicsComponent();
            _physics.Position = position;
            _physics.Size = new Vector3(10f, 10f, 10f);
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
        public void InitializeCamera(IControlContext controlContext, IntPtr window)
        {
            if (_camera == null)
                _camera = new CameraController(controlContext, window, this);
        }
        public void Update(float deltaTime, IntPtr window, float scrollDelta, PlayerMovement movement, bool isGameActive)
        {
            if (_camera != null)
            {
                _camera.Update(deltaTime, scrollDelta, isGameActive);
                if (isGameActive)
                {
                    movement.Update(this, deltaTime, (id, pos, rotation) => { }, _camera);
                    Position = _physics.Position;
                    //Console.WriteLine($"Player: Updated, Position={Position}, Perspective={_camera.CurrentPerspective}");
                }
            }
        }
    }
}