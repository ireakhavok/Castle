using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Networking;
using SiegeEngine.PlayerSystem;

namespace SiegeEngine.Systems
{
    public class ClientPredictionSystem : GameSystem
    {
        private readonly IGameServer _server;
        private readonly EventBus _eventBus;
        private readonly Dictionary<int, List<MovementRequest>> _buffer = new Dictionary<int, List<MovementRequest>>();
        private readonly Dictionary<int, RemoteState> _remotes = new Dictionary<int, RemoteState>();
        private readonly HashSet<int> _predictedEntities = new HashSet<int>();
        private uint _clientTick;
        private float _tickAccum;
        private const float TickDt = 1f / 60f;
        private const int MaxBufferedTicks = 120;

        public uint ClientTick => _clientTick;

        private struct RemoteState
        {
            public Vector3 From;
            public Vector3 To;
            public Quaternion FromRot;
            public Quaternion ToRot;
            public float T;
        }

        public ClientPredictionSystem(IGameServer server, EventBus eventBus) : base(server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _eventBus.Subscribe<EntityMovedEvent>(OnEntityMoved);
            _eventBus.Subscribe<EntityReplicationEvent>(OnReplication);
        }

        public void EnqueueMovementRequest(int entityId, Vector2 requestedPos, Quaternion requestedRotation, ulong steamId)
        {
            var request = new MovementRequest(requestedPos, requestedRotation, steamId, DateTime.UtcNow.Ticks, _clientTick);
            if (!_buffer.TryGetValue(entityId, out var list))
            {
                list = new List<MovementRequest>();
                _buffer[entityId] = list;
            }
            list.Add(request);
            _predictedEntities.Add(entityId);
            while (list.Count > MaxBufferedTicks)
                list.RemoveAt(0);
        }

        public override void Update(float deltaTime)
        {
            _tickAccum += deltaTime;
            while (_tickAccum >= TickDt)
            {
                _tickAccum -= TickDt;
                _clientTick++;
            }
        }

        private void OnReplication(EntityReplicationEvent e)
        {
            if (e?.Deltas == null) return;
            for (int i = 0; i < e.Deltas.Count; i++)
            {
                var d = e.Deltas[i];
                if (_predictedEntities.Contains(d.Id))
                    continue;
                Entity entity = _server.GetEntityById(d.Id);
                EntityDeltaTracker.Apply(entity, d);
            }
        }

        private void OnEntityMoved(EntityMovedEvent e)
        {
            Entity entity = _server.GetEntityById(e.EntityId);
            if (entity == null) return;
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics == null) return;

            Vector3 serverPos = new Vector3(e.Position.X, e.Position.Y, physics.Position.Z);

            if (_predictedEntities.Contains(e.EntityId) && entity.GetComponent<Player>() != null)
            {
                ReconcileLocal(e.EntityId, physics, serverPos, e.Rotation, e.AckTick);
                return;
            }

            physics.Position = serverPos;
            physics.Rotation = e.Rotation;
        }

        private void ReconcileLocal(int entityId, PhysicsComponent physics, Vector3 serverPos, Quaternion serverRot, uint ackTick)
        {
            if (!_buffer.TryGetValue(entityId, out var list) || list.Count == 0)
            {
                physics.Position = serverPos;
                physics.Rotation = serverRot;
                return;
            }

            int keep = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Tick > ackTick)
                {
                    keep = i;
                    break;
                }
                keep = i + 1;
            }
            if (keep > 0)
                list.RemoveRange(0, keep);

            physics.Position = serverPos;
            physics.Rotation = serverRot;
            for (int i = 0; i < list.Count; i++)
            {
                MovementRequest pending = list[i];
                physics.Position = new Vector3(pending.Position.X, pending.Position.Y, physics.Position.Z);
                physics.Rotation = pending.Rotation;
            }
        }
    }
}
