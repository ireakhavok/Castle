
using SiegeEngine.Core.AssetParsing.Model;
using System.Collections.Generic;
using System.Numerics;
namespace SiegeEngine.Core.AssetParsing.Model
{
    public class Skeleton
    {
        public List<Bone> Bones { get; set; }
        private Matrix4x4[] _currentTransforms;
        public Matrix4x4[] GetTransforms()
        {
            if (_currentTransforms == null)
            {
                _currentTransforms = new Matrix4x4[Bones.Count];
                for (int i = 0; i < Bones.Count; i++)
                    _currentTransforms[i] = Matrix4x4.Identity;
            }
            return _currentTransforms;
        }
        public void UpdateTransforms(Matrix4x4[] transforms)
        {
            _currentTransforms = transforms;
        }
        public Matrix4x4[] ComputeGlobalTransforms(Matrix4x4[] localTransforms)
        {
            var globalTransforms = new Matrix4x4[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
            {
                var parentIndex = Bones[i].ParentIndex;
                globalTransforms[i] = (parentIndex >= 0 ? globalTransforms[parentIndex] : Matrix4x4.Identity) * localTransforms[i];
            }
            return globalTransforms;
        }
        public Matrix4x4[] ComputeFinalTransforms(Matrix4x4[] globalTransforms)
        {
            var finalTransforms = new Matrix4x4[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
            {
                finalTransforms[i] = globalTransforms[i] * Bones[i].BindPose;
            }
            return finalTransforms;
        }
    }
}