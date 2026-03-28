// Folder: SiegeEngine/Core/Interfaces
// File: IProjectionProvider.cs
using System.Numerics;

namespace SiegeEngine.Core.Interfaces
{
    public interface IProjectionProvider
    {
        Matrix4x4 GetProjectionMatrix(int width, int height, float near, float far);
    }
}