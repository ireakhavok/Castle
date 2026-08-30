// Folder: SiegeEngine/PlayerSystem
// File: CameraController.cs
using System;
using System.Numerics;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Physics;

namespace SiegeEngine.PlayerSystem
{
    public enum Perspective
    {
        FirstPerson,
        ThirdPerson,
        OverTheShoulder
    }

    public class CameraController
    {
        protected readonly IControlContext _controlContext;
        protected readonly IntPtr _window;
        protected readonly Player _player;
        protected Perspective _perspective = Perspective.ThirdPerson;
        protected float _yaw = 0f;
        protected float _pitch = 0f;
        protected float _distance = 2.0f;
        protected readonly float _minDistance = 0.5f;
        protected readonly float _maxDistance = 100.0f;
        protected readonly float _zoomSpeed = 10f;
        protected readonly float _xSpeed = 2.0f;
        protected readonly float _ySpeed = 2.0f;
        protected readonly float _pitchMinLimit = -89f;
        protected readonly float _pitchMaxLimit = 89f;
        protected Vector2 _lastMousePos = Vector2.Zero;
        protected bool _firstMouseMove = true;
        protected bool _isRightShoulder = true;
        protected readonly float _shoulderShiftAmount = 1.0f;
        protected readonly float _playerHeight = 1.9f;
        protected bool _isPPressed = false;
        protected bool _wasPPressedLastFrame = false;
        protected bool _wasShiftPressedLastFrame = false;
        protected bool _wasTabPressedLastFrame = false;
        protected Vector3 _position;
        private IHeightProvider _ground;
        private const float GroundClearance = 0.45f;

        public Vector3 Position => _position;
        public Matrix4x4 ViewMatrix { get; set; }
        public float Yaw => _yaw;
        public Perspective CurrentPerspective => _perspective;
        public float Pitch => _pitch;
        public Vector2 MousePosition { get; private set; }

        public CameraController(IControlContext controlContext, IntPtr window, Player player = null)
        {
            _controlContext = controlContext ?? throw new ArgumentNullException(nameof(controlContext));
            _window = window;
            _player = player;
            _position = _player?.Physics.Position + new Vector3(0, 0, _playerHeight) ?? new Vector3(64, 36, 0.05f);
            UpdateCamera();
        }

        public void SetPerspective(Perspective perspective)
        {
            _perspective = perspective;
        }

        public void SetGroundQuery(IHeightProvider ground)
        {
            _ground = ground;
        }

        public void Update(float deltaTime, float scrollDelta, bool isGameActive)
        {
            bool focused = _controlContext.GetWindowAttrib(_window, WindowAttribute.Focused);
            if (!focused)
            {
                _controlContext.SetInputMode(_window, CursorAttribute.Cursor, CursorMode.Normal);
                _firstMouseMove = true;
                return;
            }
            _controlContext.SetInputMode(_window, CursorAttribute.Cursor,
                isGameActive ? CursorMode.Disabled : CursorMode.Normal);
            _controlContext.GetCursorPos(_window, out double mouseX, out double mouseY);
            Vector2 mousePos = new Vector2((float)mouseX, (float)mouseY);
            if (!isGameActive)
            {
                MousePosition = mousePos;
                _firstMouseMove = true;
            }
            else
            {
                Vector2 delta = Vector2.Zero;
                if (_firstMouseMove)
                {
                    _lastMousePos = mousePos;
                    _firstMouseMove = false;
                }
                else
                {
                    delta = mousePos - _lastMousePos;
                    _lastMousePos = mousePos;
                }
                float sensitivityX = _xSpeed * deltaTime;
                float sensitivityY = _ySpeed * deltaTime;
                _yaw += delta.X * sensitivityX;
                _pitch -= delta.Y * sensitivityY;
                _pitch = Math.Clamp(_pitch, _pitchMinLimit, _pitchMaxLimit);
                if (_player != null)
                {
                    _isPPressed = _controlContext.GetKey(_window, Key.P) == InputAction.Press;
                    if (_isPPressed && !_wasPPressedLastFrame)
                        ChangePerspective();
                    _wasPPressedLastFrame = _isPPressed;
                    bool isShiftPressed = _controlContext.GetKey(_window, Key.LeftShift) == InputAction.Press;
                    bool isTabPressed = _controlContext.GetKey(_window, Key.Tab) == InputAction.Press;
                    if (_perspective == Perspective.OverTheShoulder && isShiftPressed && isTabPressed && (!_wasShiftPressedLastFrame || !_wasTabPressedLastFrame))
                        _isRightShoulder = !_isRightShoulder;
                    _wasShiftPressedLastFrame = isShiftPressed;
                    _wasTabPressedLastFrame = isTabPressed;
                    if (_perspective != Perspective.FirstPerson && scrollDelta != 0)
                    {
                        float step = MathF.Max(0.35f, _distance * 0.18f);
                        _distance -= scrollDelta * step;
                        _distance = Math.Clamp(_distance, _minDistance, _maxDistance);
                    }
                }
            }
            MousePosition = mousePos;
            UpdateCamera();
        }

