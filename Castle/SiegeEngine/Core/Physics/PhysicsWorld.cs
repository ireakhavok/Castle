// Folder: SiegeEngine/Core/Physics
// File: PhysicsWorld.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.Physics
{
    public class PhysicsWorld
    {
        private readonly List<PhysicsComponent> _bodies = new List<PhysicsComponent>();
        private IHeightProvider _heightProvider;
        private float _accumulator;
        private Vector3 _gravity = new Vector3(0f, 0f, -9.81f);
        private readonly List<ContactManifold> _manifolds = new List<ContactManifold>();

        public bool UseFixedTimestep { get; set; } = true;
        public float FixedTimestep { get; set; } = 1f / 60f;
        public Vector3 Gravity
        {
            get => _gravity;
            set => _gravity = value;
        }

        public EventBus EventBus { get; set; }

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
                if (_accumulator > FixedTimestep * 5f)
                    _accumulator = FixedTimestep * 5f;

                while (_accumulator >= FixedTimestep)
                {
                    Integrate(FixedTimestep);
                    DetectAndResolveContacts(FixedTimestep);
                    _accumulator -= FixedTimestep;
                }
            }
            else
            {
                Integrate(deltaTime);
                DetectAndResolveContacts(deltaTime);
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
                    body.Velocity *= MathF.Max(0f, 1f - body.LinearDamping * dt);
                    body.AngularVelocity *= MathF.Max(0f, 1f - body.AngularDamping * dt);
                    body.Velocity += _gravity * dt;
                    body.Position += body.Velocity * dt;
                    ApplyGroundClamp(body);
                }
                else if (body.BodyType == BodyType.Kinematic)
                {
                    body.Velocity += _gravity * dt;
                    body.Position = new Vector3(
                        body.Position.X,
                        body.Position.Y,
                        body.Position.Z + body.Velocity.Z * dt);
                    ApplyGroundClamp(body);
                }
            }
        }

        private void ApplyGroundClamp(PhysicsComponent body)
        {
            if (body == null || _heightProvider == null) return;
            if (body.BodyType == BodyType.Static) return;

            float groundZ = _heightProvider.GetInterpolatedHeight(body.Position.X, body.Position.Y);

            if (body.Position.Z < groundZ)
            {
                body.Position = new Vector3(body.Position.X, body.Position.Y, groundZ);
                if (body.Velocity.Z < 0f)
                    body.Velocity = new Vector3(body.Velocity.X, body.Velocity.Y, 0f);
            }
        }

        private void DetectAndResolveContacts(float dt)
        {
            _manifolds.Clear();

            for (int i = 0; i < _bodies.Count; i++)
            {
                var a = _bodies[i];
                if (a == null || !a.CollisionEnabled || a.IsSleeping) continue;
                if (a.Shape == null) a.RebuildShape();

                for (int j = i + 1; j < _bodies.Count; j++)
                {
                    var b = _bodies[j];
                    if (b == null || !b.CollisionEnabled || b.IsSleeping) continue;
                    if (b.Shape == null) b.RebuildShape();

                    if (a.BodyType == BodyType.Static && b.BodyType == BodyType.Static)
                        continue;

                    var manifold = GenerateManifold(a, b);
                    if (manifold != null && manifold.PointCount > 0)
                        _manifolds.Add(manifold);
                }
            }

            for (int m = 0; m < _manifolds.Count; m++)
                ResolveManifold(_manifolds[m], dt);
        }

        private ContactManifold GenerateManifold(PhysicsComponent a, PhysicsComponent b)
        {
            if (a.BodyType == BodyType.Static && b.BodyType != BodyType.Static)
            {
                var tmp = a; a = b; b = tmp;
            }

            var shapeA = a.Shape;
            var shapeB = b.Shape;
            if (shapeA == null || shapeB == null) return null;

            var manifold = new ContactManifold { BodyA = a, BodyB = b };

            if (shapeA is CapsuleShape capA)
            {
                if (shapeB is ObbShape obbB)
                    CapsuleVsObb(capA, a, obbB, b, manifold);
                else if (shapeB is TriangleMeshShape meshB)
                    CapsuleVsMeshAabb(capA, a, meshB, b, manifold);
                else if (shapeB is CapsuleShape capB)
                    CapsuleVsCapsule(capA, a, capB, b, manifold);
            }
            else if (shapeA is ObbShape obbA)
            {
                if (shapeB is ObbShape obbB)
                    ObbVsObb(obbA, a, obbB, b, manifold);
                else if (shapeB is TriangleMeshShape meshB)
                    ObbVsMeshAabb(obbA, a, meshB, b, manifold);
                else if (shapeB is CapsuleShape capB)
                    CapsuleVsObb(capB, b, obbA, a, manifold);
            }
            else if (shapeA is TriangleMeshShape meshA)
            {
                if (shapeB is CapsuleShape capB)
                    CapsuleVsMeshAabb(capB, b, meshA, a, manifold);
                else if (shapeB is ObbShape obbB)
                    ObbVsMeshAabb(obbB, b, meshA, a, manifold);
            }

            return manifold.PointCount > 0 ? manifold : null;
        }

        private void CapsuleVsCapsule(CapsuleShape a, PhysicsComponent bodyA,
            CapsuleShape b, PhysicsComponent bodyB, ContactManifold manifold)
        {
            Vector3 ca = bodyA.Position + new Vector3(0, 0, a.Radius);
            Vector3 cb = bodyB.Position + new Vector3(0, 0, b.Radius);
            Vector3 delta = ca - cb;
            float dist = delta.Length();
            float rSum = a.Radius + b.Radius;
            if (dist < rSum && dist > 1e-6f)
            {
                Vector3 n = delta / dist;
                if (n.Z > 0.7f) return;
                manifold.Add(new ContactPoint
                {
                    Position = cb + n * b.Radius,
                    Normal = n,
                    Penetration = rSum - dist
                });
            }
        }

        private void CapsuleVsObb(CapsuleShape cap, PhysicsComponent capBody,
            ObbShape obb, PhysicsComponent obbBody, ContactManifold manifold)
        {
            float radius = cap.Radius;
            float height = cap.Height;
            Vector3 feet = capBody.Position;

            Vector3[] samples =
            {
                feet + new Vector3(0, 0, radius),
                feet + new Vector3(0, 0, height * 0.5f),
                feet + new Vector3(0, 0, height - radius)
            };

            for (int s = 0; s < samples.Length; s++)
            {
                Vector3 closest = ClosestPointOnObb(samples[s], obbBody.Position, obbBody.Rotation, obb);
                Vector3 delta = samples[s] - closest;
                float dist = delta.Length();
                if (dist < radius && dist > 1e-6f)
                {
                    Vector3 n = delta / dist;
                    if (n.Z > 0.7f) continue;
                    manifold.Add(new ContactPoint
                    {
                        Position = closest,
                        Normal = n,
                        Penetration = radius - dist
                    });
                }
            }
        }

        private void CapsuleVsMeshAabb(CapsuleShape cap, PhysicsComponent capBody,
            TriangleMeshShape mesh, PhysicsComponent meshBody, ContactManifold manifold)
        {
            mesh.GetAabb(meshBody.Position, meshBody.Rotation, out Vector3 aabbMin, out Vector3 aabbMax);
            Vector3 half = (aabbMax - aabbMin) * 0.5f;
            Vector3 centre = (aabbMin + aabbMax) * 0.5f;

            float radius = cap.Radius;
            float height = cap.Height;
            Vector3 feet = capBody.Position;

            Vector3[] samples =
            {
                feet + new Vector3(0, 0, radius),
                feet + new Vector3(0, 0, height * 0.5f),
                feet + new Vector3(0, 0, height - radius)
            };

            for (int s = 0; s < samples.Length; s++)
            {
                Vector3 closest = ClosestPointOnAabb(samples[s], centre, half);
                Vector3 delta = samples[s] - closest;
                float dist = delta.Length();
                if (dist < radius && dist > 1e-6f)
                {
                    Vector3 n = delta / dist;
                    if (n.Z > 0.7f) continue;
                    manifold.Add(new ContactPoint
                    {
                        Position = closest,
                        Normal = n,
                        Penetration = radius - dist
                    });
                }
            }
        }

        private void ObbVsObb(ObbShape a, PhysicsComponent bodyA,
            ObbShape b, PhysicsComponent bodyB, ContactManifold manifold)
        {
            Matrix4x4 rotA = Matrix4x4.CreateFromQuaternion(bodyA.Rotation);
            Matrix4x4 rotB = Matrix4x4.CreateFromQuaternion(bodyB.Rotation);
            Vector3 ca = bodyA.Position + Vector3.Transform(a.CenterOffset, rotA);
            Vector3 cb = bodyB.Position + Vector3.Transform(b.CenterOffset, rotB);
            Vector3 delta = ca - cb;
            float dist = delta.Length();
            float ra = a.HalfExtents.Length();
            float rb = b.HalfExtents.Length();
            if (dist < ra + rb && dist > 1e-6f)
            {
                Vector3 n = delta / dist;
                manifold.Add(new ContactPoint
                {
                    Position = cb + n * rb,
                    Normal = n,
                    Penetration = ra + rb - dist
                });
            }
        }

        private void ObbVsMeshAabb(ObbShape obb, PhysicsComponent obbBody,
            TriangleMeshShape mesh, PhysicsComponent meshBody, ContactManifold manifold)
        {
            mesh.GetAabb(meshBody.Position, meshBody.Rotation, out Vector3 aabbMin, out Vector3 aabbMax);
            Vector3 half = (aabbMax - aabbMin) * 0.5f;
            Vector3 centre = (aabbMin + aabbMax) * 0.5f;

            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(obbBody.Rotation);
            Vector3 obbCentre = obbBody.Position + Vector3.Transform(obb.CenterOffset, rot);

            Vector3 closest = ClosestPointOnAabb(obbCentre, centre, half);
            Vector3 delta = obbCentre - closest;
            float dist = delta.Length();
            float radius = obb.HalfExtents.Length();
            if (dist < radius && dist > 1e-6f)
            {
                Vector3 n = delta / dist;
                manifold.Add(new ContactPoint
                {
                    Position = closest,
                    Normal = n,
                    Penetration = radius - dist
                });
            }
        }

        private void ResolveManifold(ContactManifold m, float dt)
        {
            var a = m.BodyA;
            var b = m.BodyB;

            bool aDynamic = a.BodyType == BodyType.Dynamic;
            bool bDynamic = b.BodyType == BodyType.Dynamic;

            float invMassA = aDynamic ? 1f / MathF.Max(0.001f, a.Mass) : 0f;
            float invMassB = bDynamic ? 1f / MathF.Max(0.001f, b.Mass) : 0f;

            if (a.BodyType == BodyType.Kinematic && a.Shape is CapsuleShape)
                invMassA = 1f;
            if (b.BodyType == BodyType.Kinematic && b.Shape is CapsuleShape)
                invMassB = 1f;

            float totalInv = invMassA + invMassB;
            if (totalInv < 1e-8f) return;

            for (int i = 0; i < m.PointCount; i++)
            {
                var p = m.Points[i];
                Vector3 n = p.Normal;

                const float percent = 0.8f;
                const float slop = 0.01f;
                float correctionMag = MathF.Max(p.Penetration - slop, 0f) * percent;
                Vector3 correction = n * (correctionMag / totalInv);

                if (invMassA > 0f) a.Position += correction * invMassA;
                if (invMassB > 0f) b.Position -= correction * invMassB;

                Vector3 relVel = a.Velocity - b.Velocity;
                float velAlongNormal = Vector3.Dot(relVel, n);
                if (velAlongNormal > 0f) continue;

                float e = MathF.Min(a.Restitution, b.Restitution);
                float j = -(1f + e) * velAlongNormal / totalInv;
                Vector3 impulse = n * j;

                if (invMassA > 0f) a.Velocity += impulse * invMassA;
                if (invMassB > 0f) b.Velocity -= impulse * invMassB;

                relVel = a.Velocity - b.Velocity;
                Vector3 tangent = relVel - n * Vector3.Dot(relVel, n);
                float tLen = tangent.Length();
                if (tLen > 1e-6f)
                {
                    tangent /= tLen;
                    float jt = -Vector3.Dot(relVel, tangent) / totalInv;
                    float mu = MathF.Sqrt(a.Friction * b.Friction);
                    jt = Math.Clamp(jt, -j * mu, j * mu);
                    Vector3 frictionImpulse = tangent * jt;
                    if (invMassA > 0f) a.Velocity += frictionImpulse * invMassA;
                    if (invMassB > 0f) b.Velocity -= frictionImpulse * invMassB;
                }
            }
        }

        private static Vector3 ClosestPointOnObb(Vector3 point, Vector3 position, Quaternion rotation, ObbShape obb)
        {
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(rotation);
            Vector3 worldCentre = position + Vector3.Transform(obb.CenterOffset, rot);
            Matrix4x4.Invert(rot, out Matrix4x4 inv);
            Vector3 local = Vector3.Transform(point - worldCentre, inv);
            local = Vector3.Clamp(local, -obb.HalfExtents, obb.HalfExtents);
            return Vector3.Transform(local, rot) + worldCentre;
        }

        private static Vector3 ClosestPointOnAabb(Vector3 point, Vector3 centre, Vector3 halfExtents)
        {
            Vector3 local = point - centre;
            local = Vector3.Clamp(local, -halfExtents, halfExtents);
            return centre + local;
        }
    }
}