// Folder: SiegeEngine/Core/Physics
// File: ColliderShape.cs
using System.Numerics;

namespace SiegeEngine.Core.Physics
{
    /// <summary>
    /// Abstract collision shape. All concrete shapes live in local space relative to
    /// the owning PhysicsComponent's Position / Rotation. World-space queries transform
    /// on the fly so Static meshes never need to be re-baked when the body is moved
    /// in the editor.
    /// </summary>
    public abstract class ColliderShape
    {
        public abstract void GetAabb(in Vector3 position, in Quaternion rotation, out Vector3 min, out Vector3 max);

        public abstract bool Raycast(in Vector3 position, in Quaternion rotation,
            in Vector3 origin, in Vector3 direction, float maxDistance,
            out float distance, out Vector3 normal);
    }
}