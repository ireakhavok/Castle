using System;
using System.Collections.Generic;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Definitions;

namespace SiegeEngine.Core.Rendering
{
    public abstract class Renderer : IRenderer
    {
        protected readonly Player _player;

        protected Renderer(Player player)
        {
            _player = player;
        }

        public abstract void Initialize(nint windowHandle, int width, int height, Player player);
        public abstract void Render(IReadOnlyList<Entity> entities);
        public abstract void Resize(int width, int height);
        public abstract void Dispose();
    }
}