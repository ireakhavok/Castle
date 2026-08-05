// Folder: SiegeEngine/Core/Physics
// File: PhysicsWorld.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.Definitions;

namespace SiegeEngine.Core.Physics
{
    /// <summary>
    /// Core simulation world. Owns the timestep accumulator, body list and (later)
    /// broadphase / contacts / islands. Phase 1 performs simple gravity integration
    /// for Dynamic bodies and ground projection for Kinematic (player) bodies.
    /// Position is treated as the contact / feet point — no half-height offset.
    /// </summary>
    public class PhysicsWorld
    {
        private readonly List<PhysicsComponent> _bodies = new List<PhysicsComponent>();
        private IHeightProvider _heightProvider;
        private float _accumulator;
        private Vector3 _gravity = new Vector3(0f, 0f, -9.81f);

        public bool UseFixedTimestep { get; set; } = true;
        public float FixedTimestep { get; set; } = 1f / 60f;
        public Vector3 Gravity
        {
            get => _gravity;
            set => _gravity = value;
        }

        public void SetHeightProvider(IHeightProvider provider)
        {
            _heightProvider = provider;
        }

        public IHeightProvider HeightProvider => _heightProvider;

        public void RegisterBody(PhysicsComponent body)
        {
            if (body == null) return;
            if (!_bodies.Contains(body))
                _bodies.Add(body);
        }

        public void UnregisterBody(PhysicsComponent body)
        {
            if (body == null) return;
            _bodies.Remove(body);
        }

        public void ClearBodies()
        {
            _bodies.Clear();
        }

        /// <summary>
        /// Ground projection. Safe to call after kinematic movement writes.
        /// Kinematic / Dynamic → only lift when below the surface (stop falling through).
        /// Static             → never touched.
        /// Position is the contact / feet point — no half-height offset is applied.
        /// </summary>
        public void SnapToGround(PhysicsComponent body)
        {
            ApplyGroundClamp(body);
        }

        public void Step(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            if (UseFixedTimestep)
            {
                _accumulator += deltaTime;
                // Clamp to avoid spiral of death on long frames
                if (_accumulator > FixedTimestep * 5f)
                    _accumulator = FixedTimestep * 5f;

                while (_accumulator >= FixedTimestep)
                {
                    Integrate(FixedTimestep);
                    _accumulator -= FixedTimestep;
                }
            }
            else
            {
                Integrate(deltaTime);
            }
        }

        private void Integrate(float dt)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body == null || body.IsSleeping) continue;

                if (body.BodyType == BodyType.Dynamic)
                {
                    // Simple linear damping
                    body.Velocity *= MathF.Max(0f, 1f - body.LinearDamping * dt);
                    body.AngularVelocity *= MathF.Max(0f, 1f - body.AngularDamping * dt);

                    // Gravity
                    body.Velocity += _gravity * dt;

                    // Integrate position
                    body.Position += body.Velocity * dt;

                    ApplyGroundClamp(body);
                }
                else if (body.BodyType == BodyType.Kinematic)
                {
                    // Physics owns vertical motion. Horizontal is owned by PlayerMovement.
                    // Apply gravity, integrate only Z, then conditional ground response.
                    body.Velocity += _gravity * dt;

                    body.Position = new Vector3(
                        body.Position.X,
                        body.Position.Y,
                        body.Position.Z + body.Velocity.Z * dt);

                    ApplyGroundClamp(body);
                }
                // Static: intentionally ignored — stays exactly where placed
            }
        }

        private void ApplyGroundClamp(PhysicsComponent body)
        {
            if (body == null || _heightProvider == null) return;
            if (body.BodyType == BodyType.Static) return;

            float groundZ = _heightProvider.GetInterpolatedHeight(body.Position.X, body.Position.Y);

            // Both Kinematic and Dynamic: only prevent penetration. Never weld to the surface.
            // Position is the contact / feet point for Phase 1.
            if (body.Position.Z < groundZ)
            {
                body.Position = new Vector3(body.Position.X, body.Position.Y, groundZ);
                if (body.Velocity.Z < 0f)
                    body.Velocity = new Vector3(body.Velocity.X, body.Velocity.Y, 0f);
            }
        }
    }
}