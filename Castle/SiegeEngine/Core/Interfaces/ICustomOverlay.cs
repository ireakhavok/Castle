// Folder: SiegeEngine.Core.Interfaces
// File: ICustomOverlay.cs
using SiegeEngine.Core.Rendering;

namespace SiegeEngine.Core.Interfaces
{
    public interface ICustomOverlay
    {
        void Draw(UIQuadRenderer quadRenderer, float panelWidth, float panelHeight);
    }
}