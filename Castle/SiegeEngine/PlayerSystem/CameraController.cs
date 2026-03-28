// Folder: SiegeEngine/PlayerSystem
// File: CameraController.cs
using System;
using System.Numerics;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
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
        protected float _distance = 200.0f;
        protected readonly float _minDistance = 50.0f;
        protected readonly float _maxDistance = 8000.0f;
        protected readonly float _zoomSpeed = 1000.0f;
        protected readonly float _xSpeed = 2.0f;
        protected readonly float _ySpeed = 2.0f;
        protected readonly float _pitchMinLimit = -89f;
        protected readonly float _pitchMaxLimit = 89f;
        protected Vector2 _lastMousePos = Vector2.Zero;
        protected bool _firstMouseMove = true;
        protected bool _isRightShoulder = true;
        protected readonly float _shoulderShiftAmount = 300f;
        protected readonly float _playerHeight = 190f;
        protected bool _isPPressed = false;
        protected bool _wasPPressedLastFrame = false;
        protected bool _wasShiftPressedLastFrame = false;
        protected bool _wasTabPressedLastFrame = false;
        protected Vector3 _position;
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
            _position = _player?.Physics.Position + new Vector3(0, 0, _playerHeight) ?? new Vector3(64, 36, 5);
            UpdateCamera();
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
                //Console.WriteLine($"Menu mouse position: {MousePosition}");
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
                if (_player != null) // Player mode
                {
                    _isPPressed = _controlContext.GetKey(_window, Key.P) == InputAction.Press;
                    if (_isPPressed && !_wasPPressedLastFrame)
                    {
                        ChangePerspective();
                    }
                    _wasPPressedLastFrame = _isPPressed;
                    bool isShiftPressed = _controlContext.GetKey(_window, Key.LeftShift) == InputAction.Press;
                    bool isTabPressed = _controlContext.GetKey(_window, Key.Tab) == InputAction.Press;
                    if (_perspective == Perspective.OverTheShoulder && isShiftPressed && isTabPressed && (!_wasShiftPressedLastFrame || !_wasTabPressedLastFrame))
                    {
                        _isRightShoulder = !_isRightShoulder;
                    }
                    _wasShiftPressedLastFrame = isShiftPressed;
                    _wasTabPressedLastFrame = isTabPressed;
                    if (_perspective != Perspective.FirstPerson && scrollDelta != 0)
                    {
                        _distance -= scrollDelta * _zoomSpeed * deltaTime;
                        _distance = Math.Clamp(_distance, _minDistance, _maxDistance);
                        Console.WriteLine($"Camera distance adjusted to: {_distance}");
                    }
                }
                else // Editor mode: WASD flying
                {
                    float moveSpeed = 20.0f * deltaTime;
                    float yawRad = _yaw * (float)(Math.PI / 180);
                    float pitchRad = _pitch * (float)(Math.PI / 180);
                    Vector3 forward = Vector3.Normalize(new Vector3(
                        (float)Math.Cos(pitchRad) * (float)Math.Sin(yawRad),
                        (float)Math.Cos(pitchRad) * (float)Math.Cos(yawRad),
                        (float)Math.Sin(pitchRad)
                    ));
                    Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
                    if (_controlContext.GetKey(_window, Key.W) == InputAction.Press) _position += forward * moveSpeed;
                    if (_controlContext.GetKey(_window, Key.S) == InputAction.Press) _position -= forward * moveSpeed;
                    if (_controlContext.GetKey(_window, Key.A) == InputAction.Press) _position -= right * moveSpeed;
                    if (_controlContext.GetKey(_window, Key.D) == InputAction.Press) _position += right * moveSpeed;
                    if (_controlContext.GetKey(_window, Key.Space) == InputAction.Press) _position += Vector3.UnitZ * moveSpeed;
                    if (_controlContext.GetKey(_window, Key.LeftControl) == InputAction.Press) _position -= Vector3.UnitZ * moveSpeed;
                }
                UpdateCamera();
            }
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
            if (_player != null) // Player mode only
            {
                Vector3 fptarget = _player.Physics.Position + new Vector3(0, 0, _playerHeight);
                Vector3 tptarget = _player.Physics.Position + new Vector3(0, 0, _playerHeight / 2);
                Vector3 otstarget = _player.Physics.Position + new Vector3(0, 0, _playerHeight);
                float yawRad = _yaw * (float)(Math.PI / 180);
                float pitchRad = _pitch * (float)(Math.PI / 180);
                if (_perspective == Perspective.FirstPerson)
                {
                    _position = fptarget;
                    Vector3 direction = new Vector3(
                        (float)Math.Cos(pitchRad) * (float)Math.Sin(yawRad),
                        (float)Math.Cos(pitchRad) * (float)Math.Cos(yawRad),
                        (float)Math.Sin(pitchRad)
                    );
                    ViewMatrix = Matrix4x4.CreateLookAt(_position, _position + direction, Vector3.UnitZ);
                }
                else if (_perspective == Perspective.ThirdPerson)
                {
                    Vector3 offset = new Vector3(
                        (float)Math.Sin(yawRad) * (float)Math.Cos(pitchRad),
                        (float)Math.Cos(yawRad) * (float)Math.Cos(pitchRad),
                        (float)Math.Sin(pitchRad)
                    ) * _distance;
                    _position = tptarget - offset;
                    ViewMatrix = Matrix4x4.CreateLookAt(_position, tptarget, Vector3.UnitZ);
                }
                else if (_perspective == Perspective.OverTheShoulder)
                {
                    float shoulderShift = _isRightShoulder ? _shoulderShiftAmount : -_shoulderShiftAmount;
                    Vector3 right = Vector3.Normalize(Vector3.Cross(new Vector3(
                        (float)Math.Sin(yawRad) * (float)Math.Cos(pitchRad),
                        (float)Math.Cos(yawRad) * (float)Math.Cos(pitchRad),
                        (float)Math.Sin(pitchRad)
                    ), Vector3.UnitZ));
                    Vector3 offset = new Vector3(
                        (float)Math.Sin(yawRad) * (float)Math.Cos(pitchRad),
                        (float)Math.Cos(yawRad) * (float)Math.Cos(pitchRad),
                        (float)Math.Sin(pitchRad)
                    ) * (_distance / 5);
                    _position = otstarget - offset + right * shoulderShift;
                    ViewMatrix = Matrix4x4.CreateLookAt(_position, otstarget, Vector3.UnitZ);
                }
            }
            else // Editor mode: WASD updates _position, look ahead
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