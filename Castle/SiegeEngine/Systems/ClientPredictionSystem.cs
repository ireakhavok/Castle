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
        private struct PoseSnapshot
        {
            public uint Tick;
            public Vector3 Position;
            public Quaternion Rotation;
            public float AnimTime;
        }

        private readonly IGameServer _server;
        private readonly EventBus _eventBus;
        private readonly Dictionary<int, List<MovementRequest>> _buffer = new Dictionary<int, List<MovementRequest>>();
        private readonly HashSet<int> _predictedEntities = new HashSet<int>();
        private readonly Dictionary<int, PoseSnapshot> _snapFrom = new Dictionary<int, PoseSnapshot>();
        private readonly Dictionary<int, PoseSnapshot> _snapTo = new Dictionary<int, PoseSnapshot>();
        private readonly Dictionary<int, float> _interpT = new Dictionary<int, float>();
        private uint _clientTick;
        private float _tickAccum;
        private const int MaxBufferedTicks = 120;
        private const float InterpWindow = 0.1f;

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
            InterpolateRemotes(deltaTime);
        }

        private void InterpolateRemotes(float deltaTime)
        {
            foreach (var kv in _snapTo)
            {
                int id = kv.Key;
                if (_predictedEntities.Contains(id)) continue;
                Entity entity = _server.GetEntityById(id);
                var physics = entity?.GetComponent<PhysicsComponent>();
                if (physics == null) continue;
                if (!_snapFrom.TryGetValue(id, out var from))
                {
                    physics.RenderPosition = kv.Value.Position;
                    continue;
                }
                float t = 0f;
                _interpT.TryGetValue(id, out t);
                t += InterpWindow > 0f ? deltaTime / InterpWindow : 1f;
                if (t > 1f) t = 1f;
                _interpT[id] = t;
                physics.RenderPosition = Vector3.Lerp(from.Position, kv.Value.Position, t);
            }
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
            PushRemoteSnapshot(entity, d);
            EntityDeltaTracker.Apply(entity, d);
        }

        private void PushRemoteSnapshot(Entity entity, EntityNetDelta d)
        {
            var physics = entity.GetComponent<PhysicsComponent>();
            if (physics == null) return;
            PoseSnapshot next = new PoseSnapshot
            {
                Tick = d.AckTick,
                Position = (d.Dirty & EntityDeltaTracker.DirtyPos) != 0
                    ? new Vector3(d.Px, d.Py, d.Pz)
                    : physics.Position,
                Rotation = (d.Dirty & EntityDeltaTracker.DirtyRot) != 0
                    ? new Quaternion(d.Rx, d.Ry, d.Rz, d.Rw)
                    : physics.Rotation,
                AnimTime = d.AnimTime
            };
            if (_snapTo.TryGetValue(d.Id, out var prev))
                _snapFrom[d.Id] = prev;
            else
                _snapFrom[d.Id] = new PoseSnapshot { Tick = 0, Position = physics.RenderPosition, Rotation = physics.Rotation, AnimTime = 0f };
            _snapTo[d.Id] = next;
            _interpT[d.Id] = 0f;
        }

        private void ReconcileLocal(EntityNetDelta d)
        {
            Entity entity = _server.GetEntityById(d.Id);
            var physics = entity?.GetComponent<PhysicsComponent>();
            if (physics == null) return;

            EntityDeltaTracker.Apply(entity, d);
            physics.RenderPosition = physics.Position;

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
