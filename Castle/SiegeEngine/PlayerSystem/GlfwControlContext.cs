// SiegeEngine.PlayerSystem/GlfwControlContext.cs
using Silk.NET.GLFW;
using System;

namespace SiegeEngine.PlayerSystem
{
    public unsafe class GlfwControlContext : Interfaces.IControlContext
    {
        private readonly Glfw _glfw;

        public GlfwControlContext(Glfw glfw)
        {
            _glfw = glfw ?? throw new ArgumentNullException(nameof(glfw));
        }

        public void SetCursorPosCallback(WindowHandle* window, Interfaces.IControlContext.CursorPosCallback callback)
        {
            _glfw.SetCursorPosCallback(window, (w, x, y) => callback(w, x, y));
        }

        public void SetMouseButtonCallback(WindowHandle* window, Interfaces.IControlContext.MouseButtonCallback callback)
        {
            _glfw.SetMouseButtonCallback(window, (w, rawButton, rawAction, rawMods) =>
            {
                callback(w, (MouseButton)rawButton, (InputAction)rawAction, (KeyModifiers)rawMods);
            });
        }

        public void SetKeyCallback(WindowHandle* window, Interfaces.IControlContext.KeyCallback callback)
        {
            _glfw.SetKeyCallback(window, (w, rawKey, scancode, rawAction, rawMods) =>
            {
                callback(w, (Keys)rawKey, scancode, (InputAction)rawAction, (KeyModifiers)rawMods);
            });
        }

        public void SetScrollCallback(WindowHandle* window, Interfaces.IControlContext.ScrollCallback callback)
        {
            _glfw.SetScrollCallback(window, (w, x, y) => callback(w, x, y));
        }

        public void SetWindowSizeCallback(WindowHandle* window, Interfaces.IControlContext.WindowSizeCallback callback)
        {
            _glfw.SetWindowSizeCallback(window, (w, width, height) => callback(w, width, height));
        }

        public void GetCursorPos(WindowHandle* window, out double xpos, out double ypos)
        {
            _glfw.GetCursorPos(window, out xpos, out ypos);
        }

        public void SetInputMode(WindowHandle* window, CursorStateAttribute attrib, CursorModeValue value)
        {
            _glfw.SetInputMode(window, attrib, value);
        }

        public InputAction GetKey(WindowHandle* window, Keys key)
        {
            return (InputAction)_glfw.GetKey(window, key);
        }

        public InputAction GetMouseButton(WindowHandle* window, MouseButton button)
        {
            return (InputAction)_glfw.GetMouseButton(window, (int)button);
        }

        public bool WindowShouldClose(WindowHandle* window)
        {
            return _glfw.WindowShouldClose(window);
        }

        public bool GetWindowAttrib(WindowHandle* window, WindowAttributeGetter attrib)
        {
            return _glfw.GetWindowAttrib(window, attrib);
        }

        public void PollEvents()
        {
            _glfw.PollEvents();
        }

        public void SwapBuffers(WindowHandle* window)
        {
            _glfw.SwapBuffers(window);
        }
    }
}