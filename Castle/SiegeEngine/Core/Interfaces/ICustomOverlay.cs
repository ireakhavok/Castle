// Folder: SiegeEngine.Core.Interfaces
// File: ICustomOverlay.cs
using SiegeEngine.Core.GPU.Renderers;
using System.Numerics;

namespace SiegeEngine.Core.Interfaces
{
    public interface ICustomOverlay
    {
        void Draw(UIQuadRenderer quadRenderer, float panelWidth, float panelHeight);
    }

    public interface IWorldOverlay
    {
        void RenderWorld(Matrix4x4 view, Matrix4x4 projection);
    }
}