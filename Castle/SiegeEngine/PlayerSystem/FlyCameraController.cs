using System;
using System.Numerics;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
namespace SiegeEngine.Scenes
{
    public class FlyCameraController
    {
        private readonly IControlContext _controlContext;
        private readonly IntPtr _window;
        private float _yaw = 0f;
        private float _pitch = 0f;
        private readonly float _xSpeed = 2.0f;
        private readonly float _ySpeed = 2.0f;
        private readonly float _pitchMinLimit = -89f;
        private readonly float _pitchMaxLimit = 89f;
        private Vector2 _lastMousePos = Vector2.Zero;
        private bool _firstMouseMove = true;
        private Vector3 _position = new Vector3(64, 36, 5);
        public Vector3 Position { get => _position; set { _position = value; UpdateCamera(); } }
        public Matrix4x4 ViewMatrix { get; private set; }
        public float Yaw { get => _yaw; set { _yaw = value; UpdateCamera(); } }
        public float Pitch { get => _pitch; set { _pitch = value; UpdateCamera(); } }
        public FlyCameraController(IControlContext controlContext, IntPtr window)
        {
            _controlContext = controlContext ?? throw new ArgumentNullException(nameof(controlContext));
            _window = window;
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
            _controlContext.SetInputMode(_window, CursorAttribute.Cursor, isGameActive ? CursorMode.Disabled : CursorMode.Normal);
            _controlContext.GetCursorPos(_window, out double mouseX, out double mouseY);
            Vector2 mousePos = new Vector2((float)mouseX, (float)mouseY);
            Vector2 delta = Vector2.Zero;
            if (isGameActive)
            {
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
                _controlContext.GetWindowSize(_window, out int w, out int h);
                _controlContext.SetCursorPos(_window, w / 2.0, h / 2.0);
                _lastMousePos = new Vector2(w / 2f, h / 2f);
            }
            else
            {
                _firstMouseMove = true;
            }
            float moveSpeed = 200.0f * deltaTime;
            if (_controlContext.GetKey(_window, Key.LeftShift) == InputAction.Press)
            {
                moveSpeed *= 10f;
            }
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
            UpdateCamera();
        }
        private void UpdateCamera()
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