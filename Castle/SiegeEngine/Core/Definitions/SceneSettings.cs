// Folder: SiegeEngine/Core/Definitions
// File: SceneSettings.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SiegeEngine.Core.Physics;

namespace SiegeEngine.Core.Definitions
{
    public enum PlayerPresence
    {
        None,
        Avatar,
        Spectator
    }

    public enum PlayerCollisionType
    {
        Capsule,
        Hitbox
    }

    public class SceneSettings
    {
        [JsonPropertyName("avatarPackKey")]
        public string AvatarPackKey { get; set; }

        [JsonPropertyName("animationPackKey")]
        public string AnimationPackKey { get; set; }

        [JsonPropertyName("controllerTypeName")]
        public string ControllerTypeName { get; set; }

        [JsonPropertyName("preferredSpawnPointIds")]
        public List<int> PreferredSpawnPointIds { get; set; } = new List<int>();

        [JsonPropertyName("cameraMode")]
        public string CameraMode { get; set; }

        [JsonPropertyName("playerPresence")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PlayerPresence PlayerPresence { get; set; } = PlayerPresence.Avatar;

        [JsonPropertyName("playerCollisionType")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PlayerCollisionType PlayerCollisionType { get; set; } = PlayerCollisionType.Hitbox;

        // Defaults match PhysicsComponent so an unedited scene does not change fall/friction.
        [JsonPropertyName("playerMass")]
        public float PlayerMass { get; set; } = 1f;

        [JsonPropertyName("playerFriction")]
        public float PlayerFriction { get; set; } = 1.8f;

        [JsonPropertyName("playerRestitution")]
        public float PlayerRestitution { get; set; } = 0f;

        [JsonPropertyName("playerLinearDamping")]
        public float PlayerLinearDamping { get; set; } = 0.4f;

        [JsonPropertyName("playerAngularDamping")]
        public float PlayerAngularDamping { get; set; } = 0.4f;

        [JsonPropertyName("playerKineticFriction")]
        public float PlayerKineticFriction { get; set; } = 0.60f;

        [JsonPropertyName("playerStaticFriction")]
        public float PlayerStaticFriction { get; set; } = 0.85f;

        [JsonPropertyName("playerReceiveFriction")]
        public bool PlayerReceiveFriction { get; set; } = true;

        [JsonPropertyName("playerReceiveVerticalContact")]
        public bool PlayerReceiveVerticalContact { get; set; } = true;

        [JsonPropertyName("playerKeepUpright")]
        public bool PlayerKeepUpright { get; set; } = true;

        public PlayerCollisionType ResolvePlayerCollisionType(bool avatarHasSkeleton)
        {
            if (PlayerCollisionType == PlayerCollisionType.Hitbox && !avatarHasSkeleton)
                return PlayerCollisionType.Capsule;
            return PlayerCollisionType;
        }

        public void ApplyPlayerPhysics(PhysicsComponent physics) => ApplyToPlayer(physics);

        public void ApplyToPlayer(PhysicsComponent physics)
        {
            if (physics == null) return;
            physics.Mass = PlayerMass > 1e-4f ? PlayerMass : 1f;
            physics.Friction = PlayerFriction;
            physics.Restitution = PlayerRestitution;
            physics.LinearDamping = PlayerLinearDamping;
            physics.AngularDamping = PlayerAngularDamping;
            physics.KineticFriction = PlayerKineticFriction;
            physics.StaticFriction = PlayerStaticFriction;
            physics.ReceiveFriction = PlayerReceiveFriction;
            physics.ReceiveVerticalContact = PlayerReceiveVerticalContact;
            physics.KeepUpright = PlayerKeepUpright;
            physics.BodyType = BodyType.Dynamic;
        }
    }
}
