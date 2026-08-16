// Folder: SiegeEngine/Core/Rendering
// File: OrthoProjection.cs
using SiegeEngine.Core.Interfaces;
using System;
using System.Numerics;

namespace SiegeEngine.Core.Rendering.Projections
{
    public class OrthoProjection : IProjectionProvider
    {
        private readonly float _tiltAngleDegrees;

        public OrthoProjection(float tiltAngleDegrees = 30f)
        {
            _tiltAngleDegrees = tiltAngleDegrees;
        }

        public Matrix4x4 GetProjectionMatrix(int width, int height, float near, float far)
        {
            float aspect = (float)width / height;
            float orthoHeight = 1000f; // Adjustable base height
            float orthoWidth = orthoHeight * aspect;
            var ortho = Matrix4x4.CreateOrthographic(orthoWidth, orthoHeight, near, far);
            float tiltRad = _tiltAngleDegrees * (MathF.PI / 180f);
            var tilt = Matrix4x4.CreateRotationX(tiltRad);
            return ortho * tilt;
        }
    }
}