// Folder: SiegeEngine/Core/Physics
// File: BoneHitboxShape.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.AssetParsing.Model;

namespace SiegeEngine.Core.Physics
{
    /// <summary>
    /// Per-bone capsules/spheres fitted to mesh vertices (the same space the
    /// vertex shader uses as aPosition). Posed by the same palette the mesh
    /// viewer uploads: BindPose * currentGlobal, i.e. ModelComponent.BoneMatrices
    /// after WriteSkinning / ModelViewer SetRestPose. No animation clip required.
    /// </summary>
    public sealed class BoneHitboxShape : ColliderShape
    {
        public enum Kind { Sphere, Capsule }

        public struct Primitive
        {
            public Kind Type;
            public int BoneIndex;
            public int ChildIndex;
            public float Radius;
        }

        public Primitive[] Primitives { get; }
        public Vector3[] WorldA { get; }
        public Vector3[] WorldB { get; }
        public RagdollJoint[] Joints { get; }
        public float BoundingRadius { get; private set; }
        public Vector3 LocalCentreOfMass { get; private set; }

        // Endpoints in mesh/file units — identical to FBXVertex.Position.
        private readonly Vector3[] _meshA;
        private readonly Vector3[] _meshB;
        private readonly float _toMeters;

        public BoneHitboxShape(FBXModel model)
        {
            var prims = new List<Primitive>(32);
            var meshA = new List<Vector3>(32);
            var meshB = new List<Vector3>(32);
            var joints = new List<RagdollJoint>(32);

            if (model == null || model.Skeleton == null || model.Skeleton.Bones == null
                || model.Skeleton.Bones.Count == 0)
            {
                Primitives = Array.Empty<Primitive>();
                WorldA = Array.Empty<Vector3>();
                WorldB = Array.Empty<Vector3>();
                Joints = Array.Empty<RagdollJoint>();
                _meshA = Array.Empty<Vector3>();
                _meshB = Array.Empty<Vector3>();
                _toMeters = 1f;
                BoundingRadius = 0.5f;
                return;
            }

            var skeleton = model.Skeleton;
            int boneCount = skeleton.Bones.Count;
            _toMeters = model.UnitToMeters > 1e-8f ? model.UnitToMeters : 1f;

            Matrix4x4[] restGlobals = skeleton.ComputeGlobalTransforms();
            var boneFile = new Vector3[boneCount];
            for (int i = 0; i < boneCount; i++)
                boneFile[i] = restGlobals[i].Translation;

            GatherMeshClusters(model, boneCount, out Vector3[] centroidFile, out int[] vertCounts);
            var endpoints = new Vector3[boneCount];
            for (int i = 0; i < boneCount; i++)
                endpoints[i] = centroidFile[i];

            int[] childCounts = CountChildren(skeleton);
            float[] radii = MeasureMeshRadii(model, endpoints, vertCounts);
            const float minLenM = 0.02f;

            for (int i = 0; i < boneCount; i++)
            {
                if (vertCounts[i] <= 0)
                    continue;
                int child = PickPrimaryChild(skeleton, i, endpoints);
                if (child >= 0 && vertCounts[child] <= 0)
                    child = -1;
                if (child >= 0)
                {
                    Vector3 a = endpoints[i];
                    Vector3 b = endpoints[child];
                    float lenM = (b - a).Length() * _toMeters;
                    if (lenM < minLenM)
                        continue;
                    float r = radii[i];
                    if (r < 1e-4f) r = radii[child];
                    if (r < 1e-4f)
                        continue;
                    prims.Add(new Primitive
                    {
                        Type = Kind.Capsule,
                        BoneIndex = i,
                        ChildIndex = child,
                        Radius = r
                    });
                    meshA.Add(a);
                    meshB.Add(b);
                }
                else if (childCounts[i] == 0)
                {
                    float r = radii[i];
                    if (r < 1e-4f)
                        continue;
                    Vector3 p = endpoints[i];
                    prims.Add(new Primitive
                    {
                        Type = Kind.Sphere,
                        BoneIndex = i,
                        ChildIndex = -1,
                        Radius = r
                    });
                    meshA.Add(p);
                    meshB.Add(p);
                }
            }

            for (int i = 0; i < boneCount; i++)
            {
                int parent = skeleton.Bones[i].ParentIndex;
                if (parent >= 0 && parent < boneCount)
                    joints.Add(MakeJoint(parent, i, boneFile, restGlobals));
            }

            Primitives = prims.ToArray();
            _meshA = meshA.ToArray();
            _meshB = meshB.ToArray();
            WorldA = new Vector3[_meshA.Length];
            WorldB = new Vector3[_meshB.Length];
            Joints = joints.ToArray();

            // File verts, unskinned — same as the mesh when BoneMatrices is null.
            Pose(Vector3.Zero, Quaternion.Identity, null);

            Vector3 com = Vector3.Zero;
            float maxR = 0.1f;
            if (Primitives.Length > 0)
            {
                for (int i = 0; i < Primitives.Length; i++)
                    com += (WorldA[i] + WorldB[i]) * 0.5f;
                com /= Primitives.Length;
                for (int i = 0; i < Primitives.Length; i++)
                {
                    maxR = MathF.Max(maxR, (WorldA[i] - com).Length() + Primitives[i].Radius);
                    maxR = MathF.Max(maxR, (WorldB[i] - com).Length() + Primitives[i].Radius);
                }
            }
            LocalCentreOfMass = com;
            BoundingRadius = maxR;
        }

