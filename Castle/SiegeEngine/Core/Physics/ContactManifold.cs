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
    }

    public class ContactManifold
    {
        public PhysicsComponent BodyA;
        public PhysicsComponent BodyB;
        public ContactPoint[] Points = new ContactPoint[4];
        public int PointCount;

        public void Add(in ContactPoint p)
        {
            if (PointCount >= 4) return;
            Points[PointCount++] = p;
        }

        public void Clear() => PointCount = 0;
    }
}