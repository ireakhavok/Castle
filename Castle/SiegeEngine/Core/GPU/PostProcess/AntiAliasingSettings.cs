// Folder: SiegeEngine/Core/GPU/PostProcess
// File: AntiAliasingSettings.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Managers;

namespace SiegeEngine.Core.GPU.PostProcess
{
    /// <summary>
    /// Process-wide AA mode. Machine-local settings win when the user has
    /// chosen a mode on this box. Otherwise the authored scene payload is
    /// used. Otherwise SMAA (IDE session default).
    /// </summary>
    public static class AntiAliasingSettings
    {
        private static UISettingsManager _machine;
        private static EnvironmentSettings _authored;
        private static AntiAliasingMode? _sessionOverride;

        public static void BindMachine(UISettingsManager settings)
        {
            _machine = settings;
        }

        public static void BindAuthored(EnvironmentSettings environment)
        {
            _authored = environment;
        }

        public static void SetSessionOverride(AntiAliasingMode? mode)
        {
            _sessionOverride = mode;
        }

        public static AntiAliasingMode Resolve()
        {
            if (_sessionOverride.HasValue)
                return _sessionOverride.Value;

            if (_machine != null && _machine.HasAntiAliasingOverride)
                return _machine.AntiAliasingMode;

            if (TryParse(_authored?.AntiAliasing, out AntiAliasingMode authored))
                return authored;

            return AntiAliasingMode.SMAA;
        }

        public static void SetLive(AntiAliasingMode mode, bool save = true)
        {
            _sessionOverride = null;
            if (_machine == null)
            {
                _sessionOverride = mode;
                return;
            }
            _machine.SetAntiAliasingMode(mode, save);
        }

        public static bool TryParse(string value, out AntiAliasingMode mode)
            => AntiAliasingModeParser.TryParse(value, out mode);

        public static string ToPayloadString(AntiAliasingMode mode)
            => AntiAliasingModeParser.ToPayloadString(mode);
    }
}