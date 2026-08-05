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
                }
                else if (body.BodyType == BodyType.Kinematic)
                {
                    // Previous-frame IsGrounded decides gravity. Contact phase will refresh the flag.
                    if (body.IsGrounded)
                    {
                        // Keep horizontal velocity; kill residual downward so we do not re-penetrate.
                        body.Velocity = new Vector3(body.Velocity.X, body.Velocity.Y, MathF.Max(0f, body.Velocity.Z));
                    }
                    else
                    {
                        body.Velocity += _gravity * dt;
                    }
                    body.Position = new Vector3(
                        body.Position.X,
                        body.Position.Y,
                        body.Position.Z + body.Velocity.Z * dt);
                }
            }
        }
        private void DetectAndResolveContacts(float dt)
        {
            // Clear grounded flags so only contacts generated this step can re-set them.
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
                    if (b == null || !b.CollisionEnabled || b.IsSleeping) continue;
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
            // Five axial samples for more reliable deep-penetration and edge contacts.
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
            Vector3 queryMin = feet - new Vector3(radius, radius, 0f);
            Vector3 queryMax = feet + new Vector3(radius, radius, height);
            mesh.GetAabb(meshBody.Position, meshBody.Rotation, out Vector3 aabbMin, out Vector3 aabbMax);
            if (queryMax.X < aabbMin.X || queryMin.X > aabbMax.X ||
                queryMax.Y < aabbMin.Y || queryMin.Y > aabbMax.Y ||
                queryMax.Z < aabbMin.Z || queryMin.Z > aabbMax.Z)
                return;
            _triA.Clear();
            _triB.Clear();
            _triC.Clear();
            mesh.QueryWorldTriangles(meshBody.Position, meshBody.Rotation, queryMin, queryMax, _triA, _triB, _triC);
            // Five axial samples for more reliable deep-penetration and edge contacts.
            Vector3[] samples =
            {
                feet + new Vector3(0, 0, radius),
                feet + new Vector3(0, 0, height * 0.25f),
                feet + new Vector3(0, 0, height * 0.5f),
                feet + new Vector3(0, 0, height * 0.75f),
                feet + new Vector3(0, 0, height - radius)
            };
            for (int t = 0; t < _triA.Count; t++)
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
            // Single stable feet contact — eliminates multi-sample fighting / visual double-image.
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
                // Supporting contact within slope limit → grounded.
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
            // Lowest point of the OBB along world Z.
            Vector3 localDown = Vector3.Transform(new Vector3(0, 0, -obb.HalfExtents.Z), rot);
            Vector3 bottom = centre + localDown;
            // Also consider the four lower corners for a more stable support.
            Vector3[] corners =
            {
                bottom,
                centre + Vector3.Transform(new Vector3( obb.HalfExtents.X,  obb.HalfExtents.Y, -obb.HalfExtents.Z), rot),
                centre + Vector3.Transform(new Vector3( obb.HalfExtents.X, -obb.HalfExtents.Y, -obb.HalfExtents.Z), rot),
                centre + Vector3.Transform(new Vector3(-obb.HalfExtents.X,  obb.HalfExtents.Y, -obb.HalfExtents.Z), rot),
                centre + Vector3.Transform(new Vector3(-obb.HalfExtents.X, -obb.HalfExtents.Y, -obb.HalfExtents.Z), rot)
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
        private void ResolveManifold(ContactManifold m, float dt)
        {
            var a = m.BodyA;
            var b = m.BodyB;
            // Statics = infinite mass. Dynamics use real mass.
            // ALL kinematics receive invMass = 1 so they can be pushed out of static geometry
            // while still integrating gravity. This replaces the old discrete ground clamp.
            float invMassA = 0f;
            float invMassB = 0f;
            if (a != null)
            {
                if (a.BodyType == BodyType.Dynamic)
                    invMassA = 1f / MathF.Max(0.001f, a.Mass);
                else if (a.BodyType == BodyType.Kinematic)
                    invMassA = 1f;
            }
            if (b != null)
            {
                if (b.BodyType == BodyType.Dynamic)
                    invMassB = 1f / MathF.Max(0.001f, b.Mass);
                else if (b.BodyType == BodyType.Kinematic)
                    invMassB = 1f;
            }
            // Heightfield (BodyB == null) stays infinite mass.
            float totalInv = invMassA + invMassB;
            if (totalInv < 1e-8f) return;
            for (int i = 0; i < m.PointCount; i++)
            {
                var p = m.Points[i];
                Vector3 n = p.Normal;
                const float percent = 0.8f;
                const float slop = 0.01f;
                float correctionMag = MathF.Max(p.Penetration - slop, 0f) * percent;
                // Heightfield + kinematic: vertical-only position correction.
                // Prevents the tilted normal from injecting lateral displacement that fights PlayerMovement.
                if (b == null && a != null && a.BodyType == BodyType.Kinematic)
                {
                    a.Position = new Vector3(a.Position.X, a.Position.Y, a.Position.Z + correctionMag);
                }
                else
                {
                    Vector3 correction = n * (correctionMag / totalInv);
                    if (invMassA > 0f) a.Position += correction * invMassA;
                    if (invMassB > 0f && b != null) b.Position -= correction * invMassB;
                }
                Vector3 velA = a != null ? a.Velocity : Vector3.Zero;
                Vector3 velB = b != null ? b.Velocity : Vector3.Zero;
                Vector3 relVel = velA - velB;
                float velAlongNormal = Vector3.Dot(relVel, n);
                if (velAlongNormal > 0f) continue;
                float e = MathF.Min(a?.Restitution ?? 0f, b?.Restitution ?? 0f);
                float j = -(1f + e) * velAlongNormal / totalInv;
                Vector3 impulse = n * j;
                if (invMassA > 0f) a.Velocity += impulse * invMassA;
                if (invMassB > 0f && b != null) b.Velocity -= impulse * invMassB;
                relVel = (a != null ? a.Velocity : Vector3.Zero) - (b != null ? b.Velocity : Vector3.Zero);
                Vector3 tangent = relVel - n * Vector3.Dot(relVel, n);
                float tLen = tangent.Length();
                if (tLen > 1e-6f)
                {
                    tangent /= tLen;
                    // Heightfield receives stronger friction + static stick to stop slope creep.
                    float mu;
                    if (b == null)
                        mu = MathF.Max(1.2f, (a?.Friction ?? 0.5f) * 2f);
                    else
                        mu = MathF.Sqrt((a?.Friction ?? 0.5f) * (b?.Friction ?? 0.5f));
                    if (tLen < 0.15f && b == null && a != null)
                    {
                        // Static friction: completely cancel residual tangential velocity.
                        a.Velocity -= tangent * Vector3.Dot(a.Velocity, tangent);
                    }
                    else
                    {
                        float jt = -Vector3.Dot(relVel, tangent) / totalInv;
                        jt = Math.Clamp(jt, -j * mu, j * mu);
                        Vector3 frictionImpulse = tangent * jt;
                        if (invMassA > 0f) a.Velocity += frictionImpulse * invMassA;
                        if (invMassB > 0f && b != null) b.Velocity -= frictionImpulse * invMassB;
                    }
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