        /// <summary>
        /// Exact matrix ModelViewer.SetRestPose / WriteSkinning uploads:
        /// BindPose (inverse-bind) * current global. No clip sampling.
        /// </summary>
        public static Matrix4x4[] BuildRestSkin(FBXModel model)
        {
            if (model?.Skeleton?.Bones == null || model.Skeleton.Bones.Count == 0)
                return null;
            int n = model.Skeleton.Bones.Count;
            Matrix4x4[] globals = model.Skeleton.ComputeGlobalTransforms();
            var skin = new Matrix4x4[n];
            for (int i = 0; i < n; i++)
                skin[i] = model.Skeleton.Bones[i].BindPose * globals[i];
            return skin;
        }

        public void Pose(Vector3 bodyPosition, Quaternion bodyRotation, Matrix4x4[] skinMatrices)
        {
            Matrix4x4 body = Matrix4x4.CreateFromQuaternion(bodyRotation);
            body.Translation = bodyPosition;
            Matrix4x4 model = Matrix4x4.CreateScale(_toMeters) * body;
            Pose(model, skinMatrices);
        }

        /// <summary>
        /// Same multiply as ModelRenderer: world = modelMatrix * (BoneMatrix * aPosition).
        /// modelMatrix is physics.BuildRenderModelMatrix(UnitToMeters).
        /// skinMatrices is ModelComponent.BoneMatrices. Null keeps file verts (unskinned mesh).
        /// </summary>
        public void Pose(Matrix4x4 modelMatrix, Matrix4x4[] skinMatrices)
        {
            bool hasSkin = skinMatrices != null && skinMatrices.Length > 0;
            for (int i = 0; i < Primitives.Length; i++)
            {
                Vector3 a = _meshA[i];
                Vector3 b = _meshB[i];
                if (hasSkin)
                {
                    int bone = Primitives[i].BoneIndex;
                    int child = Primitives[i].ChildIndex;
                    if (bone >= 0 && bone < skinMatrices.Length)
                        a = Vector3.Transform(_meshA[i], skinMatrices[bone]);
                    if (child >= 0 && child < skinMatrices.Length)
                        b = Vector3.Transform(_meshB[i], skinMatrices[child]);
                    else if (Primitives[i].Type == Kind.Sphere)
                        b = a;
                }
                WorldA[i] = Vector3.Transform(a, modelMatrix);
                WorldB[i] = Vector3.Transform(b, modelMatrix);
            }
        }

        public Vector3 LowestWorldPoint()
        {
            Vector3 best = Vector3.Zero;
            float z = float.MaxValue;
            for (int i = 0; i < Primitives.Length; i++)
            {
                float za = WorldA[i].Z - Primitives[i].Radius;
                float zb = WorldB[i].Z - Primitives[i].Radius;
                if (za < z) { z = za; best = new Vector3(WorldA[i].X, WorldA[i].Y, za); }
                if (zb < z) { z = zb; best = new Vector3(WorldB[i].X, WorldB[i].Y, zb); }
            }
            return z == float.MaxValue ? Vector3.Zero : best;
        }

