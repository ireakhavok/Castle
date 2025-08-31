using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using SiegeEngine.Networking;
using SiegeEngine.PlayerSystem;

namespace SiegeEngine.Systems
{
    public class ClientPredictionSystem : GameSystem
    {
        private readonly IGameServer _server;
        private readonly EventBus _eventBus;
        private readonly Queue<MovementRequest> _pendingMoves = new Queue<MovementRequest>();
        private readonly Dictionary<int, Vector3> _lastServerPositions = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Quaternion> _lastServerRotations = new Dictionary<int, Quaternion>();
        private readonly float _reconciliationThreshold = 0.1f;
        private readonly float _bufferDuration = 0.1f; // Buffer moves for 0.1s
        private readonly float _maxDistance = 20.0f; // Match ServerValidationSystem

        public ClientPredictionSystem(IGameServer server, EventBus eventBus) : base(server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _eventBus.Subscribe<EntityMovedEvent>(OnEntityMoved);
        }

        public void EnqueueMovementRequest(int entityId, Vector2 requestedPos, Quaternion requestedRotation, ulong steamId)
        {
            var request = new MovementRequest(requestedPos, requestedRotation, steamId, DateTime.UtcNow.Ticks);
            _pendingMoves.Enqueue(request);
            Console.WriteLine($"ClientPredictionSystem: Enqueued movement request for entity {entityId} to {requestedPos}, Rotation={requestedRotation}");
        }

        public override void Update(float deltaTime)
        {
            // No sending in Update; requests are sent via PlayerMovement.sendMovementRequest
        }

        private void OnEntityMoved(EntityMovedEvent e)
        {
            Entity entity = _server.GetEntityById(e.EntityId);
            if (entity == null) return;

            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics == null) return;

            var player = entity.GetComponent<Player>();
            if (player == null) return;

            // Validate position to mirror server logic
            bool isValid = Vector2.Distance(new Vector2(physics.Position.X, physics.Position.Y), e.Position) <= _maxDistance &&
                           e.Position.X >= 0 && e.Position.X <= 128 && e.Position.Y >= 0 && e.Position.Y <= 72;

            Console.WriteLine($"ClientPredictionSystem: Processed movement for entity {e.EntityId} to {e.Position}, Rotation={e.Rotation}, IsValid={isValid}");

            // Check if server sent identity rotation (likely validation failure)
            bool isIdentityRotation = e.Rotation.X == 0 && e.Rotation.Y == 0 && e.Rotation.Z == 0 && e.Rotation.W == 1;

            if (isValid && !isIdentityRotation)
            {
                _lastServerPositions[e.EntityId] = new Vector3(e.Position.X, e.Position.Y, physics.Position.Z);
                _lastServerRotations[e.EntityId] = e.Rotation;

                if (Vector3.Distance(physics.Position, _lastServerPositions[e.EntityId]) < _reconciliationThreshold)
                {
                    Console.WriteLine($"ClientPredictionSystem: Skipped reconciliation for entity {e.EntityId} due to recent server state");
                    return;
                }
            }
            else
            {
                if (_pendingMoves.Count > 0 && (DateTime.UtcNow.Ticks - _pendingMoves.Peek().Timestamp) / 10000000f < _bufferDuration)
                {
                    Console.WriteLine($"ClientPredictionSystem: Buffering moves for entity {e.EntityId}");
                    // Apply latest pending move to smooth transition
                    var latestRequest = _pendingMoves.Peek();
                    physics.Position = new Vector3(latestRequest.Position.X, latestRequest.Position.Y, physics.Position.Z);
                    physics.Rotation = latestRequest.Rotation;
                    Console.WriteLine($"ClientPredictionSystem: Applied buffered move for entity {e.EntityId} to {latestRequest.Position}, Rotation={latestRequest.Rotation}");
                    return;
                }

                physics.Position = new Vector3(e.Position.X, e.Position.Y, physics.Position.Z);
                _lastServerPositions[e.EntityId] = physics.Position;
                // Retain client rotation if server sent identity
                if (isIdentityRotation && _pendingMoves.Count > 0)
                {
                    physics.Rotation = _pendingMoves.Peek().Rotation;
                    Console.WriteLine($"ClientPredictionSystem: Retained buffered rotation for entity {e.EntityId}: {physics.Rotation}");
                }
                else
                {
                    physics.Rotation = e.Rotation;
                }
                _lastServerRotations[e.EntityId] = physics.Rotation;
                Console.WriteLine($"ClientPredictionSystem: Reverted entity {e.EntityId} to server position {physics.Position}, rotation {physics.Rotation}");

                var tempQueue = new Queue<MovementRequest>(_pendingMoves);
                _pendingMoves.Clear();
                while (tempQueue.Count > 0)
                {
                    var pendingRequest = tempQueue.Dequeue();
                    physics.Position = new Vector3(pendingRequest.Position.X, pendingRequest.Position.Y, physics.Position.Z);
                    physics.Rotation = pendingRequest.Rotation;
                    _pendingMoves.Enqueue(pendingRequest);
                    Console.WriteLine($"ClientPredictionSystem: Reapplied pending moves for entity {e.EntityId} to {pendingRequest.Position}, Rotation={pendingRequest.Rotation}");
                }
            }
        }

        private static Vector4 ToVector4(Quaternion q) => new Vector4(q.X, q.Y, q.Z, q.W);
    }
}