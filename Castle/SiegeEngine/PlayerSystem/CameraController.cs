using System;
using System.Numerics;
using SiegeEngine.Managers;
using Silk.NET.GLFW;

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
        private readonly Glfw _glfw;
        private readonly Player _player;
        private readonly MenuManager _menuManager; // Added to check EditorMode
        private Perspective _perspective = Perspective.ThirdPerson;
        private float _yaw = 0f;
        private float _pitch = 0f;
        private float _distance = 50.0f;
        private readonly float _minDistance = 10.0f;
        private readonly float _maxDistance = 200.0f;
        private readonly float _zoomSpeed = 50.0f;
        private readonly float _xSpeed = 2.0f;
        private readonly float _ySpeed = 2.0f;
        private readonly float _pitchMinLimit = -89f;
        private readonly float _pitchMaxLimit = 89f;
        private Vector2 _lastMousePos = Vector2.Zero;
        private bool _firstMouseMove = true;
        private bool _isRightShoulder = true;
        private readonly float _shoulderShiftAmount = 5.0f;
        private readonly float _playerHeight = 10.0f;
        private bool _isPPressed = false;
        private bool _wasPPressedLastFrame = false;
        private bool _wasShiftPressedLastFrame = false;
        private bool _wasTabPressedLastFrame = false;
        private Vector3 _position;

        public Vector3 Position => _position;
        public Matrix4x4 ViewMatrix { get; private set; }
        public float Yaw => _yaw;
        public Perspective CurrentPerspective => _perspective;
        public float Pitch => _pitch;
        public Vector2 MousePosition { get; private set; }

        public CameraController(Glfw glfw, Player player = null, MenuManager menuManager = null)
        {
            _glfw = glfw ?? throw new ArgumentNullException(nameof(glfw));
            _player = player;
            _menuManager = menuManager; // Inject MenuManager
            _position = _player?.Position + new Vector3(0, 0, _playerHeight) ?? new Vector3(64, 36, 5);
            UpdateCamera();
        }

        public unsafe void Update(float deltaTime, WindowHandle* window, float scrollDelta, bool isGameActive)
        {
            // Skip update in EditorMode to prevent Menu mouse position logs
            if (_menuManager?.EditorMode == true)
            {
                Console.WriteLine("CameraController: Skipping Update in EditorMode");
                return;
            }

            bool focused = _glfw.GetWindowAttrib(window, WindowAttributeGetter.Focused);
            if (!focused)
            {
                _glfw.SetInputMode(window, CursorStateAttribute.Cursor, CursorModeValue.CursorNormal);
                _firstMouseMove = true;
                return;
            }

            _glfw.SetInputMode(window, CursorStateAttribute.Cursor,
                isGameActive ? CursorModeValue.CursorDisabled : CursorModeValue.CursorNormal);

            _glfw.GetCursorPos(window, out double mouseX, out double mouseY);
            Vector2 mousePos = new Vector2((float)mouseX, (float)mouseY);

            if (!isGameActive)
            {
                MousePosition = mousePos;
                Console.WriteLine($"Menu mouse position: {MousePosition}");
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
                    _isPPressed = _glfw.GetKey(window, Keys.P) == 1;
                    if (_isPPressed && !_wasPPressedLastFrame)
                    {
                        ChangePerspective();
                    }
                    _wasPPressedLastFrame = _isPPressed;

                    bool isShiftPressed = _glfw.GetKey(window, Keys.ShiftLeft) == 1;
                    bool isTabPressed = _glfw.GetKey(window, Keys.Tab) == 1;
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
                        (float)Math.Sin(yawRad) * (float)Math.Cos(pitchRad),
                        (float)Math.Cos(yawRad) * (float)Math.Cos(pitchRad),
                        (float)Math.Sin(pitchRad)
                    ));
                    Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));

                    if (_glfw.GetKey(window, Keys.W) == 1) _position += forward * moveSpeed;
                    if (_glfw.GetKey(window, Keys.S) == 1) _position -= forward * moveSpeed;
                    if (_glfw.GetKey(window, Keys.A) == 1) _position -= right * moveSpeed;
                    if (_glfw.GetKey(window, Keys.D) == 1) _position += right * moveSpeed;
                    if (_glfw.GetKey(window, Keys.Space) == 1) _position += Vector3.UnitZ * moveSpeed;
                    if (_glfw.GetKey(window, Keys.ControlLeft) == 1) _position -= Vector3.UnitZ * moveSpeed;
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

        private void UpdateCamera()
        {
            if (_player != null) // Player mode only
            {
                Vector3 target = _player.Position + new Vector3(0, 0, _playerHeight / 2);
                float yawRad = _yaw * (float)(Math.PI / 180);
                float pitchRad = _pitch * (float)(Math.PI / 180);

                if (_perspective == Perspective.FirstPerson)
                {
                    _position = target;
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
                    _position = target - offset;
                    ViewMatrix = Matrix4x4.CreateLookAt(_position, target, Vector3.UnitZ);
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
                    _position = target - offset + right * shoulderShift;
                    ViewMatrix = Matrix4x4.CreateLookAt(_position, target, Vector3.UnitZ);
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