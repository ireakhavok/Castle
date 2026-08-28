using SiegeEngine.Core.Events;
using System;
using System.Numerics;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Definitions;

namespace SiegeEngine.Systems
{
    public class EnvironmentSystem : GameSystem
    {
        private float _timeOfDay;
        private WeatherState _weatherState;
        private Vector3 _ambientColor;
        private float _fogDensity;
        private FogMode _fogMode;
        private readonly EventBus _eventBus;

        public EnvironmentSystem(IGameServer server, EventBus eventBus) : base(server)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _timeOfDay = 12.0f;
            _weatherState = WeatherState.Clear;
            _fogMode = FogMode.Off;
            UpdateAmbientColor();
            _fogDensity = 0.01f;
        }

        public override void Update(float deltaTime)
        {
            float timeScale = 60.0f;
            _timeOfDay += deltaTime * timeScale / 3600.0f;
            if (_timeOfDay >= 24.0f)
                _timeOfDay -= 24.0f;

            UpdateAmbientColor();
        }

        private void UpdateAmbientColor()
        {
            if (_timeOfDay >= 6.0f && _timeOfDay < 18.0f)
            {
                _ambientColor = new Vector3(0.8f, 0.8f, 0.8f);
            }
            else
            {
                _ambientColor = new Vector3(0.2f, 0.2f, 0.3f);
            }
            _eventBus.Publish(new TimeOfDayChangedEvent(_timeOfDay, _ambientColor), true);
        }

        public float TimeOfDay => _timeOfDay;
        public WeatherState Weather => _weatherState;
        public Vector3 AmbientColor => _ambientColor;
        public float FogDensity => _fogDensity;
        public FogMode FogMode => _fogMode;

        public void SetTimeOfDay(float time)
        {
            _timeOfDay = time % 24.0f;
            UpdateAmbientColor();
        }

        public void ApplyEnvironment(EnvironmentSettings settings)
        {
            if (settings == null) return;
            _timeOfDay = settings.TimeOfDay % 24.0f;
            _fogDensity = settings.FogDensity;
            if (!FogModeParser.TryParse(settings.FogMode, out _fogMode))
                _fogMode = _fogDensity > 0.001f ? FogMode.Exponential : FogMode.Off;
            if (!string.IsNullOrWhiteSpace(settings.Weather) && Enum.TryParse(settings.Weather, true, out WeatherState weather))
                _weatherState = weather;
            if (settings.AmbientColor.LengthSquared() > 1e-6f)
                _ambientColor = settings.AmbientColor;
            else
                UpdateAmbientColor();
        }

        public void SetWeather(WeatherState state)
        {
            _weatherState = state;
            switch (state)
            {
                case WeatherState.Fog:
                    _fogDensity = 0.05f;
                    if (_fogMode == FogMode.Off)
                        _fogMode = FogMode.Exponential;
                    break;
                default:
                    _fogDensity = 0.01f;
                    break;
            }
            _eventBus.Publish(new WeatherChangedEvent(state), true);
        }
    }
}
