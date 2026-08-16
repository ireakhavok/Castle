// Folder: SiegeEngine/Core/Rendering
// File: PerspectiveProjection.cs
using SiegeEngine.Core.Interfaces;
using System;
using System.Numerics;

namespace SiegeEngine.Core.Rendering.Projections
{
    public class PerspectiveProjection : IProjectionProvider
    {
        public Matrix4x4 GetProjectionMatrix(int width, int height, float near, float far)
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)width / height, near, far);
        }
    }
}