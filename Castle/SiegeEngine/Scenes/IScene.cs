using SiegeEngine.Core.Definitions;
using SiegeEngine.PlayerSystem;
using System.Collections.Generic;

namespace SiegeEngine.Scenes
{
    public interface IScene
    {
        void Initialize(int width, int height);
        void Update(float deltaTime);
        void Render(IReadOnlyList<Entity> entities);
        void Resize(int width, int height);
        void Dispose();
        void SetPlayer(Player player);
    }
}