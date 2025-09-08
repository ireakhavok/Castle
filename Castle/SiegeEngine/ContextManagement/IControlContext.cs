// SiegeEngine.ContextManagement/IControlContext.cs
using SiegeEngine.Definitions;
using System;

namespace SiegeEngine.ContextManagement
{
    public interface IControlContext
    {
        public delegate void CursorPosCallback(IntPtr window, double xpos, double ypos);
        public delegate void MouseButtonCallback(IntPtr window, MouseButton button, InputAction action, KeyModifiers mods);
        public delegate void KeyCallback(IntPtr window, Key key, int scancode, InputAction action, KeyModifiers mods);
        public delegate void ScrollCallback(IntPtr window, double xoffset, double yoffset);
        public delegate void WindowSizeCallback(IntPtr window, int width, int height);
        void SetCursorPosCallback(IntPtr window, CursorPosCallback callback);
        void SetMouseButtonCallback(IntPtr window, MouseButtonCallback callback);
        void SetKeyCallback(IntPtr window, KeyCallback callback);
        void SetScrollCallback(IntPtr window, ScrollCallback callback);
        void SetWindowSizeCallback(IntPtr window, WindowSizeCallback callback);
        void GetCursorPos(IntPtr window, out double xpos, out double ypos);
        void SetInputMode(IntPtr window, CursorAttribute attrib, CursorMode value);
        InputAction GetKey(IntPtr window, Key key);
        InputAction GetMouseButton(IntPtr window, MouseButton button);
        bool WindowShouldClose(IntPtr window);
        bool GetWindowAttrib(IntPtr window, WindowAttribute attrib);
        void PollEvents();
        void SwapBuffers(IntPtr window);
    }
}