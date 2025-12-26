using SiegeEngine.Core.AssetParsing.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace SiegeEngine.Core.AssetParsing.Model
{
    public class Animation
    {
        public string Name { get; set; }
        public List<Keyframe> Keyframes { get; set; } = new List<Keyframe>();
        public Matrix4x4[] GetBoneTransforms(float time)
        {
            if (Keyframes.Count == 0)
            {
                return null;
            }
            float duration = Keyframes.Last().Time;
            if (duration <= 0)
            {
                return Keyframes[0].BoneTransforms.ToArray();
            }
            time = time % duration;
            int lowerIndex = Keyframes.FindLastIndex(kf => kf.Time <= time);
            if (lowerIndex == -1)
            {
                return Keyframes[0].BoneTransforms.ToArray();
            }
            if (lowerIndex == Keyframes.Count - 1 || Keyframes[lowerIndex].Time == time)
            {
                return Keyframes[lowerIndex].BoneTransforms.ToArray();
            }
            Keyframe lower = Keyframes[lowerIndex];
            Keyframe upper = Keyframes[lowerIndex + 1];
            float factor = (time - lower.Time) / (upper.Time - lower.Time);
            int numBones = lower.BoneTransforms.Count;
            Matrix4x4[] interpolated = new Matrix4x4[numBones];
            for (int b = 0; b < numBones; b++)
            {
                bool lowerDecomposed = Matrix4x4.Decompose(lower.BoneTransforms[b], out Vector3 lScale, out Quaternion lRot, out Vector3 lTrans);
                bool upperDecomposed = Matrix4x4.Decompose(upper.BoneTransforms[b], out Vector3 uScale, out Quaternion uRot, out Vector3 uTrans);
                if (lowerDecomposed && upperDecomposed)
                {
                    Vector3 iTrans = Vector3.Lerp(lTrans, uTrans, factor);
                    Quaternion iRot = Quaternion.Slerp(lRot, uRot, factor);
                    Vector3 iScale = Vector3.Lerp(lScale, uScale, factor);
                    interpolated[b] = Matrix4x4.CreateScale(iScale) * Matrix4x4.CreateFromQuaternion(iRot) * Matrix4x4.CreateTranslation(iTrans);
                }
                else
                {
                    interpolated[b] = lower.BoneTransforms[b];
                    Console.WriteLine($"Animation {Name}: Failed to decompose matrices for bone {b} at time {time} (lower: {lowerDecomposed}, upper: {upperDecomposed}). Using lower keyframe.");
                }
            }
            return interpolated;
        }
    }
}