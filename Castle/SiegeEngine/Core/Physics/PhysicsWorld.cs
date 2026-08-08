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
        private HeightfieldShape _heightfieldShape;
        private float _accumulator;
        private Vector3 _gravity = new Vector3(0f, 0f, -9.81f);
        private readonly List<ContactManifold> _manifolds = new List<ContactManifold>();
        private readonly List<Vector3> _triA = new List<Vector3>(64);
        private readonly List<Vector3> _triB = new List<Vector3>(64);
        private readonly List<Vector3> _triC = new List<Vector3>(64);
        public bool UseFixedTimestep { get; set; } = true;
        public float FixedTimestep { get; set; } = 1f / 60f;
        public Vector3 Gravity
        {
            get => _gravity;
            set => _gravity = value;
        }
        public int SolverIterations { get; set; } = 10;
        public EventBus EventBus { get; set; }
        public void SetHeightProvider(IHeightProvider provider)
        {
            _heightProvider = provider;
            _heightfieldShape = provider != null ? new HeightfieldShape(provider) : null;
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
        public void SnapToGround(PhysicsComponent body) { }
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
                    UpdateSleeping(FixedTimestep);
                    _accumulator -= FixedTimestep;
                }
            }
            else
            {
                Integrate(deltaTime);
                DetectAndResolveContacts(deltaTime);
                UpdateSleeping(deltaTime);
            }
            for (int i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body == null) continue;
                body.RenderPosition = body.Position;
            }
        }
        private void Integrate(float dt)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body == null || body.IsSleeping || body.BodyType == BodyType.Static)
                    continue;
                bool isCharacterCapsule = body.BodyType == BodyType.Kinematic && body.Shape is CapsuleShape;
                // Linear damping
                body.Velocity *= MathF.Max(0f, 1f - body.LinearDamping * dt);
                if (isCharacterCapsule)
                {
                    body.AngularVelocity = Vector3.Zero;
                }
                else
                {
                    body.AngularVelocity *= MathF.Max(0f, 1f - body.AngularDamping * dt);
                }
                // Gravity
                if (body.InvMass > 0f)
                {
                    if (isCharacterCapsule)
                    {
                        if (body.IsGrounded)
                        {
                            // Exact kinematic projection: kill residual velocity into the support direction.
                            float vn = Vector3.Dot(body.Velocity, Vector3.UnitZ);
                            if (vn < 0f)
                                body.Velocity -= Vector3.UnitZ * vn;
                        }
                        else
                        {
                            body.Velocity += _gravity * dt;
                        }
                    }
                    else
                    {
                        body.Velocity += _gravity * dt;
                    }
                }
                // Linear integration
                body.Position += body.Velocity * dt;
                // Angular integration (character capsule skipped)
                if (!isCharacterCapsule && body.AngularVelocity.LengthSquared() > 1e-12f)
                {
                    Quaternion omegaQ = new Quaternion(body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z, 0f);
                    Quaternion dq = Quaternion.Multiply(omegaQ, body.Rotation) * 0.5f;
                    body.Rotation = Quaternion.Normalize(body.Rotation + dq * dt);
                }
            }
        }
        private void DetectAndResolveContacts(float dt)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body != null && body.BodyType == BodyType.Kinematic)
                    body.IsGrounded = false;
            }
            _manifolds.Clear();
            for (int i = 0; i < _bodies.Count; i++)
            {
                var a = _bodies[i];
                if (a == null || !a.CollisionEnabled || a.IsSleeping) continue;
                if (a.Shape == null) a.RebuildShape();
                for (int j = i + 1; j < _bodies.Count; j++)
                {
                    var b = _bodies[j];
                    if (b == null || !b.CollisionEnabled) continue;
                    // Wake sleeping body if contacted by an active body
                    if (b.IsSleeping)
                    {
                        if (a.BodyType == BodyType.Static) continue;
                        b.IsSleeping = false;
                        b.SleepTimer = 0f;
                    }
                    if (b.Shape == null) b.RebuildShape();
                    if (a.BodyType == BodyType.Static && b.BodyType == BodyType.Static)
                        continue;
                    var manifold = GenerateManifold(a, b);
                    if (manifold != null && manifold.PointCount > 0)
                        _manifolds.Add(manifold);
                }
            }
            if (_heightfieldShape != null)
            {
                for (int i = 0; i < _bodies.Count; i++)
                {
                    var body = _bodies[i];
                    if (body == null || !body.CollisionEnabled || body.IsSleeping) continue;
                    if (body.BodyType == BodyType.Static) continue;
                    if (body.Shape == null) body.RebuildShape();
                    var manifold = new ContactManifold { BodyA = body, BodyB = null };
                    if (body.Shape is CapsuleShape cap)
                        CapsuleVsHeightfield(cap, body, _heightfieldShape, manifold);
                    else if (body.Shape is ObbShape obb)
                        ObbVsHeightfield(obb, body, _heightfieldShape, manifold);
                    if (manifold.PointCount > 0)
                        _manifolds.Add(manifold);
                }
            }
            // Velocity-level sequential impulse only (no position correction)
            for (int iter = 0; iter < SolverIterations; iter++)
            {
                for (int m = 0; m < _manifolds.Count; m++)
                    ResolveVelocity(_manifolds[m]);
            }
            // Hard geometric projection (no velocity change, no soft bias)
            ProjectPositions();
        }
        private void ResolveVelocity(ContactManifold m)
        {
            var a = m.BodyA;
            var b = m.BodyB;
            float invMassA = a != null ? a.InvMass : 0f;
            float invMassB = b != null ? b.InvMass : 0f;
            float totalInvMass = invMassA + invMassB;
            if (totalInvMass < 1e-8f && (a == null || a.InvInertiaLocal == Vector3.Zero) && (b == null || b.InvInertiaLocal == Vector3.Zero))
                return;
            for (int i = 0; i < m.PointCount; i++)
            {
                var p = m.Points[i];
                Vector3 n = p.Normal;
                Vector3 comA = a != null ? a.WorldCentreOfMass : Vector3.Zero;
                Vector3 comB = b != null ? b.WorldCentreOfMass : Vector3.Zero;
                Vector3 rA = p.Position - comA;
                Vector3 rB = p.Position - comB;
                Vector3 velA = a != null ? a.Velocity + Vector3.Cross(a.AngularVelocity, rA) : Vector3.Zero;
                Vector3 velB = b != null ? b.Velocity + Vector3.Cross(b.AngularVelocity, rB) : Vector3.Zero;
                Vector3 relVel = velA - velB;
                float velAlongNormal = Vector3.Dot(relVel, n);
                if (velAlongNormal > 0f) continue;
                float angularEffA = 0f;
                float angularEffB = 0f;
                if (a != null && a.InvInertiaLocal != Vector3.Zero)
                {
                    Vector3 rn = Vector3.Cross(rA, n);
                    Vector3 iRn = a.ApplyInvInertiaWorld(rn);
                    angularEffA = Vector3.Dot(rn, iRn);
                }
                if (b != null && b.InvInertiaLocal != Vector3.Zero)
                {
                    Vector3 rn = Vector3.Cross(rB, n);
                    Vector3 iRn = b.ApplyInvInertiaWorld(rn);
                    angularEffB = Vector3.Dot(rn, iRn);
                }
                float invMassEff = totalInvMass + angularEffA + angularEffB;
                if (invMassEff < 1e-8f) continue;
                float e = MathF.Min(a?.Restitution ?? 0f, b?.Restitution ?? 0f);
                float j = -(1f + e) * velAlongNormal / invMassEff;
                Vector3 impulse = n * j;
                if (a != null && invMassA > 0f)
                {
                    a.Velocity += impulse * invMassA;
                    a.AngularVelocity += a.ApplyInvInertiaWorld(Vector3.Cross(rA, impulse));
                }
                if (b != null && invMassB > 0f)
                {
                    b.Velocity -= impulse * invMassB;
                    b.AngularVelocity -= b.ApplyInvInertiaWorld(Vector3.Cross(rB, impulse));
                }
                // Friction
                relVel = (a != null ? a.Velocity + Vector3.Cross(a.AngularVelocity, rA) : Vector3.Zero)
                       - (b != null ? b.Velocity + Vector3.Cross(b.AngularVelocity, rB) : Vector3.Zero);
                Vector3 tangent = relVel - n * Vector3.Dot(relVel, n);
                float tLen = tangent.Length();
                if (tLen > 1e-6f)
                {
                    tangent /= tLen;
                    float mu = b == null
                        ? MathF.Max(1.2f, (a?.Friction ?? 0.5f) * 2f)
                        : MathF.Sqrt((a?.Friction ?? 0.5f) * (b?.Friction ?? 0.5f));
                    float jt = -Vector3.Dot(relVel, tangent) / invMassEff;
                    jt = Math.Clamp(jt, -j * mu, j * mu);
                    Vector3 frictionImpulse = tangent * jt;
                    if (a != null && invMassA > 0f)
                    {
                        a.Velocity += frictionImpulse * invMassA;
                        a.AngularVelocity += a.ApplyInvInertiaWorld(Vector3.Cross(rA, frictionImpulse));
                    }
                    if (b != null && invMassB > 0f)
                    {
                        b.Velocity -= frictionImpulse * invMassB;
                        b.AngularVelocity -= b.ApplyInvInertiaWorld(Vector3.Cross(rB, frictionImpulse));
                    }
                }
            }
        }
        private void ProjectPositions()
        {
            const float numericSlop = 0.001f;
            for (int m = 0; m < _manifolds.Count; m++)
            {
                var manifold = _manifolds[m];
                var a = manifold.BodyA;
                var b = manifold.BodyB;
                float invMassA = a != null ? a.InvMass : 0f;
                float invMassB = b != null ? b.InvMass : 0f;
                float totalInv = invMassA + invMassB;
                if (totalInv < 1e-8f) continue;
                for (int i = 0; i < manifold.PointCount; i++)
                {
                    var p = manifold.Points[i];
                    float depth = p.Penetration - numericSlop;
                    if (depth <= 0f) continue;
                    Vector3 corr = p.Normal * (depth / totalInv);
                    if (invMassA > 0f && a != null)
                        a.Position += corr * invMassA;
                    if (invMassB > 0f && b != null)
                        b.Position -= corr * invMassB;
                }
            }
        }
        private void UpdateSleeping(float dt)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body == null || body.BodyType != BodyType.Dynamic || body.IsSleeping)
                    continue;
                float ke = 0.5f * body.Mass * body.Velocity.LengthSquared();
                if (body.InvInertiaLocal != Vector3.Zero)
                {
                    // Approximate rotational KE using the diagonal
                    Vector3 w = body.AngularVelocity;
                    ke += 0.5f * (w.X * w.X / MathF.Max(body.InvInertiaLocal.X, 1e-8f)
                                + w.Y * w.Y / MathF.Max(body.InvInertiaLocal.Y, 1e-8f)
                                + w.Z * w.Z / MathF.Max(body.InvInertiaLocal.Z, 1e-8f));
                }
                float threshold = body.SleepThreshold;
                if (ke < threshold * threshold)
                {
                    body.SleepTimer += dt;
                    if (body.SleepTimer > 0.5f)
                        body.IsSleeping = true;
                }
                else
                {
                    body.SleepTimer = 0f;
                }
            }
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
                    CapsuleVsTriangleMesh(capA, a, meshB, b, manifold);
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
                    CapsuleVsTriangleMesh(capB, b, meshA, a, manifold);
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
                feet + new Vector3(0, 0, height * 0.25f),
                feet + new Vector3(0, 0, height * 0.5f),
                feet + new Vector3(0, 0, height * 0.75f),
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
                    manifold.Add(new ContactPoint
                    {
                        Position = closest,
                        Normal = n,
                        Penetration = radius - dist
                    });
                }
            }
        }
        private void CapsuleVsTriangleMesh(CapsuleShape cap, PhysicsComponent capBody,
            TriangleMeshShape mesh, PhysicsComponent meshBody, ContactManifold manifold)
        {
            float radius = cap.Radius;
            float height = cap.Height;
            Vector3 feet = capBody.Position;
            Vector3 queryMin = feet - new Vector3(radius * 1.5f, radius * 1.5f, 0f);
            Vector3 queryMax = feet + new Vector3(radius * 1.5f, radius * 1.5f, height);
            mesh.GetAabb(meshBody.Position, meshBody.Rotation, out Vector3 aabbMin, out Vector3 aabbMax);
            if (queryMax.X < aabbMin.X || queryMin.X > aabbMax.X ||
                queryMax.Y < aabbMin.Y || queryMin.Y > aabbMax.Y ||
                queryMax.Z < aabbMin.Z || queryMin.Z > aabbMax.Z)
                return;
            _triA.Clear();
            _triB.Clear();
            _triC.Clear();
            mesh.QueryWorldTriangles(meshBody.Position, meshBody.Rotation, queryMin, queryMax, _triA, _triB, _triC);

            // Hard safety limit – complex static meshes must never explode cost
            const int maxTriangles = 48;
            int triCount = Math.Min(_triA.Count, maxTriangles);

            Vector3[] samples =
            {
                feet + new Vector3(0, 0, radius),
                feet + new Vector3(0, 0, height * 0.2f),
                feet + new Vector3(0, 0, height * 0.4f),
                feet + new Vector3(0, 0, height * 0.6f),
                feet + new Vector3(0, 0, height * 0.8f),
                feet + new Vector3(0, 0, height - radius)
            };
            for (int t = 0; t < triCount; t++)
            {
                Vector3 a = _triA[t];
                Vector3 b = _triB[t];
                Vector3 c = _triC[t];
                for (int s = 0; s < samples.Length; s++)
                {
                    Vector3 closest = ClosestPointOnTriangle(samples[s], a, b, c);
                    Vector3 delta = samples[s] - closest;
                    float dist = delta.Length();
                    if (dist < radius && dist > 1e-6f)
                    {
                        Vector3 n = delta / dist;
                        manifold.Add(new ContactPoint
                        {
                            Position = closest,
                            Normal = n,
                            Penetration = radius - dist
                        });
                        if (manifold.PointCount >= 4) return;
                    }
                }
            }
        }
        private void CapsuleVsHeightfield(CapsuleShape cap, PhysicsComponent body,
            HeightfieldShape field, ContactManifold manifold)
        {
            Vector3 feet = body.Position;
            float groundZ = field.SampleHeight(feet.X, feet.Y);
            float penetration = groundZ - feet.Z;
            if (penetration > 0f)
            {
                Vector3 n = field.SampleNormal(feet.X, feet.Y);
                manifold.Add(new ContactPoint
                {
                    Position = new Vector3(feet.X, feet.Y, groundZ),
                    Normal = n,
                    Penetration = penetration
                });
                float slopeDeg = MathF.Acos(Math.Clamp(n.Z, -1f, 1f)) * (180f / MathF.PI);
                if (slopeDeg <= body.SlopeLimitDegrees)
                    body.IsGrounded = true;
            }
        }
        private void ObbVsHeightfield(ObbShape obb, PhysicsComponent body,
            HeightfieldShape field, ContactManifold manifold)
        {
            Matrix4x4 rot = Matrix4x4.CreateFromQuaternion(body.Rotation);
            Vector3 centre = body.Position + Vector3.Transform(obb.CenterOffset, rot);
            Vector3 hx = Vector3.Transform(new Vector3(obb.HalfExtents.X, 0f, 0f), rot);
            Vector3 hy = Vector3.Transform(new Vector3(0f, obb.HalfExtents.Y, 0f), rot);
            Vector3 hz = Vector3.Transform(new Vector3(0f, 0f, obb.HalfExtents.Z), rot);
            Vector3[] corners =
            {
                centre + hx + hy + hz,
                centre + hx + hy - hz,
                centre + hx - hy + hz,
                centre + hx - hy - hz,
                centre - hx + hy + hz,
                centre - hx + hy - hz,
                centre - hx - hy + hz,
                centre - hx - hy - hz
            };
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 p = corners[i];
                float groundZ = field.SampleHeight(p.X, p.Y);
                float penetration = groundZ - p.Z;
                if (penetration > 0f)
                {
                    Vector3 n = field.SampleNormal(p.X, p.Y);
                    manifold.Add(new ContactPoint
                    {
                        Position = new Vector3(p.X, p.Y, groundZ),
                        Normal = n,
                        Penetration = penetration
                    });
                    float slopeDeg = MathF.Acos(Math.Clamp(n.Z, -1f, 1f)) * (180f / MathF.PI);
                    if (slopeDeg <= body.SlopeLimitDegrees)
                        body.IsGrounded = true;
                    if (manifold.PointCount >= 4) return;
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
        private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = p - a;
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                return a + ab * v;
            }
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                return a + ac * w;
            }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return b + (c - b) * w;
            }
            float denom = 1f / (va + vb + vc);
            float v2 = vb * denom;
            float w2 = vc * denom;
            return a + ab * v2 + ac * w2;
        }
    }
}