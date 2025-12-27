using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing.Model
{
    public class FBXModel
    {
        public List<MeshData> Meshes { get; set; } = new List<MeshData>();
        public Skeleton Skeleton { get; set; } = new Skeleton();
        public List<Animation> Animations { get; set; } = new List<Animation>();
        public int[] SourceToTarget { get; set; }
        public int[] Signs { get; set; }
        public float ModelScale { get; set; }
        public Matrix4x4 P4 { get; set; }
        public Matrix4x4 InvP4 { get; set; }
        public bool ReverseWinding { get; set; }
        public bool HasSkin { get; set; } = false;

        public FBXModel()
        {
        }

        public bool HasUnweightedVertices()
        {
            return Meshes.Any(m => m.Vertices.Any(v => v.Weight0 + v.Weight1 + v.Weight2 + v.Weight3 == 0));
        }

        public void CopyWeightsFrom(FBXModel other)
        {
            if (Meshes.Count != 1 || other.Meshes.Count != 1)
            {
                Console.WriteLine("Mesh count mismatch for weight copy, using heuristic");
                AssignToClosestBone(Meshes[0]);
                HasSkin = true;
                return;
            }

            var mainMesh = Meshes[0];
            var otherMesh = other.Meshes[0];

            if (mainMesh.Vertices.Count != otherMesh.Vertices.Count)
            {
                Console.WriteLine("Vertex count mismatch for weight copy, using heuristic");
                if (mainMesh.Vertices.Count > otherMesh.Vertices.Count)
                    AssignToRootBone(mainMesh);
                else
                    AssignToClosestBone(mainMesh);
                HasSkin = true;
                return;
            }

            for (int i = 0; i < mainMesh.Vertices.Count; i++)
            {
                mainMesh.Vertices[i].BoneID0 = otherMesh.Vertices[i].BoneID0;
                mainMesh.Vertices[i].BoneID1 = otherMesh.Vertices[i].BoneID1;
                mainMesh.Vertices[i].BoneID2 = otherMesh.Vertices[i].BoneID2;
                mainMesh.Vertices[i].BoneID3 = otherMesh.Vertices[i].BoneID3;
                mainMesh.Vertices[i].Weight0 = otherMesh.Vertices[i].Weight0;
                mainMesh.Vertices[i].Weight1 = otherMesh.Vertices[i].Weight1;
                mainMesh.Vertices[i].Weight2 = otherMesh.Vertices[i].Weight2;
                mainMesh.Vertices[i].Weight3 = otherMesh.Vertices[i].Weight3;
            }
            HasSkin = true;
        }

        private void AssignToRootBone(MeshData mainMesh)
        {
            int rootIdx = Skeleton.Bones.FindIndex(b => b.ParentIndex == -1);
            if (rootIdx == -1) return;
            for (int i = 0; i < mainMesh.Vertices.Count; i++)
            {
                mainMesh.Vertices[i].BoneID0 = rootIdx;
                mainMesh.Vertices[i].Weight0 = 1f;
                mainMesh.Vertices[i].BoneID1 = -1;
                mainMesh.Vertices[i].Weight1 = 0f;
                mainMesh.Vertices[i].BoneID2 = -1;
                mainMesh.Vertices[i].Weight2 = 0f;
                mainMesh.Vertices[i].BoneID3 = -1;
                mainMesh.Vertices[i].Weight3 = 0f;
            }
            Console.WriteLine("Assigned all vertices to root bone");
        }

        private void AssignToClosestBone(MeshData mainMesh)
        {
            if (Skeleton.Bones.Count == 0) return;
            var restLocals = Skeleton.Bones.Select(b => b.LocalRest).ToArray();
            var globals = Skeleton.ComputeGlobalTransforms(restLocals);
            var bonePos = globals.Select(g => g.Translation).ToArray();
            for (int i = 0; i < mainMesh.Vertices.Count; i++)
            {
                var v = mainMesh.Vertices[i];
                Vector3 vpos = new Vector3(v.X, v.Y, v.Z);
                int closest = -1;
                float minDist = float.MaxValue;
                for (int j = 0; j < bonePos.Length; j++)
                {
                    float dist = (vpos - bonePos[j]).LengthSquared();
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = j;
                    }
                }
                if (closest != -1)
                {
                    v.BoneID0 = closest;
                    v.Weight0 = 1f;
                    v.BoneID1 = -1;
                    v.Weight1 = 0f;
                    v.BoneID2 = -1;
                    v.Weight2 = 0f;
                    v.BoneID3 = -1;
                    v.Weight3 = 0f;
                    mainMesh.Vertices[i] = v;
                }
            }
            Console.WriteLine("Assigned vertices to closest bones");
        }

        public void ComputeBindPoses()
        {
            if (Skeleton == null || Skeleton.Bones.Count == 0) return;
            var restLocals = Skeleton.Bones.Select(b => b.LocalRest).ToArray();
            var restGlobals = Skeleton.ComputeGlobalTransforms(restLocals);
            for (int i = 0; i < Skeleton.Bones.Count; i++)
            {
                if (!Matrix4x4.Invert(restGlobals[i], out Matrix4x4 invRestGlobal))
                {
                    Console.WriteLine($"Failed to invert rest global for bone {i} ({Skeleton.Bones[i].Name})");
                    Skeleton.Bones[i].BindPose = Matrix4x4.Identity;
                    continue;
                }
                Skeleton.Bones[i].BindPose = invRestGlobal;
            }
            Console.WriteLine("Computed bind poses for skeleton");
        }
    }
}