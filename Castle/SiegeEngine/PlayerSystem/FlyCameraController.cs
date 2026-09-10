// Folder: SiegeEngine/PlayerSystem
// File: FlyCameraController.cs
using System;
using System.Numerics;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Scenes
{
    public class FlyCameraController
    {
        private readonly IControlContext _controlContext;
        private readonly IntPtr _window;
        private float _yaw = 0f;
        private float _pitch = 0f;
        private readonly float _pitchMinLimit = -89f;
        private readonly float _pitchMaxLimit = 89f;
        private Vector2 _lastMousePos = Vector2.Zero;
        private bool _firstMouseMove = true;
        private bool _wasGameActive;
        private CursorMode _lastCursorMode = CursorMode.Normal;
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
        public void ResetLookDelta()
        {
            _firstMouseMove = true;
        }
        public void Update(float deltaTime, float scrollDelta, bool isGameActive)
        {
            bool focused = _controlContext.GetWindowAttrib(_window, WindowAttribute.Focused);
            if (!focused)
            {
                _controlContext.SetInputMode(_window, CursorAttribute.Cursor, CursorMode.Normal);
                ResetLookDelta();
                _wasGameActive = false;
                _lastCursorMode = CursorMode.Normal;
                UpdateCamera();
                return;
            }
            CursorMode wantCursor = isGameActive ? CursorMode.Disabled : CursorMode.Normal;
            if (wantCursor != _lastCursorMode)
            {
                ResetLookDelta();
                _lastCursorMode = wantCursor;
            }
            if (isGameActive != _wasGameActive)
            {
                ResetLookDelta();
                _wasGameActive = isGameActive;
            }
            _controlContext.SetInputMode(_window, CursorAttribute.Cursor, wantCursor);
            _controlContext.GetCursorPos(_window, out double mouseX, out double mouseY);
            Vector2 mousePos = new Vector2((float)mouseX, (float)mouseY);
            if (!isGameActive)
            {
                _lastMousePos = mousePos;
                ResetLookDelta();
                UpdateCamera();
                return;
            }
            Vector2 center = _controlContext.GetCurrentViewport().Center;
            Vector2 delta = Vector2.Zero;
            if (_firstMouseMove)
            {
                _firstMouseMove = false;
            }
            else
            {
                delta = mousePos - center;
            }
            _controlContext.SetCursorPos(_window, center.X, center.Y);
            _lastMousePos = center;
            _yaw += delta.X * RuntimeSettings.Current.LookX;
            _pitch -= delta.Y * RuntimeSettings.Current.LookY;
            _pitch = Math.Clamp(_pitch, _pitchMinLimit, _pitchMaxLimit);
            float moveSpeed = 200.0f * deltaTime;
            if (_controlContext.GetKey(_window, Key.LeftShift) == InputAction.Press) { moveSpeed *= 10f; }
            float yawRad = _yaw * (float)(Math.PI / 180);
            float pitchRad = _pitch * (float)(Math.PI / 180);
            Vector3 forward = Vector3.Normalize(new Vector3((float)Math.Cos(pitchRad) * (float)Math.Sin(yawRad), (float)Math.Cos(pitchRad) * (float)Math.Cos(yawRad), (float)Math.Sin(pitchRad)));
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
            if (_controlContext.GetKey(_window, Key.W) == InputAction.Press) _position += forward * moveSpeed;
            if (_controlContext.GetKey(_window, Key.S) == InputAction.Press) _position -= forward * moveSpeed;
            if (_controlContext.GetKey(_window, Key.A) == InputAction.Press) _position -= right * moveSpeed;
            if (_controlContext.GetKey(_window, Key.D) == InputAction.Press) _position += right * moveSpeed;
            if (_controlContext.GetKey(_window, Key.Space) == InputAction.Press) _position += Vector3.UnitZ * moveSpeed;
            if (_controlContext.GetKey(_window, Key.LeftControl) == InputAction.Press
                || _controlContext.GetKey(_window, Key.RightControl) == InputAction.Press)
                _position -= Vector3.UnitZ * moveSpeed;
            UpdateCamera();
        }
        public void UpdateCamera()
        {
            float yawRad = _yaw * (float)(Math.PI / 180);
            float pitchRad = _pitch * (float)(Math.PI / 180);
            Vector3 direction = new Vector3((float)Math.Cos(pitchRad) * (float)Math.Sin(yawRad), (float)Math.Cos(pitchRad) * (float)Math.Cos(yawRad), (float)Math.Sin(pitchRad));
            ViewMatrix = Matrix4x4.CreateLookAt(_position, _position + direction, Vector3.UnitZ);
        }
        public void RefreshViewMatrix()
        {
            UpdateCamera();
        }
    }
}
