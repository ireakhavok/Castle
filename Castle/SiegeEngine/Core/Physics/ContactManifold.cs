// Folder: SiegeEngine/Core/Physics
// File: ContactManifold.cs
using SiegeEngine.Core.Definitions;
using System.Numerics;

namespace SiegeEngine.Core.Physics
{
    public struct ContactPoint
    {
        public Vector3 Position;
        public Vector3 Normal;      // points from bodyB toward bodyA
        public float Penetration;
        public float Friction;
        public float Restitution;
    }

    public class ContactManifold
    {
        public PhysicsComponent BodyA;
        public PhysicsComponent BodyB;
        public ContactPoint[] Points = new ContactPoint[8];
        public int PointCount;

        public void Add(in ContactPoint p)
        {
            if (PointCount >= Points.Length) return;
            Points[PointCount++] = p;
        }

        public void Clear() => PointCount = 0;
    }
}