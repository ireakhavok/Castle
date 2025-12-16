using System;
using SiegeEngine.Core.Definitions;

namespace SiegeEngine.Core.Managers
{
    public class GameModeManager
    {
        private GameMode _selectedMode = GameMode.None;

        public void SelectMode(GameMode mode)
        {
            _selectedMode = mode;
            Console.WriteLine($"Selected game mode: {mode}");
        }

        public GameMode GetSelectedMode()
        {
            return _selectedMode;
        }
    }
}