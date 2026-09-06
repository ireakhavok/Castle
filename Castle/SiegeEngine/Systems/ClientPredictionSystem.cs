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
        private readonly HashSet<int> _predictedEntities = new HashSet<int>();
        private uint _clientTick;
        private float _tickAccum;
        private const int MaxBufferedTicks = 120;

        public uint ClientTick => _clientTick;

        public ClientPredictionSystem(IGameServer server, EventBus eventBus) : base(server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _eventBus.Subscribe<EntityMovedEvent>(OnEntityMoved);
        }

        public void EnqueueMovementRequest(int entityId, Vector3 requestedPos, Quaternion requestedRotation, ulong steamId)
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
            if (deltaTime > 0f)
                _clientTick++;
        }

        private void OnEntityMoved(EntityMovedEvent e)
        {
            if (e == null) return;
            var deltas = e.AllDeltas();
            for (int i = 0; i < deltas.Count; i++)
                ApplyDelta(deltas[i], e.Authoritative);
        }

        private void ApplyDelta(EntityNetDelta d, bool authoritative)
        {
            if (d == null) return;
            Entity entity = _server.GetEntityById(d.Id);
            if (entity == null) return;

            if (_predictedEntities.Contains(d.Id) && entity.GetComponent<Player>() != null)
            {
                if (!authoritative)
                    return;
                ReconcileLocal(d);
                return;
            }
            EntityDeltaTracker.Apply(entity, d);
        }

        private void ReconcileLocal(EntityNetDelta d)
        {
            Entity entity = _server.GetEntityById(d.Id);
            var physics = entity?.GetComponent<PhysicsComponent>();
            if (physics == null) return;

            EntityDeltaTracker.Apply(entity, d);

            if (!_buffer.TryGetValue(d.Id, out var list) || list.Count == 0)
                return;

            int keep = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Tick > d.AckTick)
                {
                    keep = i;
                    break;
                }
                keep = i + 1;
            }
            if (keep > 0)
                list.RemoveRange(0, keep);

            for (int i = 0; i < list.Count; i++)
            {
                MovementRequest pending = list[i];
                physics.Position = pending.Position;
                physics.Rotation = pending.Rotation;
                physics.RenderPosition = pending.Position;
            }
        }
    }
}
