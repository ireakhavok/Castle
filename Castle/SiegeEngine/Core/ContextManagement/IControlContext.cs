using SiegeEngine.Core.Definitions;
using System;
namespace SiegeEngine.Core.ContextManagement
{
    public interface IControlContext
    {
        public delegate void CursorPosCallback(nint window, double xpos, double ypos);
        public delegate void MouseButtonCallback(nint window, MouseButton button, InputAction action, KeyModifiers mods);
        public delegate void KeyCallback(nint window, Key key, int scancode, InputAction action, KeyModifiers mods);
        public delegate void ScrollCallback(nint window, double xoffset, double yoffset);
        public delegate void WindowSizeCallback(nint window, int width, int height);
        void SetCursorPosCallback(nint window, CursorPosCallback callback);
        void SetMouseButtonCallback(nint window, MouseButtonCallback callback);
        void SetKeyCallback(nint window, KeyCallback callback);
        void SetScrollCallback(nint window, ScrollCallback callback);
        void SetWindowSizeCallback(nint window, WindowSizeCallback callback);
        void GetCursorPos(nint window, out double xpos, out double ypos);
        void SetCursorPos(nint window, double xpos, double ypos);
        void SetInputMode(nint window, CursorAttribute attrib, CursorMode value);
        InputAction GetKey(nint window, Key key);
        InputAction GetMouseButton(nint window, MouseButton button);
        bool WindowShouldClose(nint window);
        bool GetWindowAttrib(nint window, WindowAttribute attrib);
        void PollEvents();
        void SwapBuffers(nint window);
        void GetWindowSize(nint window, out int width, out int height);
        double GetTime();
    }
}