// SiegeEngine.Interfaces/IControlContext.cs
using Silk.NET.GLFW;
using System;

namespace SiegeEngine.ContextManagement
{
    public unsafe interface IControlContext
    {
        public delegate void CursorPosCallback(WindowHandle* window, double xpos, double ypos);
        public delegate void MouseButtonCallback(WindowHandle* window, MouseButton button, InputAction action, KeyModifiers mods);
        public delegate void KeyCallback(WindowHandle* window, Keys key, int scancode, InputAction action, KeyModifiers mods);
        public delegate void ScrollCallback(WindowHandle* window, double xoffset, double yoffset);
        public delegate void WindowSizeCallback(WindowHandle* window, int width, int height);

        void SetCursorPosCallback(WindowHandle* window, CursorPosCallback callback);
        void SetMouseButtonCallback(WindowHandle* window, MouseButtonCallback callback);
        void SetKeyCallback(WindowHandle* window, KeyCallback callback);
        void SetScrollCallback(WindowHandle* window, ScrollCallback callback);
        void SetWindowSizeCallback(WindowHandle* window, WindowSizeCallback callback);
        void GetCursorPos(WindowHandle* window, out double xpos, out double ypos);
        void SetInputMode(WindowHandle* window, CursorStateAttribute attrib, CursorModeValue value);
        InputAction GetKey(WindowHandle* window, Keys key);
        InputAction GetMouseButton(WindowHandle* window, MouseButton button);
        bool WindowShouldClose(WindowHandle* window);
        bool GetWindowAttrib(WindowHandle* window, WindowAttributeGetter attrib);
        void PollEvents();
        void SwapBuffers(WindowHandle* window);
    }
}