using System;
using System.Linq;
using System.Numerics;
using SiegeEngine.Definitions;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Interfaces;

namespace SiegeEngine.Definitions
{
    public class Spawner
    {
        private readonly IGameServer _server;
        private int _nextId = 1;
        private readonly Random _random = new(); // New for random positions

        public Spawner(IGameServer server)
        {
            _server = server;
        }

        public Entity SpawnEntity(string type, Vector3 position)
        {
            if (type == "Player" && _server.GetEntities().Any(e => e.Type == "Player"))
            {
                Console.WriteLine("Spawner: Player already exists, skipping spawn.");
                return null;
            }

            var entity = new Entity { Id = _nextId++, Type = type };
            entity.AddComponent(new PhysicsComponent { Position = position, IsBreakable = type != "Water" });
            if (type == "Player")
            {
                entity.AddComponent(new Player(entity.Id, position));
            }
            else if (type == "AIPlayer") // New type
            {
                entity.AddComponent(new AIPlayer(entity.Id, position));
            }
            _server.AddEntity(entity);
            Console.WriteLine($"Spawner: Spawned entity {entity.Id} of type {type} at {position}");
            return entity;
        }

        public void SpawnAIPlayers(int count) // New bulk spawn
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 position = new Vector3(
                    (float)_random.NextDouble() * 128, // Within grid
                    (float)_random.NextDouble() * 72,
                    0
                );
                SpawnEntity("AIPlayer", position);
            }
            Console.WriteLine($"Spawner: Spawned {count} AIPlayers");
        }
    }
}