        public override void GetAabb(in Vector3 position, in Quaternion rotation, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            if (Primitives.Length == 0)
            {
                min = position - new Vector3(BoundingRadius);
                max = position + new Vector3(BoundingRadius);
                return;
            }
            for (int i = 0; i < Primitives.Length; i++)
            {
                float r = Primitives[i].Radius;
                min = Vector3.Min(min, Vector3.Min(WorldA[i], WorldB[i]) - new Vector3(r));
                max = Vector3.Max(max, Vector3.Max(WorldA[i], WorldB[i]) + new Vector3(r));
            }
        }

        public override bool Raycast(in Vector3 position, in Quaternion rotation,
            in Vector3 origin, in Vector3 direction, float maxDistance,
            out float distance, out Vector3 normal)
        {
            distance = maxDistance;
            normal = Vector3.UnitZ;
            bool hit = false;
            Vector3 dir = direction;
            float dlen = dir.Length();
            if (dlen < 1e-8f) return false;
            dir /= dlen;
            for (int i = 0; i < Primitives.Length; i++)
            {
                if (RayVsCapsule(origin, dir, maxDistance, WorldA[i], WorldB[i], Primitives[i].Radius,
                    out float t, out Vector3 n) && t < distance)
                {
                    distance = t;
                    normal = n;
                    hit = true;
                }
            }
            return hit;
        }

        private static RagdollJoint MakeJoint(int parent, int child, Vector3[] boneFile,
            Matrix4x4[] restGlobals)
        {
            Vector3 parentWorld = boneFile[parent];
            Vector3 childWorld = boneFile[child];
            Matrix4x4.Invert(restGlobals[parent], out Matrix4x4 invParent);
            Matrix4x4.Invert(restGlobals[child], out Matrix4x4 invChild);
            Vector3 parentLocal = Vector3.Transform(parentWorld, invParent);
            Vector3 childLocal = Vector3.Transform(childWorld, invChild);
            return new RagdollJoint
            {
                ParentBone = parent,
                ChildBone = child,
                ParentLocalAnchor = parentLocal,
                ChildLocalAnchor = childLocal,
                Enabled = false
            };
        }

        private static int[] CountChildren(Skeleton skeleton)
        {
            int n = skeleton.Bones.Count;
            var counts = new int[n];
            for (int i = 0; i < n; i++)
            {
                int p = skeleton.Bones[i].ParentIndex;
                if (p >= 0 && p < n)
                    counts[p]++;
            }
            return counts;
        }

        private static int PickPrimaryChild(Skeleton skeleton, int boneIndex, Vector3[] positions)
        {
            int n = skeleton.Bones.Count;
            int best = -1;
            float bestLen = -1f;
            for (int i = 0; i < n; i++)
            {
                if (skeleton.Bones[i].ParentIndex != boneIndex)
                    continue;
                float len = (positions[i] - positions[boneIndex]).LengthSquared();
                if (len > bestLen)
                {
                    bestLen = len;
                    best = i;
                }
            }
            return best;
        }

        private static void GatherMeshClusters(FBXModel model, int boneCount,
            out Vector3[] centroidFile, out int[] counts)
        {
            centroidFile = new Vector3[boneCount];
            counts = new int[boneCount];
            if (model.Meshes == null) return;
            for (int m = 0; m < model.Meshes.Count; m++)
            {
                var mesh = model.Meshes[m];
                if (mesh.Vertices == null) continue;
                for (int v = 0; v < mesh.Vertices.Count; v++)
                {
                    var vert = mesh.Vertices[v];
                    int b = DominantBone(vert);
                    if (b < 0 || b >= boneCount) continue;
                    centroidFile[b] += vert.Position;
                    counts[b]++;
                }
            }
            for (int i = 0; i < boneCount; i++)
            {
                if (counts[i] > 0)
                    centroidFile[i] /= counts[i];
            }
        }

