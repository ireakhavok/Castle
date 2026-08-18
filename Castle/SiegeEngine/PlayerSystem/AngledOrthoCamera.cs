// Folder: SiegeEngine/PlayerSystem
// File: AngledOrthoCamera.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.GPU.ContextManagement;
using System;
using System.Diagnostics;
using System.Numerics;

namespace SiegeEngine.PlayerSystem
{
    public class AngledOrthoCamera : CameraController
    {
        public AngledOrthoCamera(IControlContext controlContext, IntPtr window, Player player = null)
            : base(controlContext, window, player)
        {
            _pitch = -30f; // Fixed tilt for angled view
            _yaw = 0f; // Lock yaw for consistent orientation
            _perspective = Perspective.ThirdPerson; // Use third-person distance for zoom
        }

        private void UpdateCamera()
        {
            if (_player != null)
            {
                Vector3 target = _player.Physics.Position + new Vector3(0, 0, _playerHeight / 2);
                float yawRad = _yaw * (float)(Math.PI / 180);
                float pitchRad = _pitch * (float)(Math.PI / 180);
                Vector3 offset = new Vector3(
                    (float)Math.Sin(yawRad) * (float)Math.Cos(pitchRad),
                    (float)Math.Cos(yawRad) * (float)Math.Cos(pitchRad),
                    (float)Math.Sin(pitchRad)
                ) * _distance;
                _position = target - offset;
                ViewMatrix = Matrix4x4.CreateLookAt(_position, target, Vector3.UnitZ);
            }
            else
            {
                float yawRad = _yaw * (float)(Math.PI / 180);
                float pitchRad = _pitch * (float)(Math.PI / 180);
                Vector3 direction = new Vector3(
                    (float)Math.Cos(pitchRad) * (float)Math.Sin(yawRad),
                    (float)Math.Cos(pitchRad) * (float)Math.Cos(yawRad),
                    (float)Math.Sin(pitchRad)
                );
                ViewMatrix = Matrix4x4.CreateLookAt(_position, _position + direction, Vector3.UnitZ);
            }
        }
    }
}