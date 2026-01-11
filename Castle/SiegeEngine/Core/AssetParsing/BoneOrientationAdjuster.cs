// Folder: AssetParsing
// File: BoneOrientationAdjuster.cs
using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing
{
    public static class BoneOrientationAdjuster
    {
        public static void AdjustSkeletonOrientations(FBXModel model)
        {
            Skeleton skeleton = model.Skeleton;
            if (skeleton == null || skeleton.Bones.Count == 0) return;

            var locals = skeleton.Bones.Select(b => b.LocalRest).ToArray();
            var globals = skeleton.ComputeGlobalTransforms(locals);

            var roots = skeleton.Bones.Where(b => b.ParentIndex == -1).Select(b => skeleton.Bones.IndexOf(b)).ToList();

            foreach (int root in roots)
            {
                AdjustBoneOrientation(root, null, skeleton, globals, locals);
            }

            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                skeleton.Bones[i].LocalRest = locals[i];
                if (Matrix4x4.Decompose(locals[i], out Vector3 s, out Quaternion r, out Vector3 t))
                {
                    skeleton.Bones[i].LclScaling = s;
                    skeleton.Bones[i].LclRotation = r;
                    skeleton.Bones[i].LclTranslation = t;
                }
            }
        }

        private static void AdjustBoneOrientation(int idx, Matrix4x4? parentCorrectionInv, Skeleton skeleton, Matrix4x4[] globals, Matrix4x4[] locals)
        {
            Bone bone = skeleton.Bones[idx];
            Vector3 head = globals[idx].Translation;

            var boneChildren = bone.Children.Where(c => true).Select(c => skeleton.Bones.IndexOf(c)).ToList(); // Assuming all children are bones
            var childLocs = new List<Vector3>();
            foreach (int cIdx in boneChildren)
            {
                Vector3 loc = globals[cIdx].Translation - head;
                float mag = loc.Length();
                if (mag > 1e-6f)
                {
                    childLocs.Add(loc / mag);
                }
            }

            Matrix4x4? correctionMatrix = null;

            if (childLocs.Count == 0)
            {
                // No children, inherit the correction from parent (if possible)
                if (parentCorrectionInv.HasValue)
                {
                    Matrix4x4.Invert(parentCorrectionInv.Value, out Matrix4x4 parentCorrection);
                    correctionMatrix = parentCorrection;
                }
            }
            else
            {
                Vector3 bestAxis = Vector3.Zero;
                if (childLocs.Count == 1)
                {
                    Vector3 vec = childLocs[0];
                    bestAxis = new Vector3(0, 0, vec.Z >= 0 ? 1 : -1);
                    if (Math.Abs(vec.X) > Math.Abs(vec.Y))
                    {
                        if (Math.Abs(vec.X) > Math.Abs(vec.Z))
                        {
                            bestAxis = new Vector3(vec.X >= 0 ? 1 : -1, 0, 0);
                        }
                    }
                    else if (Math.Abs(vec.Y) > Math.Abs(vec.Z))
                    {
                        bestAxis = new Vector3(0, vec.Y >= 0 ? 1 : -1, 0);
                    }
                }
                else
                {
                    float bestAngle = -1f;
                    foreach (Vector3 vec in childLocs)
                    {
                        Vector3 testAxis = new Vector3(0, 0, vec.Z >= 0 ? 1 : -1);
                        if (Math.Abs(vec.X) > Math.Abs(vec.Y))
                        {
                            if (Math.Abs(vec.X) > Math.Abs(vec.Z))
                            {
                                testAxis = new Vector3(vec.X >= 0 ? 1 : -1, 0, 0);
                            }
                        }
                        else if (Math.Abs(vec.Y) > Math.Abs(vec.Z))
                        {
                            testAxis = new Vector3(0, vec.Y >= 0 ? 1 : -1, 0);
                        }

                        float maxAngle = 1f;
                        foreach (Vector3 loc in childLocs)
                        {
                            maxAngle = Math.Min(maxAngle, Vector3.Dot(testAxis, loc));
                        }

                        if (bestAngle < maxAngle)
                        {
                            bestAngle = maxAngle;
                            bestAxis = testAxis;
                        }
                    }
                }

                string toUp;
                if (Math.Abs(bestAxis.X) > 0.9f)
                {
                    toUp = bestAxis.X > 0 ? "X" : "-X";
                }
                else if (Math.Abs(bestAxis.Y) > 0.9f)
                {
                    toUp = bestAxis.Y > 0 ? "Y" : "-Y";
                }
                else
                {
                    toUp = bestAxis.Z > 0 ? "Z" : "-Z";
                }

                string toForward = (toUp == "X" || toUp == "-X") ? "Y" : "X";

                correctionMatrix = AxisConversion("X", "Y", toForward, toUp);
            }

            if (correctionMatrix.HasValue)
            {
                locals[idx] = locals[idx] * correctionMatrix.Value;
            }

            Matrix4x4? childParentInv = correctionMatrix.HasValue ? Invert(correctionMatrix.Value) : null;

            foreach (var child in bone.Children)
            {
                int cIdx = skeleton.Bones.IndexOf(child);
                AdjustBoneOrientation(cIdx, childParentInv, skeleton, globals, locals);
            }
        }

        private static Matrix4x4 AxisConversion(string fromForward, string fromUp, string toForward, string toUp)
        {
            Matrix4x4 fromBasis = BuildBasis(fromForward, fromUp);
            Matrix4x4 toBasis = BuildBasis(toForward, toUp);
            Matrix4x4 invFrom;
            if (!Matrix4x4.Invert(fromBasis, out invFrom))
            {
                invFrom = Matrix4x4.Transpose(fromBasis); // Approximation if singular, but shouldn't be
            }
            return toBasis * invFrom;
        }

        private static Matrix4x4 BuildBasis(string forward, string up)
        {
            Vector3 f = GetAxis(forward);
            Vector3 u = GetAxis(up);
            Vector3 r = Vector3.Normalize(Vector3.Cross(f, u));

            return new Matrix4x4(
                r.X, f.X, u.X, 0,
                r.Y, f.Y, u.Y, 0,
                r.Z, f.Z, u.Z, 0,
                0, 0, 0, 1
            );
        }

        private static Vector3 GetAxis(string axis)
        {
            switch (axis)
            {
                case "X": return new Vector3(1, 0, 0);
                case "-X": return new Vector3(-1, 0, 0);
                case "Y": return new Vector3(0, 1, 0);
                case "-Y": return new Vector3(0, -1, 0);
                case "Z": return new Vector3(0, 0, 1);
                case "-Z": return new Vector3(0, 0, -1);
                default: throw new ArgumentException("Invalid axis");
            }
        }

        private static Matrix4x4 Invert(Matrix4x4 mat)
        {
            if (!Matrix4x4.Invert(mat, out Matrix4x4 inv))
            {
                throw new InvalidOperationException("Matrix inversion failed");
            }
            return inv;
        }
    }
}