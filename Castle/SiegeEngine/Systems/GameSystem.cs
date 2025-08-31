using SiegeEngine.Interfaces;

namespace SiegeEngine.Systems
{
    public abstract class GameSystem
    {
        protected readonly IGameServer _server;

        protected GameSystem(IGameServer server)
        {
            _server = server;
        }

        public abstract void Update(float deltaTime);
    }
}