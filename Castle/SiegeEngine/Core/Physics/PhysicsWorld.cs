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
        private const float ContactSkin = 0.03f;
        public bool UseFixedTimestep { get; set; } = false;
        public float FixedTimestep { get; set; } = 1f / 60f;
        public Vector3 Gravity
        {
            get => _gravity;
            set => _gravity = value;
        }
        public int SolverIterations { get; set; } = 10;
        public EventBus EventBus { get; set; }
        public IReadOnlyList<ContactManifold> CurrentManifolds => _manifolds;
        public IReadOnlyList<PhysicsComponent> Bodies => _bodies;
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
        private static float MeshBottomWorldZ(PhysicsComponent body)
        {
            float z = body.Position.Z;
            Vector3 localMin = body.LocalBoundsMinCm;
            Vector3 localMax = body.LocalBoundsMaxCm;
            if (localMin.X <= localMax.X && localMin.Y <= localMax.Y && localMin.Z <= localMax.Z
                && !float.IsInfinity(localMin.X) && !float.IsInfinity(localMax.X))
            {
                z += Vector3.Transform(localMin, body.Rotation).Z;
            }
            return z;
        }
        public void SnapToGround(PhysicsComponent body)
        {
            if (body == null) return;
            if (!_bodies.Contains(body))
                RegisterBody(body);
            if (_heightProvider != null)
            {
                float ground = _heightProvider.GetInterpolatedHeight(body.Position.X, body.Position.Y);
                if (!float.IsNaN(ground) && !float.IsInfinity(ground) && body.Position.Z < ground)
                {
                    Vector3 p = body.Position;
                    p.Z = ground;
                    body.Position = p;
                }
            }
            ResolveSpawnOverlaps(body);
            body.RenderPosition = body.Position;
        }

        public void ResolveSpawnOverlaps(PhysicsComponent body)
        {
            if (body == null) return;
            if (!_bodies.Contains(body))
                RegisterBody(body);
            for (int pass = 0; pass < 8; pass++)
            {
                Vector3 before = body.Position;
                DetectAndResolveContacts(0f);
                ProjectPositions();
                if ((body.Position - before).LengthSquared() < 1e-10f)
                    break;
            }
            body.RenderPosition = body.Position;
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
                bool isCharacter = body.KeepUpright
                    || (body.BodyType == BodyType.Kinematic && body.Shape is CapsuleShape);
                body.Velocity *= MathF.Max(0f, 1f - body.LinearDamping * dt);
                if (isCharacter)
                {
                    body.AngularVelocity = Vector3.Zero;
                    if (body.IsGrounded)
                    {
                        Vector3 n = body.SupportNormal;
                        if (n.LengthSquared() < 1e-8f)
                            n = Vector3.UnitZ;
                        else
                            n = Vector3.Normalize(n);
                        float vn = Vector3.Dot(body.Velocity, n);
                        if (vn < 0f)
                            body.Velocity -= n * vn;
                    }
                    else
                    {
                        body.Velocity += _gravity * dt;
                    }
                    body.Position += body.Velocity * dt;
                }
                else
                {
                    if (body.InvMass > 0f)
                        body.Velocity += _gravity * dt;
                    Vector3 com = body.WorldCentreOfMass;
                    com += body.Velocity * dt;
                    if (body.AngularVelocity.LengthSquared() > 1e-12f)
                    {
                        Quaternion omegaQ = new Quaternion(body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z, 0f);
                        Quaternion dq = Quaternion.Multiply(omegaQ, body.Rotation) * 0.5f;
                        body.Rotation = Quaternion.Normalize(body.Rotation + dq * dt);
                        body.AngularVelocity *= MathF.Max(0f, 1f - body.AngularDamping * dt);
                    }
                    body.Position = com - Vector3.Transform(body.LocalCentreOfMass, body.Rotation);
                }
            }
        }
        private void DetectAndResolveContacts(float dt)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body != null && (body.BodyType == BodyType.Kinematic || body.KeepUpright))
                {
                    body.IsGrounded = false;
                    body.SupportNormal = Vector3.Zero;
                }
            }
            for (int i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body?.Shape is BoneHitboxShape hitboxes)
                {
                    Matrix4x4[] pose = body.BonePoseGlobals;
                    if (pose == null || pose.Length == 0)
                    {
                        // Identity skin = raw mesh verts (same as renderer when BoneMatrices is null).
                        pose = null;
                    }
                    hitboxes.Pose(body.Position, body.Rotation, pose);
                }
            }
            _manifolds.Clear();
            for (int i = 0; i < _bodies.Count; i++)
            {
                var a = _bodies[i];
                if (a == null || !a.CollisionEnabled) continue;
                if (a.Shape == null) continue;
                for (int j = i + 1; j < _bodies.Count; j++)
                {
                    var b = _bodies[j];
                    if (b == null || !b.CollisionEnabled) continue;
                    if (b.Shape == null) continue;
                    if (a.BodyType == BodyType.Static && b.BodyType == BodyType.Static)
                        continue;
                    if (a.IsSleeping && b.IsSleeping)
                        continue;
                    if (a.IsSleeping && b.BodyType == BodyType.Static)
                        continue;
                    if (b.IsSleeping && a.BodyType == BodyType.Static)
                        continue;
                    var manifold = GenerateManifold(a, b);
                    if (manifold != null && manifold.PointCount > 0)
                    {
                        if (a.IsSleeping)
                        {
                            a.IsSleeping = false;
                            a.SleepTimer = 0f;
                        }
                        if (b.IsSleeping)
                        {
                            b.IsSleeping = false;
                            b.SleepTimer = 0f;
                        }
                        _manifolds.Add(manifold);
                    }
                }
            }
            if (_heightfieldShape != null)
            {
                for (int i = 0; i < _bodies.Count; i++)
                {
                    var body = _bodies[i];
                    if (body == null || !body.CollisionEnabled || body.IsSleeping) continue;
                    if (body.BodyType == BodyType.Static) continue;
                    if (body.Shape == null) continue;
                    var manifold = new ContactManifold { BodyA = body, BodyB = null };
                    if (body.Shape is CapsuleShape cap)
                        CapsuleVsHeightfield(cap, body, _heightfieldShape, manifold);
                    else if (body.Shape is SphereShape sphere)
                        SphereVsHeightfield(sphere, body, _heightfieldShape, manifold);
                    else if (body.Shape is ObbShape obb)
                        ObbVsHeightfield(obb, body, _heightfieldShape, manifold);
                    else if (body.Shape is TriangleMeshShape mesh)
                    {
                        if (body.KeepUpright)
                            TriangleMeshPlayerVsHeightfield(mesh, body, _heightfieldShape, manifold);
                        else
                            TriangleMeshVsHeightfield(mesh, body, _heightfieldShape, manifold);
                    }
                    else if (body.Shape is BoneHitboxShape hitboxes)
                        BoneHitboxVsHeightfield(hitboxes, body, _heightfieldShape, manifold);
                    if (manifold.PointCount > 0)
                        _manifolds.Add(manifold);
                }
            }
            for (int iter = 0; iter < SolverIterations; iter++)
            {
                for (int m = 0; m < _manifolds.Count; m++)
                    ResolveVelocity(_manifolds[m]);
            }
            ProjectPositions();
            RepairNormalVelocities();
            ApplyRollingResistance(dt);
            ApplyRestingDeadZone();
        }
        private static bool IsStaticPartner(PhysicsComponent b)
        {
            return b == null || b.BodyType == BodyType.Static || b.InvMass <= 0f;
        }
        private int PickRestContact(ContactManifold manifold)
        {
            int best = 0;
            float bestDot = float.MinValue;
            Vector3 againstG = -_gravity;
            float gLen = againstG.Length();
            if (gLen > 1e-8f) againstG /= gLen;
            else againstG = Vector3.UnitZ;
            for (int i = 0; i < manifold.PointCount; i++)
            {
                float d = Vector3.Dot(manifold.Points[i].Normal, againstG);
                if (d > bestDot)
                {
                    bestDot = d;
                    best = i;
                }
            }
            return best;
        }
        private void ApplyRollingResistance(float dt)
        {
            for (int m = 0; m < _manifolds.Count; m++)
            {
                var manifold = _manifolds[m];
                if (!IsStaticPartner(manifold.BodyB)) continue;
                var a = manifold.BodyA;
                if (a == null || a.BodyType != BodyType.Dynamic || a.InvMass <= 0f) continue;
                if (manifold.PointCount == 0) continue;
                int rest = PickRestContact(manifold);
                var p = manifold.Points[rest];
                Vector3 n = p.Normal;
                Vector3 rA = p.Position - a.WorldCentreOfMass;
                Vector3 vPlane = a.Velocity - n * Vector3.Dot(a.Velocity, n);
                float vPlaneLen = vPlane.Length();
                if (vPlaneLen < 1e-5f) continue;
                Vector3 tDir = vPlane / vPlaneLen;
                float nForce = a.Mass * MathF.Abs(Vector3.Dot(_gravity, n));
                if (nForce < 1e-6f) continue;
                float maxForce = a.RollingResistance * nForce;
                float maxImpulse = maxForce * dt;
                float jt = -MathF.Min(vPlaneLen * a.Mass, maxImpulse);
                Vector3 forceImpulse = tDir * jt;
                a.Velocity += forceImpulse * a.InvMass;
                a.AngularVelocity += a.ApplyInvInertiaWorld(Vector3.Cross(rA, forceImpulse));
                p.RollingResistanceImpulse = forceImpulse;
                manifold.Points[rest] = p;
            }
        }
        private void ResolveVelocity(ContactManifold m)
        {
            var a = m.BodyA;
            var b = m.BodyB;
            float invMassA = a != null ? a.InvMass : 0f;
            float invMassB = b != null ? b.InvMass : 0f;
            float totalInvMass = invMassA + invMassB;
            bool kinematicVsStatic = a != null && a.BodyType == BodyType.Kinematic
                                  && (b == null || b.BodyType == BodyType.Static);
            if (totalInvMass < 1e-8f && !kinematicVsStatic
                && (a == null || a.InvInertiaLocal == Vector3.Zero)
                && (b == null || b.InvInertiaLocal == Vector3.Zero))
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
                if (kinematicVsStatic)
                {
                    a.Velocity -= n * velAlongNormal;
                    continue;
                }
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
                float zA = a != null ? a.Velocity.Z : 0f;
                float zB = b != null ? b.Velocity.Z : 0f;
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
                relVel = (a != null ? a.Velocity + Vector3.Cross(a.AngularVelocity, rA) : Vector3.Zero)
                       - (b != null ? b.Velocity + Vector3.Cross(b.AngularVelocity, rB) : Vector3.Zero);
                Vector3 tangent = relVel - n * Vector3.Dot(relVel, n);
                float tLen = tangent.Length();
                if (tLen > 1e-6f)
                {
                    tangent /= tLen;
                    float muKinetic = b == null
                        ? (a?.KineticFriction ?? 0.60f)
                        : MathF.Sqrt((a?.Friction ?? 0.5f) * (b?.Friction ?? 0.5f));
                    float muStatic = b == null
                        ? (a?.StaticFriction ?? 0.85f)
                        : muKinetic * 1.25f;
                    float jtMax = (tLen < 0.06f ? muStatic : muKinetic) * MathF.Abs(j);
                    float jt = -Vector3.Dot(relVel, tangent) / invMassEff;
                    jt = Math.Clamp(jt, -jtMax, jtMax);
                    Vector3 frictionImpulse = tangent * jt;
                    if (a != null && invMassA > 0f && a.ReceiveFriction)
                    {
                        a.Velocity += frictionImpulse * invMassA;
                        a.AngularVelocity += a.ApplyInvInertiaWorld(Vector3.Cross(rA, frictionImpulse));
                    }
                    if (b != null && invMassB > 0f && b.ReceiveFriction)
                    {
                        b.Velocity -= frictionImpulse * invMassB;
                        b.AngularVelocity -= b.ApplyInvInertiaWorld(Vector3.Cross(rB, frictionImpulse));
                    }
                    p.FrictionImpulse = frictionImpulse;
                }
                if (b != null && a != null && !a.ReceiveVerticalContact)
                    a.Velocity = new Vector3(a.Velocity.X, a.Velocity.Y, zA);
                if (b != null && !b.ReceiveVerticalContact)
                    b.Velocity = new Vector3(b.Velocity.X, b.Velocity.Y, zB);
                m.Points[i] = p;
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
                bool kinematicVsStatic = a != null && a.BodyType == BodyType.Kinematic
                                      && (b == null || b.BodyType == BodyType.Static);
                if (totalInv < 1e-8f && !kinematicVsStatic) continue;
                float posZA = a != null ? a.Position.Z : 0f;
                float posZB = b != null ? b.Position.Z : 0f;

                if (kinematicVsStatic)
                {
                    // Apply only the single deepest contact. Multiple conflicting
                    // normals in one frame are what cause the character to pop.
                    int best = -1;
                    float bestPen = 0f;
                    for (int i = 0; i < manifold.PointCount; i++)
                    {
                        float depth = manifold.Points[i].Penetration - numericSlop;
                        if (depth > bestPen)
                        {
                            bestPen = depth;
                            best = i;
                        }
                    }
                    if (best >= 0)
                        a.Position += manifold.Points[best].Normal * bestPen;
                    RestoreVerticalPosition(a, b, posZA, posZB);
                    continue;
                }

                if (a != null && a.BodyType == BodyType.Dynamic && IsStaticPartner(b)
                    && manifold.PointCount >= 2)
                {
                    ProjectPlaneContacts(manifold, numericSlop);
                    RestoreVerticalPosition(a, b, posZA, posZB);
                    continue;
                }

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
                RestoreVerticalPosition(a, b, posZA, posZB);
            }
        }
        private static void RestoreVerticalPosition(PhysicsComponent a, PhysicsComponent b, float posZA, float posZB)
        {
            if (b != null && a != null && !a.ReceiveVerticalContact)
                a.Position = new Vector3(a.Position.X, a.Position.Y, posZA);
            if (b != null && !b.ReceiveVerticalContact)
                b.Position = new Vector3(b.Position.X, b.Position.Y, posZB);
        }
        private void ProjectPlaneContacts(ContactManifold manifold, float slop)
        {
            var a = manifold.BodyA;
            if (a == null) return;
            int n = manifold.PointCount;
            var used = new bool[n];
            Vector3 delta = Vector3.Zero;
            for (int i = 0; i < n; i++)
            {
                if (used[i]) continue;
                float d1 = manifold.Points[i].Penetration - slop;
                Vector3 n1 = manifold.Points[i].Normal;
                d1 -= Vector3.Dot(delta, n1);
                if (d1 <= 0f)
                {
                    used[i] = true;
                    continue;
                }
                int partner = -1;
                float partnerC = 0f;
                for (int j = i + 1; j < n; j++)
                {
                    if (used[j]) continue;
                    Vector3 n2 = manifold.Points[j].Normal;
                    float c = Vector3.Dot(n1, n2);
                    if (MathF.Abs(c) < 0.999f)
                    {
                        partner = j;
                        partnerC = c;
                        break;
                    }
                }
                if (partner < 0)
                {
                    delta += n1 * d1;
                    used[i] = true;
                    continue;
                }
                Vector3 n2p = manifold.Points[partner].Normal;
                float d2 = manifold.Points[partner].Penetration - slop;
                d2 -= Vector3.Dot(delta, n2p);
                if (d2 <= 0f)
                {
                    delta += n1 * d1;
                    used[i] = true;
                    used[partner] = true;
                    continue;
                }
                float denom = 1f - partnerC * partnerC;
                if (denom < 1e-6f)
                {
                    delta += n1 * MathF.Max(d1, d2);
                }
                else
                {
                    float aa = (d1 - d2 * partnerC) / denom;
                    float bb = (d2 - d1 * partnerC) / denom;
                    if (aa < 0f)
                    {
                        aa = 0f;
                        bb = d2;
                    }
                    if (bb < 0f)
                    {
                        bb = 0f;
                        aa = d1;
                    }
                    delta += n1 * aa + n2p * bb;
                }
                used[i] = true;
                used[partner] = true;
            }
            a.Position += delta;
        }
        private void RepairNormalVelocities()
        {
            for (int m = 0; m < _manifolds.Count; m++)
            {
                var manifold = _manifolds[m];
                var a = manifold.BodyA;
                var b = manifold.BodyB;
                float invMassA = a != null ? a.InvMass : 0f;
                float invMassB = b != null ? b.InvMass : 0f;
                float totalInv = invMassA + invMassB;
                bool kinematicVsStatic = a != null && a.BodyType == BodyType.Kinematic
                                      && (b == null || b.BodyType == BodyType.Static);
                if (totalInv < 1e-8f && !kinematicVsStatic) continue;
                for (int i = 0; i < manifold.PointCount; i++)
                {
                    var p = manifold.Points[i];
                    Vector3 n = p.Normal;
                    Vector3 comA = a != null ? a.WorldCentreOfMass : Vector3.Zero;
                    Vector3 comB = b != null ? b.WorldCentreOfMass : Vector3.Zero;
                    Vector3 rA = p.Position - comA;
                    Vector3 rB = p.Position - comB;
                    Vector3 velA = a != null ? a.Velocity + Vector3.Cross(a.AngularVelocity, rA) : Vector3.Zero;
                    Vector3 velB = b != null ? b.Velocity + Vector3.Cross(b.AngularVelocity, rB) : Vector3.Zero;
                    Vector3 relVel = velA - velB;
                    float vn = Vector3.Dot(relVel, n);
                    if (vn >= 0f) continue;
                    float zA = a != null ? a.Velocity.Z : 0f;
                    float zB = b != null ? b.Velocity.Z : 0f;
                    if (kinematicVsStatic)
                    {
                        a.Velocity -= n * vn;
                        if (b != null && a != null && !a.ReceiveVerticalContact)
                            a.Velocity = new Vector3(a.Velocity.X, a.Velocity.Y, zA);
                        continue;
                    }
                    float shareA = invMassA / totalInv;
                    float shareB = invMassB / totalInv;
                    if (a != null && invMassA > 0f)
                    {
                        a.Velocity -= n * vn * shareA;
                        if (a.InvInertiaLocal != Vector3.Zero)
                            a.AngularVelocity -= a.ApplyInvInertiaWorld(Vector3.Cross(rA, n * vn * shareA));
                    }
                    if (b != null && invMassB > 0f)
                    {
                        b.Velocity += n * vn * shareB;
                        if (b.InvInertiaLocal != Vector3.Zero)
                            b.AngularVelocity += b.ApplyInvInertiaWorld(Vector3.Cross(rB, n * vn * shareB));
                    }
                    if (b != null && a != null && !a.ReceiveVerticalContact)
                        a.Velocity = new Vector3(a.Velocity.X, a.Velocity.Y, zA);
                    if (b != null && !b.ReceiveVerticalContact)
                        b.Velocity = new Vector3(b.Velocity.X, b.Velocity.Y, zB);
                }
            }
        }
        private void ApplyRestingDeadZone()
        {
            const float restThreshold = 0.05f;
            for (int m = 0; m < _manifolds.Count; m++)
            {
                var manifold = _manifolds[m];
                if (!IsStaticPartner(manifold.BodyB)) continue;
                var a = manifold.BodyA;
                if (a == null || a.BodyType != BodyType.Dynamic || a.InvMass <= 0f) continue;
                if (manifold.PointCount == 0) continue;
                float speed = a.Velocity.Length();
                float spin = a.AngularVelocity.Length();
                if (speed > restThreshold || spin > restThreshold * 2.5f) continue;
                Vector3 n = manifold.Points[PickRestContact(manifold)].Normal;
                Vector3 gParallel = _gravity - n * Vector3.Dot(_gravity, n);
                float gParLen = gParallel.Length();
                float nForceApprox = a.Mass * MathF.Abs(Vector3.Dot(_gravity, n)) + a.Mass * 2f;
                float frictionCapacity = a.StaticFriction * nForceApprox;
                if (gParLen * a.Mass <= frictionCapacity)
                {
                    a.Velocity = Vector3.Zero;
                    a.AngularVelocity = Vector3.Zero;
                }
            }
        }
        private void UpdateSleeping(float dt)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body == null || body.BodyType != BodyType.Dynamic || body.IsSleeping || body.KeepUpright)
                    continue;
                float ke = 0.5f * body.Mass * body.Velocity.LengthSquared();
                if (body.InvInertiaLocal != Vector3.Zero)
                {
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
            if (shapeA is BoneHitboxShape boxesA)
            {
                if (shapeB is TriangleMeshShape meshB)
                    BoneHitboxVsTriangleMesh(boxesA, a, meshB, b, manifold);
                else if (shapeB is BoneHitboxShape boxesB)
                    BoneHitboxVsBoneHitbox(boxesA, a, boxesB, b, manifold);
                else if (shapeB is SphereShape sphereB)
                    BoneHitboxVsSphere(boxesA, a, sphereB, b, manifold);
                else if (shapeB is CapsuleShape capB)
                    BoneHitboxVsCapsule(boxesA, a, capB, b, manifold);
            }
            else if (shapeA is CapsuleShape capA)
            {
                if (shapeB is ObbShape obbB)
                    CapsuleVsObb(capA, a, obbB, b, manifold);
                else if (shapeB is TriangleMeshShape meshB)
                    CapsuleVsTriangleMesh(capA, a, meshB, b, manifold);
                else if (shapeB is CapsuleShape capB)
                    CapsuleVsCapsule(capA, a, capB, b, manifold);
                else if (shapeB is SphereShape sphereB)
                    SphereVsCapsule(sphereB, b, capA, a, manifold);
                else if (shapeB is BoneHitboxShape boxesB)
                    BoneHitboxVsCapsule(boxesB, b, capA, a, manifold);
            }
            else if (shapeA is SphereShape sphereA)
            {
                if (shapeB is CapsuleShape capB)
                    SphereVsCapsule(sphereA, a, capB, b, manifold);
                else if (shapeB is TriangleMeshShape meshB)
                    SphereVsTriangleMesh(sphereA, a, meshB, b, manifold);
                else if (shapeB is SphereShape sphereB)
                    SphereVsSphere(sphereA, a, sphereB, b, manifold);
                else if (shapeB is BoneHitboxShape boxesB)
                    BoneHitboxVsSphere(boxesB, b, sphereA, a, manifold);
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
                else if (shapeB is SphereShape sphereB)
                    SphereVsTriangleMesh(sphereB, b, meshA, a, manifold);
                else if (shapeB is ObbShape obbB)
                    ObbVsMeshAabb(obbB, b, meshA, a, manifold);
                else if (shapeB is TriangleMeshShape meshB)
                    TriangleMeshVsTriangleMesh(meshA, a, meshB, b, manifold);
                else if (shapeB is BoneHitboxShape boxesB)
                    BoneHitboxVsTriangleMesh(boxesB, b, meshA, a, manifold);
            }
            return manifold.PointCount > 0 ? manifold : null;
        }


        private void BoneHitboxVsHeightfield(BoneHitboxShape boxes, PhysicsComponent body,
            HeightfieldShape field, ContactManifold manifold)
        {
            if (body.KeepUpright)
            {
                Vector3 feet = boxes.LowestWorldPoint();
                if (feet.Z == 0f && feet.X == 0f && feet.Y == 0f)
                    feet = new Vector3(body.Position.X, body.Position.Y, body.Position.Z);
                float ground = field.SampleHeight(feet.X, feet.Y);
                float verticalPen = ground - feet.Z;
                if (verticalPen > -ContactSkin)
                {
                    Vector3 n = field.SampleNormal(feet.X, feet.Y);
                    float nZ = MathF.Max(n.Z, 0.15f);
                    float pen = MathF.Max(0f, verticalPen) / nZ;
                    manifold.Add(new ContactPoint
                    {
                        Position = new Vector3(feet.X, feet.Y, ground),
                        Normal = n,
                        Penetration = pen
                    });
                    float slopeDeg = MathF.Acos(Math.Clamp(n.Z, -1f, 1f)) * (180f / MathF.PI);
                    if (slopeDeg <= body.SlopeLimitDegrees)
                    {
                        body.IsGrounded = true;
                        body.SupportNormal = n;
                    }
                }
                return;
            }
            for (int i = 0; i < boxes.Primitives.Length; i++)
            {
                Vector3[] pts = { boxes.WorldA[i], boxes.WorldB[i] };
                float r = boxes.Primitives[i].Radius;
                for (int p = 0; p < pts.Length; p++)
                {
                    Vector3 c = pts[p];
                    float ground = field.SampleHeight(c.X, c.Y);
                    float verticalPen = ground - (c.Z - r);
                    if (verticalPen > -ContactSkin)
                    {
                        Vector3 n = field.SampleNormal(c.X, c.Y);
                        float nZ = MathF.Max(n.Z, 0.15f);
                        manifold.Add(new ContactPoint
                        {
                            Position = new Vector3(c.X, c.Y, ground),
                            Normal = n,
                            Penetration = MathF.Max(0f, verticalPen) / nZ
                        });
                    }
                }
            }
        }

        private void BoneHitboxVsTriangleMesh(BoneHitboxShape boxes, PhysicsComponent boxBody,
            TriangleMeshShape mesh, PhysicsComponent meshBody, ContactManifold manifold)
        {
            const float skin = 0.02f;
            for (int i = 0; i < boxes.Primitives.Length; i++)
            {
                Vector3 a = boxes.WorldA[i];
                Vector3 b = boxes.WorldB[i];
                float radius = boxes.Primitives[i].Radius;
                Vector3 mid = (a + b) * 0.5f;
                _triA.Clear(); _triB.Clear(); _triC.Clear();
                mesh.QueryClosestWorldTriangles(meshBody.Position, meshBody.Rotation, mid, _triA, _triB, _triC, 4);
                for (int t = 0; t < _triA.Count; t++)
                {
                    ClosestPointsSegmentTriangle(a, b, _triA[t], _triB[t], _triC[t],
                        out Vector3 pa, out Vector3 pb, out float dist);
                    float pen = radius - dist;
                    if (pen <= -skin) continue;
                    Vector3 n = pa - pb;
                    float nLen = n.Length();
                    if (nLen < 1e-8f)
                    {
                        n = Vector3.Cross(_triB[t] - _triA[t], _triC[t] - _triA[t]);
                        nLen = n.Length();
                        if (nLen < 1e-8f) continue;
                    }
                    n /= nLen;
                    if (Vector3.Dot(n, boxBody.WorldCentreOfMass - pb) < 0f)
                        n = -n;
                    manifold.Add(new ContactPoint
                    {
                        Position = pb,
                        Normal = n,
                        Penetration = MathF.Max(0f, pen)
                    });
                }
            }
        }

        private void BoneHitboxVsBoneHitbox(BoneHitboxShape aBoxes, PhysicsComponent bodyA,
            BoneHitboxShape bBoxes, PhysicsComponent bodyB, ContactManifold manifold)
        {
            for (int i = 0; i < aBoxes.Primitives.Length; i++)
            {
                Vector3 a0 = aBoxes.WorldA[i];
                Vector3 a1 = aBoxes.WorldB[i];
                float ra = aBoxes.Primitives[i].Radius;
                for (int j = 0; j < bBoxes.Primitives.Length; j++)
                {
                    Vector3 b0 = bBoxes.WorldA[j];
                    Vector3 b1 = bBoxes.WorldB[j];
                    float rb = bBoxes.Primitives[j].Radius;
                    ClosestPointsOnSegments(a0, a1, b0, b1, out Vector3 pa, out Vector3 pb);
                    Vector3 d = pa - pb;
                    float dist = d.Length();
                    float pen = ra + rb - dist;
                    if (pen <= 0f) continue;
                    Vector3 n = dist > 1e-8f ? d / dist : Vector3.UnitZ;
                    manifold.Add(new ContactPoint
                    {
                        Position = pb + n * rb,
                        Normal = n,
                        Penetration = pen
                    });
                }
            }
        }

        private void BoneHitboxVsSphere(BoneHitboxShape boxes, PhysicsComponent boxBody,
            SphereShape sphere, PhysicsComponent sphereBody, ContactManifold manifold)
        {
            Vector3 centre = sphereBody.Position + Vector3.Transform(sphere.CenterOffset, sphereBody.Rotation);
            for (int i = 0; i < boxes.Primitives.Length; i++)
            {
                Vector3 closest = ClosestPointOnSegment(centre, boxes.WorldA[i], boxes.WorldB[i]);
                Vector3 d = centre - closest;
                float dist = d.Length();
                float pen = boxes.Primitives[i].Radius + sphere.Radius - dist;
                if (pen <= 0f) continue;
                Vector3 n = dist > 1e-8f ? d / dist : Vector3.UnitZ;
                manifold.Add(new ContactPoint
                {
                    Position = closest + n * boxes.Primitives[i].Radius,
                    Normal = n,
                    Penetration = pen
                });
            }
        }

        private void BoneHitboxVsCapsule(BoneHitboxShape boxes, PhysicsComponent boxBody,
            CapsuleShape cap, PhysicsComponent capBody, ContactManifold manifold)
        {
            float half = MathF.Max(0f, cap.Height * 0.5f - cap.Radius);
            Vector3 c0 = capBody.Position + new Vector3(0f, 0f, cap.Radius);
            Vector3 c1 = capBody.Position + new Vector3(0f, 0f, cap.Height - cap.Radius);
            if (half <= 0f) { c0 = capBody.Position + new Vector3(0f, 0f, cap.Height * 0.5f); c1 = c0; }
            for (int i = 0; i < boxes.Primitives.Length; i++)
            {
                ClosestPointsOnSegments(boxes.WorldA[i], boxes.WorldB[i], c0, c1, out Vector3 pa, out Vector3 pb);
                Vector3 d = pa - pb;
                float dist = d.Length();
                float pen = boxes.Primitives[i].Radius + cap.Radius - dist;
                if (pen <= 0f) continue;
                Vector3 n = dist > 1e-8f ? d / dist : Vector3.UnitZ;
                manifold.Add(new ContactPoint
                {
                    Position = pb + n * cap.Radius,
                    Normal = n,
                    Penetration = pen
                });
            }
        }

        private static void ClosestPointsSegmentTriangle(Vector3 p0, Vector3 p1,
            Vector3 t0, Vector3 t1, Vector3 t2, out Vector3 onSeg, out Vector3 onTri, out float dist)
        {
            onSeg = p0;
            onTri = ClosestPointOnTriangle(p0, t0, t1, t2);
            dist = (p0 - onTri).Length();
            Vector3 c1 = ClosestPointOnTriangle(p1, t0, t1, t2);
            float d1 = (p1 - c1).Length();
            if (d1 < dist) { dist = d1; onSeg = p1; onTri = c1; }
            CheckEdgeEdge(p0, p1, t0, t1, ref onSeg, ref onTri, ref dist);
            CheckEdgeEdge(p0, p1, t1, t2, ref onSeg, ref onTri, ref dist);
            CheckEdgeEdge(p0, p1, t2, t0, ref onSeg, ref onTri, ref dist);
        }

        private static void ClosestPointsOnSegments(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1,
            out Vector3 pa, out Vector3 pb)
        {
            Vector3 da = a1 - a0;
            Vector3 db = b1 - b0;
            Vector3 r = a0 - b0;
            float aa = Vector3.Dot(da, da);
            float ee = Vector3.Dot(db, db);
            float ff = Vector3.Dot(db, r);
            float s, t;
            if (aa <= 1e-12f && ee <= 1e-12f)
            {
                pa = a0; pb = b0; return;
            }
            if (aa <= 1e-12f)
            {
                s = 0f;
                t = Math.Clamp(ff / ee, 0f, 1f);
            }
            else
            {
                float c = Vector3.Dot(da, r);
                if (ee <= 1e-12f)
                {
                    t = 0f;
                    s = Math.Clamp(-c / aa, 0f, 1f);
                }
                else
                {
                    float b = Vector3.Dot(da, db);
                    float denom = aa * ee - b * b;
                    s = denom != 0f ? Math.Clamp((b * ff - c * ee) / denom, 0f, 1f) : 0f;
                    t = (b * s + ff) / ee;
                    if (t < 0f) { t = 0f; s = Math.Clamp(-c / aa, 0f, 1f); }
                    else if (t > 1f) { t = 1f; s = Math.Clamp((b - c) / aa, 0f, 1f); }
                }
            }
            pa = a0 + da * s;
            pb = b0 + db * t;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float denom = ab.LengthSquared();
            if (denom < 1e-12f) return a;
            float t = Math.Clamp(Vector3.Dot(p - a, ab) / denom, 0f, 1f);
            return a + ab * t;
        }

        private void TriangleMeshVsTriangleMesh(
            TriangleMeshShape meshA, PhysicsComponent bodyA,
            TriangleMeshShape meshB, PhysicsComponent bodyB,
            ContactManifold manifold)
        {
            Vector3 comA = bodyA.WorldCentreOfMass;
            Vector3 comB = bodyB.WorldCentreOfMass;
            float rA = meshA.BoundingRadius;
            float rB = meshB.BoundingRadius;
            Vector3 delta = comA - comB;
            float sepSq = delta.LengthSquared();
            float maxSep = rA + rB + 0.08f;
            if (sepSq > maxSep * maxSep)
                return;
            const float MeshContactThreshold = 0.025f;
            const int ClosestCount = 4;
            _triA.Clear();
            _triB.Clear();
            _triC.Clear();
            meshB.QueryClosestWorldTriangles(bodyB.Position, bodyB.Rotation, comA, _triA, _triB, _triC, ClosestCount);
            int countB = _triA.Count;
            if (countB == 0) return;
            Vector3 surfaceB = _triA[0];
            float bestSurface = float.MaxValue;
            for (int t = 0; t < countB; t++)
            {
                Vector3 c = ClosestPointOnTriangle(comA, _triA[t], _triB[t], _triC[t]);
                float d2 = (c - comA).LengthSquared();
                if (d2 < bestSurface)
                {
                    bestSurface = d2;
                    surfaceB = c;
                }
            }
            var triA_A = new List<Vector3>(ClosestCount);
            var triB_A = new List<Vector3>(ClosestCount);
            var triC_A = new List<Vector3>(ClosestCount);
            meshA.QueryClosestWorldTriangles(bodyA.Position, bodyA.Rotation, surfaceB, triA_A, triB_A, triC_A, ClosestCount);
            int countA = triA_A.Count;
            if (countA == 0) return;
            float maxDist = rA + MeshContactThreshold;
            var candidates = new List<ContactPoint>(32);
            for (int ia = 0; ia < countA; ia++)
            {
                Vector3 a0 = triA_A[ia], a1 = triB_A[ia], a2 = triC_A[ia];
                for (int ib = 0; ib < countB; ib++)
                {
                    Vector3 b0 = _triA[ib], b1 = _triB[ib], b2 = _triC[ib];
                    ClosestPointsBetweenTriangles(a0, a1, a2, b0, b1, b2,
                        out Vector3 closestA, out Vector3 closestB, out float dist);
                    if (dist > maxDist)
                        continue;
                    Vector3 nB = Vector3.Cross(b1 - b0, b2 - b0);
                    float nBLen = nB.Length();
                    if (nBLen <= 1e-8f) continue;
                    nB /= nBLen;
                    // Outward from B: B's COM is the interior. +n leaves B.
                    // signedB > 0 → A is outside B. signedB < 0 → A is inside B.
                    if (Vector3.Dot(nB, closestB - comB) < 0f)
                        nB = -nB;
                    float signedB = Vector3.Dot(closestA - closestB, nB);
                    if (signedB >= MeshContactThreshold)
                        continue;
                    float pen = signedB < 0f ? -signedB : 0f;
                    MergeMeshContact(candidates, closestB, nB, pen);
                }
            }
            if (candidates.Count == 0) return;
            candidates.Sort((x, y) => y.Penetration.CompareTo(x.Penetration));
            int keep = Math.Min(4, candidates.Count);
            for (int i = 0; i < keep; i++)
                manifold.Add(candidates[i]);
        }
        private static void MergeMeshContact(List<ContactPoint> candidates, Vector3 position, Vector3 normal, float pen)
        {
            const float mergeDistSq = 0.0001f;
            for (int i = 0; i < candidates.Count; i++)
            {
                if ((candidates[i].Position - position).LengthSquared() < mergeDistSq)
                {
                    if (pen > candidates[i].Penetration)
                    {
                        candidates[i] = new ContactPoint
                        {
                            Position = position,
                            Normal = normal,
                            Penetration = pen
                        };
                    }
                    return;
                }
            }
            candidates.Add(new ContactPoint
            {
                Position = position,
                Normal = normal,
                Penetration = pen
            });
        }
        private static void ClosestPointsBetweenTriangles(
            Vector3 a0, Vector3 a1, Vector3 a2,
            Vector3 b0, Vector3 b1, Vector3 b2,
            out Vector3 closestA, out Vector3 closestB, out float dist)
        {
            closestA = a0;
            closestB = b0;
            dist = float.MaxValue;
            CheckVertexFace(a0, b0, b1, b2, ref closestA, ref closestB, ref dist);
            CheckVertexFace(a1, b0, b1, b2, ref closestA, ref closestB, ref dist);
            CheckVertexFace(a2, b0, b1, b2, ref closestA, ref closestB, ref dist);
            CheckVertexFace(b0, a0, a1, a2, ref closestB, ref closestA, ref dist);
            CheckVertexFace(b1, a0, a1, a2, ref closestB, ref closestA, ref dist);
            CheckVertexFace(b2, a0, a1, a2, ref closestB, ref closestA, ref dist);
            CheckEdgeEdge(a0, a1, b0, b1, ref closestA, ref closestB, ref dist);
            CheckEdgeEdge(a0, a1, b1, b2, ref closestA, ref closestB, ref dist);
            CheckEdgeEdge(a0, a1, b2, b0, ref closestA, ref closestB, ref dist);
            CheckEdgeEdge(a1, a2, b0, b1, ref closestA, ref closestB, ref dist);
            CheckEdgeEdge(a1, a2, b1, b2, ref closestA, ref closestB, ref dist);
            CheckEdgeEdge(a1, a2, b2, b0, ref closestA, ref closestB, ref dist);
            CheckEdgeEdge(a2, a0, b0, b1, ref closestA, ref closestB, ref dist);
            CheckEdgeEdge(a2, a0, b1, b2, ref closestA, ref closestB, ref dist);
            CheckEdgeEdge(a2, a0, b2, b0, ref closestA, ref closestB, ref dist);
        }
        private static void CheckVertexFace(Vector3 v, Vector3 t0, Vector3 t1, Vector3 t2,
            ref Vector3 closestV, ref Vector3 closestT, ref float bestDist)
        {
            Vector3 c = ClosestPointOnTriangle(v, t0, t1, t2);
            float d = (v - c).Length();
            if (d < bestDist)
            {
                bestDist = d;
                closestV = v;
                closestT = c;
            }
        }
        private static void CheckEdgeEdge(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2,
            ref Vector3 closestA, ref Vector3 closestB, ref float bestDist)
        {
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-12f && e <= 1e-12f)
            {
                s = t = 0f;
            }
            else if (a <= 1e-12f)
            {
                s = 0f;
                t = Math.Clamp(f / e, 0f, 1f);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-12f)
                {
                    t = 0f;
                    s = Math.Clamp(-c / a, 0f, 1f);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom != 0f ? Math.Clamp((b * f - c * e) / denom, 0f, 1f) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Math.Clamp(-c / a, 0f, 1f);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Math.Clamp((b - c) / a, 0f, 1f);
                    }
                }
            }
            Vector3 c1 = p1 + d1 * s;
            Vector3 c2 = p2 + d2 * t;
            float d = (c1 - c2).Length();
            if (d < bestDist)
            {
                bestDist = d;
                closestA = c1;
                closestB = c2;
            }
        }
        private void SphereVsHeightfield(SphereShape sphere, PhysicsComponent body,
            HeightfieldShape field, ContactManifold manifold)
        {
            Vector3 centre = body.WorldCentreOfMass;
            float groundZ = field.SampleHeight(centre.X, centre.Y);
            float verticalPen = (groundZ + sphere.Radius) - centre.Z;
            if (verticalPen > -ContactSkin)
            {
                Vector3 n = field.SampleNormal(centre.X, centre.Y);
                float nZ = MathF.Max(n.Z, 0.15f);
                float pen = MathF.Max(0f, verticalPen) / nZ;
                Vector3 contactPos = centre - n * sphere.Radius;
                manifold.Add(new ContactPoint
                {
                    Position = contactPos,
                    Normal = n,
                    Penetration = pen
                });
                float slopeDeg = MathF.Acos(Math.Clamp(n.Z, -1f, 1f)) * (180f / MathF.PI);
                if (slopeDeg <= body.SlopeLimitDegrees)
                {
                    body.IsGrounded = true;
                    if (n.Z >= body.SupportNormal.Z)
                        body.SupportNormal = n;
                }
            }
        }
        private void SphereVsSphere(SphereShape a, PhysicsComponent bodyA,
            SphereShape b, PhysicsComponent bodyB, ContactManifold manifold)
        {
            Vector3 ca = bodyA.WorldCentreOfMass;
            Vector3 cb = bodyB.WorldCentreOfMass;
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
        private void SphereVsCapsule(SphereShape sphere, PhysicsComponent sphereBody,
            CapsuleShape cap, PhysicsComponent capBody, ContactManifold manifold)
        {
            Vector3 sc = sphereBody.WorldCentreOfMass;
            Vector3 feet = capBody.Position;
            float r = cap.Radius;
            float h = cap.Height;
            Vector3 axisStart = feet + new Vector3(0f, 0f, r);
            Vector3 axisEnd = feet + new Vector3(0f, 0f, MathF.Max(r, h - r));
            Vector3 ab = axisEnd - axisStart;
            float abLenSq = ab.LengthSquared();
            float t = abLenSq > 1e-12f
                ? Math.Clamp(Vector3.Dot(sc - axisStart, ab) / abLenSq, 0f, 1f)
                : 0.5f;
            Vector3 closest = axisStart + ab * t;
            Vector3 delta = sc - closest;
            float dist = delta.Length();
            float rSum = sphere.Radius + r;
            if (dist >= rSum) return;
            Vector3 n;
            if (dist > 1e-6f)
            {
                n = delta / dist;
            }
            else
            {
                n = Vector3.UnitZ;
                if (Vector3.Dot(n, ab) < 0f) n = -n;
            }
            float penetration = rSum - dist;
            Vector3 contactPos = closest + n * r;
            if (sphereBody != manifold.BodyA)
            {
                n = -n;
                contactPos = sc + n * sphere.Radius;
            }
            manifold.Add(new ContactPoint
            {
                Position = contactPos,
                Normal = n,
                Penetration = penetration
            });
        }
        private void SphereVsTriangleMesh(SphereShape sphere, PhysicsComponent sphereBody,
            TriangleMeshShape mesh, PhysicsComponent meshBody, ContactManifold manifold)
        {
            Vector3 centre = sphereBody.WorldCentreOfMass;
            float radius = sphere.Radius;
            Vector3 queryMin = centre - new Vector3(radius);
            Vector3 queryMax = centre + new Vector3(radius);
            mesh.GetAabb(meshBody.Position, meshBody.Rotation, out Vector3 aabbMin, out Vector3 aabbMax);
            if (queryMax.X < aabbMin.X || queryMin.X > aabbMax.X ||
                queryMax.Y < aabbMin.Y || queryMin.Y > aabbMax.Y ||
                queryMax.Z < aabbMin.Z || queryMin.Z > aabbMax.Z)
                return;
            _triA.Clear();
            _triB.Clear();
            _triC.Clear();
            mesh.QueryWorldTriangles(meshBody.Position, meshBody.Rotation, queryMin, queryMax, _triA, _triB, _triC);
            for (int t = 0; t < _triA.Count; t++)
            {
                Vector3 closest = ClosestPointOnTriangle(centre, _triA[t], _triB[t], _triC[t]);
                Vector3 delta = centre - closest;
                float dist = delta.Length();
                if (dist < radius && dist > 1e-6f)
                {
                    Vector3 n = delta / dist;
                    if (sphereBody != manifold.BodyA)
                        n = -n;
                    manifold.Add(new ContactPoint
                    {
                        Position = closest,
                        Normal = n,
                        Penetration = radius - dist
                    });
                }
            }
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
            Vector3 queryMin = feet - new Vector3(radius, radius, 0.05f);
            Vector3 queryMax = feet + new Vector3(radius, radius, height + 0.05f);
            mesh.GetAabb(meshBody.Position, meshBody.Rotation, out Vector3 aabbMin, out Vector3 aabbMax);
            if (queryMax.X < aabbMin.X || queryMin.X > aabbMax.X ||
                queryMax.Y < aabbMin.Y || queryMin.Y > aabbMax.Y ||
                queryMax.Z < aabbMin.Z || queryMin.Z > aabbMax.Z)
                return;
            _triA.Clear();
            _triB.Clear();
            _triC.Clear();
            mesh.QueryWorldTriangles(meshBody.Position, meshBody.Rotation, queryMin, queryMax, _triA, _triB, _triC);
            Vector3[] samples =
            {
                feet + new Vector3(0, 0, radius),
                feet + new Vector3(0, 0, height * 0.5f),
                feet + new Vector3(0, 0, height - radius)
            };
            var candidates = new List<ContactPoint>(32);
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
                        candidates.Add(new ContactPoint
                        {
                            Position = closest,
                            Normal = n,
                            Penetration = radius - dist
                        });
                    }
                }
            }
            if (candidates.Count == 0) return;
            candidates.Sort((x, y) => y.Penetration.CompareTo(x.Penetration));
            int keep = Math.Min(4, candidates.Count);
            for (int i = 0; i < keep; i++)
                manifold.Add(candidates[i]);
        }
        private void CapsuleVsHeightfield(CapsuleShape cap, PhysicsComponent body,
            HeightfieldShape field, ContactManifold manifold)
        {
            Vector3 feet = body.Position;
            float groundZ = field.SampleHeight(feet.X, feet.Y);
            float verticalPen = groundZ - feet.Z;
            if (verticalPen > -ContactSkin)
            {
                Vector3 n = field.SampleNormal(feet.X, feet.Y);
                float nZ = MathF.Max(n.Z, 0.15f);
                float pen = MathF.Max(0f, verticalPen) / nZ;
                manifold.Add(new ContactPoint
                {
                    Position = new Vector3(feet.X, feet.Y, groundZ),
                    Normal = n,
                    Penetration = pen
                });
                float slopeDeg = MathF.Acos(Math.Clamp(n.Z, -1f, 1f)) * (180f / MathF.PI);
                if (slopeDeg <= body.SlopeLimitDegrees)
                {
                    body.IsGrounded = true;
                    if (n.Z >= body.SupportNormal.Z)
                        body.SupportNormal = n;
                }
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
                float verticalPen = groundZ - p.Z;
                if (verticalPen > -ContactSkin)
                {
                    Vector3 n = field.SampleNormal(p.X, p.Y);
                    float nZ = MathF.Max(n.Z, 0.15f);
                    float pen = MathF.Max(0f, verticalPen) / nZ;
                    manifold.Add(new ContactPoint
                    {
                        Position = new Vector3(p.X, p.Y, groundZ),
                        Normal = n,
                        Penetration = pen
                    });
                    float slopeDeg = MathF.Acos(Math.Clamp(n.Z, -1f, 1f)) * (180f / MathF.PI);
                    if (slopeDeg <= body.SlopeLimitDegrees)
                    {
                        body.IsGrounded = true;
                        if (n.Z >= body.SupportNormal.Z)
                            body.SupportNormal = n;
                    }
                }
            }
        }
        private void TriangleMeshVsHeightfield(TriangleMeshShape mesh, PhysicsComponent body,
            HeightfieldShape field, ContactManifold manifold)
        {
            mesh.GetAabb(body.Position, body.Rotation, out Vector3 aabbMin, out Vector3 aabbMax);
            float band = ContactSkin + 0.5f;
            Vector3 queryMin = new Vector3(aabbMin.X, aabbMin.Y, aabbMin.Z - band);
            Vector3 queryMax = new Vector3(aabbMax.X, aabbMax.Y, aabbMax.Z + band);
            _triA.Clear();
            _triB.Clear();
            _triC.Clear();
            mesh.QueryWorldTriangles(body.Position, body.Rotation, queryMin, queryMax, _triA, _triB, _triC);
            var candidates = new List<ContactPoint>(16);
            for (int t = 0; t < _triA.Count; t++)
            {
                Vector3[] verts = { _triA[t], _triB[t], _triC[t] };
                for (int v = 0; v < 3; v++)
                {
                    Vector3 p = verts[v];
                    float groundZ = field.SampleHeight(p.X, p.Y);
                    float verticalPen = groundZ - p.Z;
                    if (verticalPen > -ContactSkin)
                    {
                        Vector3 n = field.SampleNormal(p.X, p.Y);
                        float nZ = MathF.Max(n.Z, 0.15f);
                        float pen = MathF.Max(0f, verticalPen) / nZ;
                        candidates.Add(new ContactPoint
                        {
                            Position = new Vector3(p.X, p.Y, groundZ),
                            Normal = n,
                            Penetration = pen
                        });
                    }
                }
            }
            if (candidates.Count == 0) return;
            candidates.Sort((x, y) => y.Penetration.CompareTo(x.Penetration));
            int keep = Math.Min(4, candidates.Count);
            for (int i = 0; i < keep; i++)
                manifold.Add(candidates[i]);
            if (manifold.PointCount > 0)
            {
                float slopeDeg = MathF.Acos(Math.Clamp(manifold.Points[0].Normal.Z, -1f, 1f)) * (180f / MathF.PI);
                if (slopeDeg <= body.SlopeLimitDegrees)
                {
                    body.IsGrounded = true;
                    body.SupportNormal = manifold.Points[0].Normal;
                }
            }
        }
        private void TriangleMeshPlayerVsHeightfield(TriangleMeshShape mesh, PhysicsComponent body,
            HeightfieldShape field, ContactManifold manifold)
        {
            float minZ = 0f;
            if (body.LocalBoundsMinCm.X <= body.LocalBoundsMaxCm.X)
                minZ = body.LocalBoundsMinCm.Z;
            Vector3 feet = new Vector3(body.Position.X, body.Position.Y, body.Position.Z + minZ);
            float groundAtFeet = field.SampleHeight(feet.X, feet.Y);
            float verticalPen = groundAtFeet - feet.Z;
            if (verticalPen > -ContactSkin)
            {
                Vector3 n = field.SampleNormal(feet.X, feet.Y);
                float nZ = MathF.Max(n.Z, 0.15f);
                float pen = MathF.Max(0f, verticalPen) / nZ;
                manifold.Add(new ContactPoint
                {
                    Position = new Vector3(feet.X, feet.Y, groundAtFeet),
                    Normal = n,
                    Penetration = pen
                });
                float slopeDeg = MathF.Acos(Math.Clamp(n.Z, -1f, 1f)) * (180f / MathF.PI);
                if (slopeDeg <= body.SlopeLimitDegrees)
                {
                    body.IsGrounded = true;
                    body.SupportNormal = n;
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