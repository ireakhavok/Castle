using System;
using System.Collections.Generic;
using SiegeEngine.Definitions;
using SiegeEngine.PlayerSystem;

namespace SiegeEngine.Interfaces
{
    public interface IRenderer : IDisposable
    {
        void Initialize(nint windowHandle, int width, int height, Player player);
        void Render(IReadOnlyList<Entity> entities);
        void Resize(int width, int height);
    }
}