        public void RefreshFromPhysics()
        {
            if (_player != null)
                UpdateCamera();
        }

        private void ChangePerspective()
        {
            _perspective = _perspective switch
            {
                Perspective.FirstPerson => Perspective.ThirdPerson,
                Perspective.ThirdPerson => Perspective.OverTheShoulder,
                Perspective.OverTheShoulder => Perspective.FirstPerson,
                _ => Perspective.ThirdPerson
            };
            Console.WriteLine($"Camera perspective changed to: {_perspective}");
        }

        protected void UpdateCamera()
        {
            if (_player != null)
            {
                Vector3 bodyPos = _player.Physics.RenderPosition;
                Vector3 head = bodyPos + new Vector3(0, 0, _playerHeight);
                Vector3 chest = bodyPos + new Vector3(0, 0, _playerHeight * 0.55f);
                float yawRad = _yaw * (float)(Math.PI / 180);
                float pitchRad = _pitch * (float)(Math.PI / 180);
                Vector3 lookDirection = new Vector3(
                    (float)Math.Cos(pitchRad) * (float)Math.Sin(yawRad),
                    (float)Math.Cos(pitchRad) * (float)Math.Cos(yawRad),
                    (float)Math.Sin(pitchRad)
                );
                Vector3 horizontalForward = new Vector3(
                    (float)Math.Sin(yawRad),
                    (float)Math.Cos(yawRad),
                    0
                );
                if (horizontalForward.LengthSquared() < 1e-6f)
                    horizontalForward = new Vector3(0, 1, 0);
                else
                    horizontalForward = Vector3.Normalize(horizontalForward);

                if (_perspective == Perspective.FirstPerson)
                {
                    _position = head;
                    ViewMatrix = Matrix4x4.CreateLookAt(_position, _position + lookDirection, Vector3.UnitZ);
                }
                else if (_perspective == Perspective.ThirdPerson)
                {
                    Vector3 offset = lookDirection * _distance;
                    _position = KeepAboveGround(chest - offset, chest);
                    ViewMatrix = Matrix4x4.CreateLookAt(_position, chest, Vector3.UnitZ);
                }
                else if (_perspective == Perspective.OverTheShoulder)
                {
                    float shoulderShift = _isRightShoulder ? _shoulderShiftAmount * 0.6f : -_shoulderShiftAmount * 0.6f;
                    Vector3 lookFlat = new Vector3(
                        (float)Math.Sin(yawRad) * (float)Math.Cos(pitchRad),
                        (float)Math.Cos(yawRad) * (float)Math.Cos(pitchRad),
                        (float)Math.Sin(pitchRad)
                    );
                    Vector3 right = Vector3.Cross(lookFlat, Vector3.UnitZ);
                    if (right.LengthSquared() < 1e-6f)
                        right = Vector3.Cross(horizontalForward, Vector3.UnitZ);
                    right = Vector3.Normalize(right);

                    // 0 at horizon/up, 1 when looking fully down.
                    float lookDown = Math.Clamp((-_pitch) / 70f, 0f, 1f);

                    // Boom: looking down swings the camera up and forward over the player
                    // so the crosshair can see the ground instead of the back of the head.
                    float backDist = _distance * (0.82f - 0.58f * lookDown);
                    float lift = _distance * (0.14f + 0.72f * lookDown);
                    Vector3 pivot = head;
                    Vector3 desired = pivot
                        - horizontalForward * backDist
                        + Vector3.UnitZ * lift
                        + right * shoulderShift;

                    _position = KeepAboveGround(desired, pivot);
                    Vector3 lookTarget = pivot + lookDirection * 1000f;
                    ViewMatrix = Matrix4x4.CreateLookAt(_position, lookTarget, Vector3.UnitZ);
                }
            }
            else
            {
                float yawRad = _yaw * (float)(Math.PI / 180);
                float pitchRad = _pitch * (float)(Math.PI / 180);
                Vector3 direction = new Vector3(
                    (float)Math.Cos(pitchRad) * (float)Math.Sin(yawRad),
                    (float)Math.Cos(pitchRad) * (float)Math.Cos(pitchRad),
                    (float)Math.Sin(pitchRad)
                );
                ViewMatrix = Matrix4x4.CreateLookAt(_position, _position + direction, Vector3.UnitZ);
            }
        }

        private Vector3 KeepAboveGround(Vector3 desired, Vector3 pivot)
        {
            if (_ground == null)
                return desired;

            Vector3 lastClear = pivot;
            const int steps = 12;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 p = pivot + (desired - pivot) * t;
                float floor = _ground.GetInterpolatedHeight(p.X, p.Y) + GroundClearance;
                if (p.Z < floor)
                {
                    lastClear.Z = Math.Max(lastClear.Z, _ground.GetInterpolatedHeight(lastClear.X, lastClear.Y) + GroundClearance);
                    return lastClear;
                }
                lastClear = p;
            }

            float g = _ground.GetInterpolatedHeight(desired.X, desired.Y) + GroundClearance;
            if (desired.Z < g)
                desired.Z = g;
            return desired;
        }
    }
}
