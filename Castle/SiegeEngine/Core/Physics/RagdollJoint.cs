// Folder: SiegeEngine/Core/Physics
// File: RagdollJoint.cs
using System.Numerics;

namespace SiegeEngine.Core.Physics
{
    /// <summary>
    /// Parent-child ragdoll joint, fitted in rest pose. Stored and serialized
    /// with the hitbox compound. The solver does not apply this joint unless
    /// PhysicsComponent.RagdollSimulationEnabled is true.
    /// </summary>
    public struct RagdollJoint
    {
        public int ParentBone;
        public int ChildBone;
        public Vector3 ParentLocalAnchor;
        public Vector3 ChildLocalAnchor;
        public bool Enabled;
    }
}
