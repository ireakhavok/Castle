using Engine.Core.Events;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;
using System;
using System.Numerics;

namespace SiegeEngine.Systems
{
    public class EnvironmentSystem : GameSystem
    {
        private float _timeOfDay; // 0 to 24
        private WeatherState _weatherState;
        private Vector3 _ambientColor;
        private float _fogDensity;
        private readonly EventBus _eventBus;

        public EnvironmentSystem(IGameServer server, EventBus eventBus) : base(server)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _timeOfDay = 12.0f; // Start at noon
            _weatherState = WeatherState.Clear;
            UpdateAmbientColor();
            _fogDensity = 0.01f;
        }

        public override void Update(float deltaTime)
        {
            float timeScale = 60.0f; // 1 real minute = 1 game hour
            _timeOfDay += deltaTime * timeScale / 3600.0f;
            if (_timeOfDay >= 24.0f)
                _timeOfDay -= 24.0f;

            UpdateAmbientColor();
        }

        private void UpdateAmbientColor()
        {
            if (_timeOfDay >= 6.0f && _timeOfDay < 18.0f)
            {
                _ambientColor = new Vector3(0.8f, 0.8f, 0.8f); // Day
            }
            else
            {
                _ambientColor = new Vector3(0.2f, 0.2f, 0.3f); // Night
            }
            _eventBus.Publish(new TimeOfDayChangedEvent(_timeOfDay, _ambientColor), true);
        }

        public float TimeOfDay => _timeOfDay;
        public WeatherState Weather => _weatherState;
        public Vector3 AmbientColor => _ambientColor;
        public float FogDensity => _fogDensity;

        public void SetTimeOfDay(float time)
        {
            _timeOfDay = time % 24.0f;
            UpdateAmbientColor();
        }

        public void SetWeather(WeatherState state)
        {
            _weatherState = state;
            switch (state)
            {
                case WeatherState.Fog:
                    _fogDensity = 0.05f;
                    break;
                default:
                    _fogDensity = 0.01f;
                    break;
            }
            _eventBus.Publish(new WeatherChangedEvent(state), true);
        }
    }
}