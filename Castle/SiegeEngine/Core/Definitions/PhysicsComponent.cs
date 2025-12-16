using System;
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    public class PhysicsComponent : IComponent
    {
        private float _mass = 1.0f;
        private float _health = 100f;

        public PhysicsComponent()
        {
            Position = Vector3.Zero;
            Velocity = Vector3.Zero;
            Acceleration = Vector3.Zero;
            Rotation = Quaternion.Identity;
            Size = new Vector3(1f, 1f, 1f);
            IsBreakable = false;
            IsBroken = false;
            Mass = 1.0f;
            Health = 100f;
            IsVisible = true; // New default
        }

        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public Vector3 Acceleration { get; set; }
        public Quaternion Rotation { get; set; }
        public bool IsVisible { get; set; } // New property for raycasting

        public float Mass
        {
            get => _mass;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Mass must be greater than zero.", nameof(value));
                _mass = value;
            }
        }

        public bool IsBreakable { get; set; }
        public bool IsBroken { get; private set; }
        public Vector3 Size { get; set; }

        public float Health
        {
            get => _health;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Health cannot be negative.", nameof(value));
                _health = value;
                if (_health <= 0 && IsBreakable)
                {
                    IsBroken = true;
                }
            }
        }

        public void Break()
        {
            if (IsBreakable && _health <= 0)
            {
                IsBroken = true;
            }
        }
    }
}