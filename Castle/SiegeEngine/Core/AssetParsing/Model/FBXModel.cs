// Folder: SiegeEngine.Core
// File: AssetParsing/FBXModel.cs
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
        public Skeleton Skeleton { get; set; }
        public List<Animation> Animations { get; set; }
        public Entity Entity { get; set; }
        public List<Material> Materials { get; set; }
        public bool HasSkin { get; set; }
        public FBXModel()
        {
            Skeleton = new Skeleton { Bones = new List<Bone>() };
            Animations = new List<Animation>();
            Materials = new List<Material>();
        }
        public bool HasUnweightedVertices()
        {
            return Meshes.Any(m => m.Vertices.Any(v => v.Weight0 == 0 && v.Weight1 == 0 && v.Weight2 == 0 && v.Weight3 == 0));
        }
        public void CopyWeightsFrom(FBXModel other)
        {
            // Create bone name to index maps for both models
            var mainBoneMap = new Dictionary<string, int>();
            for (int i = 0; i < Skeleton.Bones.Count; i++)
            {
                mainBoneMap[Skeleton.Bones[i].Name.ToLowerInvariant()] = i;
            }
            var otherBoneMap = new Dictionary<string, int>();
            for (int i = 0; i < other.Skeleton.Bones.Count; i++)
            {
                otherBoneMap[other.Skeleton.Bones[i].Name.ToLowerInvariant()] = i;
            }

            int totalCopied = 0;
            const float epsilon = 1e-5f;
            foreach (var mainMesh in Meshes)
            {
                MeshData matchingOtherMesh = null;
                List<MeshData> candidates = other.Meshes.Where(om => om.Vertices.Count == mainMesh.Vertices.Count).ToList();
                if (candidates.Count == 0)
                {
                    Console.WriteLine($"CopyWeightsFrom: No matching mesh by vertex count ({mainMesh.Vertices.Count}), assigning to closest bone");
                    AssignToClosestBone(mainMesh);
                    continue;
                }
                foreach (var candidate in candidates)
                {
                    bool positionsMatch = true;
                    //Sample first, middle, last vertices
                    int[] sampleIndices = { 0, mainMesh.Vertices.Count / 2, mainMesh.Vertices.Count - 1 };
                    foreach (int vi in sampleIndices)
                    {
                        var mv = mainMesh.Vertices[vi];
                        var ov = candidate.Vertices[vi];
                        if (Math.Abs(mv.X - ov.X) > epsilon || Math.Abs(mv.Y - ov.Y) > epsilon || Math.Abs(mv.Z - ov.Z) > epsilon)
                        {
                            positionsMatch = false;
                            break;
                        }
                    }
                    if (positionsMatch)
                    {
                        matchingOtherMesh = candidate;
                        break;
                    }
                }
                if (matchingOtherMesh == null)
                {
                    Console.WriteLine($"CopyWeightsFrom: No position-matching mesh found for mesh with {mainMesh.Vertices.Count} vertices, assigning to closest bone");
                    AssignToClosestBone(mainMesh);
                    continue;
                }

                int meshCopied = 0;
                for (int vi = 0; vi < mainMesh.Vertices.Count; vi++)
                {
                    var mv = mainMesh.Vertices[vi];
                    var ov = matchingOtherMesh.Vertices[vi];
                    // If main vertex unweighted and other weighted, copy with remapped bone IDs
                    if (mv.Weight0 == 0 && mv.Weight1 == 0 && mv.Weight2 == 0 && mv.Weight3 == 0 &&
                        (ov.Weight0 > 0 || ov.Weight1 > 0 || ov.Weight2 > 0 || ov.Weight3 > 0))
                    {
                        int[] otherBoneIDs = new int[] { ov.BoneID0, ov.BoneID1, ov.BoneID2, ov.BoneID3 };
                        float[] weights = new float[] { ov.Weight0, ov.Weight1, ov.Weight2, ov.Weight3 };
                        int validCount = 0;
                        for (int wi = 0; wi < 4; wi++)
                        {
                            if (otherBoneIDs[wi] >= 0 && weights[wi] > 0)
                            {
                                string otherBoneName = other.Skeleton.Bones[otherBoneIDs[wi]].Name.ToLowerInvariant();
                                if (mainBoneMap.TryGetValue(otherBoneName, out int mainBoneIdx))
                                {
                                    // Assign to main
                                    switch (validCount)
                                    {
                                        case 0:
                                            mv.BoneID0 = mainBoneIdx;
                                            mv.Weight0 = weights[wi];
                                            break;
                                        case 1:
                                            mv.BoneID1 = mainBoneIdx;
                                            mv.Weight1 = weights[wi];
                                            break;
                                        case 2:
                                            mv.BoneID2 = mainBoneIdx;
                                            mv.Weight2 = weights[wi];
                                            break;
                                        case 3:
                                            mv.BoneID3 = mainBoneIdx;
                                            mv.Weight3 = weights[wi];
                                            break;
                                    }
                                    validCount++;
                                }
                                else
                                {
                                    Console.WriteLine($"CopyWeightsFrom: Bone {otherBoneName} from other model not found in main model, skipping weight");
                                }
                            }
                        }
                        // Set unused slots to -1 and 0 weight
                        if (validCount < 4)
                        {
                            if (validCount <= 0) mv.BoneID0 = -1; mv.Weight0 = 0f;
                            if (validCount <= 1) mv.BoneID1 = -1; mv.Weight1 = 0f;
                            if (validCount <= 2) mv.BoneID2 = -1; mv.Weight2 = 0f;
                            if (validCount <= 3) mv.BoneID3 = -1; mv.Weight3 = 0f;
                        }
                        // Normalize weights
                        float sumW = mv.Weight0 + mv.Weight1 + mv.Weight2 + mv.Weight3;
                        if (sumW > 0)
                        {
                            mv.Weight0 /= sumW;
                            mv.Weight1 /= sumW;
                            mv.Weight2 /= sumW;
                            mv.Weight3 /= sumW;
                        }
                        mainMesh.Vertices[vi] = mv;
                        meshCopied++;
                    }
                }
                totalCopied += meshCopied;
                Console.WriteLine($"CopyWeightsFrom: Copied and remapped weights for {meshCopied} vertices in mesh with {mainMesh.Vertices.Count} vertices");
            }
            // For any remaining unweighted meshes/vertices, assign to root bone (index 0, assuming 0 is root)
            foreach (var mainMesh in Meshes)
            {
                if (mainMesh.Vertices.Any(v => v.Weight0 == 0 && v.Weight1 == 0 && v.Weight2 == 0 && v.Weight3 == 0))
                {
                    AssignToRootBone(mainMesh);
                }
            }
            if (totalCopied > 0 || other.Meshes.Count == 0)
            {
                HasSkin = true;
            }
            Console.WriteLine($"CopyWeightsFrom: Total copied vertices: {totalCopied}");
        }
        private void AssignToRootBone(MeshData mainMesh)
        {
            int rootBone = 0; // Assuming bone 0 is root
            int assigned = 0;
            for (int vi = 0; vi < mainMesh.Vertices.Count; vi++)
            {
                var mv = mainMesh.Vertices[vi];
                if (mv.Weight0 == 0 && mv.Weight1 == 0 && mv.Weight2 == 0 && mv.Weight3 == 0)
                {
                    mv.BoneID0 = rootBone;
                    mv.Weight0 = 1f;
                    mv.BoneID1 = -1;
                    mv.BoneID2 = -1;
                    mv.BoneID3 = -1;
                    mainMesh.Vertices[vi] = mv;
                    assigned++;
                }
            }
            Console.WriteLine($"Assigned {assigned} unweighted vertices to root bone");
        }
        private void AssignToClosestBone(MeshData mainMesh)
        {
            // Get bone positions from LocalRest translation
            var bonePositions = new Vector3[Skeleton.Bones.Count];
            for (int bi = 0; bi < Skeleton.Bones.Count; bi++)
            {
                bonePositions[bi] = Skeleton.Bones[bi].LocalRest.Translation;
            }
            int assigned = 0;
            for (int vi = 0; vi < mainMesh.Vertices.Count; vi++)
            {
                var mv = mainMesh.Vertices[vi];
                if (mv.Weight0 == 0 && mv.Weight1 == 0 && mv.Weight2 == 0 && mv.Weight3 == 0)
                {
                    Vector3 vertPos = new Vector3(mv.X, mv.Y, mv.Z);
                    int closestBone = -1;
                    float minDistSq = float.MaxValue;
                    for (int bi = 0; bi < bonePositions.Length; bi++)
                    {
                        float distSq = Vector3.DistanceSquared(vertPos, bonePositions[bi]);
                        if (distSq < minDistSq)
                        {
                            minDistSq = distSq;
                            closestBone = bi;
                        }
                    }
                    if (closestBone >= 0)
                    {
                        mv.BoneID0 = closestBone;
                        mv.Weight0 = 1f;
                        mv.BoneID1 = -1;
                        mv.BoneID2 = -1;
                        mv.BoneID3 = -1;
                        mainMesh.Vertices[vi] = mv;
                        assigned++;
                    }
                }
            }
            Console.WriteLine($"Assigned {assigned} unweighted vertices to closest bones");
        }
    }
}