        private static float[] MeasureMeshRadii(FBXModel model, Vector3[] endpointsFile, int[] vertCounts)
        {
            int boneCount = model.Skeleton.Bones.Count;
            var minF = new Vector3[boneCount];
            var maxF = new Vector3[boneCount];
            var cnt = new int[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                minF[i] = new Vector3(float.MaxValue);
                maxF[i] = new Vector3(float.MinValue);
            }
            float toMeters = model.UnitToMeters > 1e-8f ? model.UnitToMeters : 1f;
            if (model.Meshes == null) return new float[boneCount];
            for (int m = 0; m < model.Meshes.Count; m++)
            {
                var mesh = model.Meshes[m];
                if (mesh.Vertices == null) continue;
                for (int v = 0; v < mesh.Vertices.Count; v++)
                {
                    var vert = mesh.Vertices[v];
                    int b = DominantBone(vert);
                    if (b < 0 || b >= boneCount) continue;
                    minF[b] = Vector3.Min(minF[b], vert.Position);
                    maxF[b] = Vector3.Max(maxF[b], vert.Position);
                    cnt[b]++;
                }
            }
            var radius = new float[boneCount];
            var skeleton = model.Skeleton;
            for (int i = 0; i < boneCount; i++)
            {
                if (cnt[i] == 0) continue;
                Vector3 mn = minF[i];
                Vector3 mx = maxF[i];
                Vector3 center = (mn + mx) * 0.5f;
                int child = PickPrimaryChild(skeleton, i, endpointsFile);
                Vector3 axis = Vector3.UnitZ;
                if (child >= 0)
                {
                    Vector3 delta = endpointsFile[child] - endpointsFile[i];
                    if (delta.LengthSquared() > 1e-10f)
                        axis = Vector3.Normalize(delta);
                }
                float r = 0f;
                for (int cx = 0; cx < 2; cx++)
                for (int cy = 0; cy < 2; cy++)
                for (int cz = 0; cz < 2; cz++)
                {
                    Vector3 corner = new Vector3(
                        cx == 0 ? mn.X : mx.X,
                        cy == 0 ? mn.Y : mx.Y,
                        cz == 0 ? mn.Z : mx.Z);
                    Vector3 d = corner - center;
                    Vector3 perp = d - axis * Vector3.Dot(d, axis);
                    r = MathF.Max(r, perp.Length());
                }
                if (r < 1e-5f)
                {
                    Vector3 half = (mx - mn) * 0.5f;
                    r = MathF.Max(MathF.Max(MathF.Abs(half.X), MathF.Abs(half.Y)), MathF.Abs(half.Z));
                }
                radius[i] = r * toMeters;
            }
            return radius;
        }

        private static int DominantBone(FBXVertex vert)
        {
            int best = vert.BoneID0;
            float w = vert.Weights.X;
            if (vert.Weights.Y > w) { w = vert.Weights.Y; best = vert.BoneID1; }
            if (vert.Weights.Z > w) { w = vert.Weights.Z; best = vert.BoneID2; }
            if (vert.Weights.W > w) { w = vert.Weights.W; best = vert.BoneID3; }
            return w > 1e-5f ? best : -1;
        }

        private static bool RayVsCapsule(Vector3 origin, Vector3 dir, float maxDist,
            Vector3 a, Vector3 b, float radius, out float t, out Vector3 normal)
        {
            t = maxDist;
            normal = Vector3.UnitZ;
            Vector3 ab = b - a;
            float abLenSq = ab.LengthSquared();
            Vector3 ao = origin - a;
            float aba = abLenSq > 1e-8f ? Vector3.Dot(ab, ao) / abLenSq : 0f;
            Vector3 center = a + ab * Math.Clamp(aba, 0f, 1f);
            Vector3 oc = origin - center;
            float bDot = Vector3.Dot(oc, dir);
            float c = Vector3.Dot(oc, oc) - radius * radius;
            float disc = bDot * bDot - c;
            if (disc < 0f) return false;
            float hit = -bDot - MathF.Sqrt(disc);
            if (hit < 0f || hit > maxDist) return false;
            t = hit;
            Vector3 n = origin + dir * t - center;
            float nLen = n.Length();
            normal = nLen > 1e-8f ? n / nLen : -dir;
            return true;
        }
    }
}
