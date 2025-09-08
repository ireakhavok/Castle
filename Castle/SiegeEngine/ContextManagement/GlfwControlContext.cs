// SiegeEngine.ContextManagement/GlfwControlContext.cs
using System;
using SiegeEngine.Definitions;
using Silk.NET.GLFW;

namespace SiegeEngine.ContextManagement
{
    public unsafe class GlfwControlContext : IControlContext
    {
        private readonly Glfw _glfw;
        public GlfwControlContext(Glfw glfw)
        {
            _glfw = glfw ?? throw new ArgumentNullException(nameof(glfw));
        }

        public void SetCursorPosCallback(IntPtr window, IControlContext.CursorPosCallback callback)
        {
            _glfw.SetCursorPosCallback((WindowHandle*)window, (w, x, y) => callback((IntPtr)w, x, y));
        }

        public void SetMouseButtonCallback(IntPtr window, IControlContext.MouseButtonCallback callback)
        {
            _glfw.SetMouseButtonCallback((WindowHandle*)window, (w, rawButton, rawAction, rawMods) =>
            {
                Definitions.MouseButton engineButton = MapGlfwMouseButton(rawButton);
                Definitions.InputAction engineAction = MapGlfwInputAction(rawAction);
                Definitions.KeyModifiers engineMods = MapGlfwMods(rawMods);
                callback((IntPtr)w, engineButton, engineAction, engineMods);
            });
        }

        public void SetKeyCallback(IntPtr window, IControlContext.KeyCallback callback)
        {
            _glfw.SetKeyCallback((WindowHandle*)window, (w, rawKey, scancode, rawAction, rawMods) =>
            {
                Key engineKey = MapGlfwKey(rawKey);
                Definitions.InputAction engineAction = MapGlfwInputAction(rawAction);
                Definitions.KeyModifiers engineMods = MapGlfwMods(rawMods);
                callback((IntPtr)w, engineKey, scancode, engineAction, engineMods);
            });
        }

        public void SetScrollCallback(IntPtr window, IControlContext.ScrollCallback callback)
        {
            _glfw.SetScrollCallback((WindowHandle*)window, (w, x, y) => callback((IntPtr)w, x, y));
        }

        public void SetWindowSizeCallback(IntPtr window, IControlContext.WindowSizeCallback callback)
        {
            _glfw.SetWindowSizeCallback((WindowHandle*)window, (w, width, height) => callback((IntPtr)w, width, height));
        }

        public void GetCursorPos(IntPtr window, out double xpos, out double ypos)
        {
            _glfw.GetCursorPos((WindowHandle*)window, out xpos, out ypos);
        }

        public void SetInputMode(IntPtr window, CursorAttribute attrib, CursorMode value)
        {
            CursorStateAttribute glfwAttrib = CursorStateAttribute.Cursor;
            CursorModeValue glfwValue = value switch
            {
                CursorMode.Normal => CursorModeValue.CursorNormal,
                CursorMode.Disabled => CursorModeValue.CursorDisabled,
                _ => CursorModeValue.CursorNormal
            };
            _glfw.SetInputMode((WindowHandle*)window, glfwAttrib, glfwValue);
        }

        public Definitions.InputAction GetKey(IntPtr window, Key key)
        {
            Keys glfwKey = MapEngineKey(key);
            return MapGlfwInputAction(_glfw.GetKey((WindowHandle*)window, glfwKey));
        }

        public Definitions.InputAction GetMouseButton(IntPtr window, Definitions.MouseButton button)
        {
            Silk.NET.GLFW.MouseButton glfwButton = MapEngineMouseButton(button);
            return MapGlfwInputAction(_glfw.GetMouseButton((WindowHandle*)window, glfwButton));
        }

        public bool WindowShouldClose(IntPtr window)
        {
            return _glfw.WindowShouldClose((WindowHandle*)window);
        }

        public bool GetWindowAttrib(IntPtr window, WindowAttribute attrib)
        {
            WindowAttributeGetter glfwAttrib = attrib switch
            {
                WindowAttribute.Focused => WindowAttributeGetter.Focused,
                _ => WindowAttributeGetter.Focused
            };
            return _glfw.GetWindowAttrib((WindowHandle*)window, glfwAttrib);
        }

        public void PollEvents()
        {
            _glfw.PollEvents();
        }

        public void SwapBuffers(IntPtr window)
        {
            _glfw.SwapBuffers((WindowHandle*)window);
        }

        private static Key MapGlfwKey(Keys glfwKey)
        {
            return glfwKey switch
            {
                Keys.A => Key.A,
                Keys.D => Key.D,
                Keys.G => Key.G,
                Keys.P => Key.P,
                Keys.S => Key.S,
                Keys.W => Key.W,
                Keys.Tab => Key.Tab,
                Keys.Space => Key.Space,
                Keys.ControlLeft => Key.LeftControl,
                Keys.ShiftLeft => Key.LeftShift,
                _ => Key.Unknown
            };
        }

        private static Keys MapEngineKey(Key engineKey)
        {
            return engineKey switch
            {
                Key.A => Keys.A,
                Key.D => Keys.D,
                Key.G => Keys.G,
                Key.P => Keys.P,
                Key.S => Keys.S,
                Key.W => Keys.W,
                Key.Tab => Keys.Tab,
                Key.Space => Keys.Space,
                Key.LeftControl => Keys.ControlLeft,
                Key.LeftShift => Keys.ShiftLeft,
                _ => Keys.Unknown
            };
        }

        private static Definitions.MouseButton MapGlfwMouseButton(Silk.NET.GLFW.MouseButton glfwButton)
        {
            return glfwButton switch
            {
                Silk.NET.GLFW.MouseButton.Left => Definitions.MouseButton.Left,
                Silk.NET.GLFW.MouseButton.Right => Definitions.MouseButton.Right,
                Silk.NET.GLFW.MouseButton.Middle => Definitions.MouseButton.Middle,
                _ => Definitions.MouseButton.Left
            };
        }

        private static Silk.NET.GLFW.MouseButton MapEngineMouseButton(Definitions.MouseButton engineButton)
        {
            return engineButton switch
            {
                Definitions.MouseButton.Left => Silk.NET.GLFW.MouseButton.Left,
                Definitions.MouseButton.Right => Silk.NET.GLFW.MouseButton.Right,
                Definitions.MouseButton.Middle => Silk.NET.GLFW.MouseButton.Middle,
                _ => Silk.NET.GLFW.MouseButton.Left
            };
        }

        private static Definitions.KeyModifiers MapGlfwMods(Silk.NET.GLFW.KeyModifiers glfwMods)
        {
            Definitions.KeyModifiers engineMods = Definitions.KeyModifiers.None;
            if ((glfwMods & Silk.NET.GLFW.KeyModifiers.Shift) != 0) engineMods |= Definitions.KeyModifiers.Shift;
            if ((glfwMods & Silk.NET.GLFW.KeyModifiers.Control) != 0) engineMods |= Definitions.KeyModifiers.Control;
            if ((glfwMods & Silk.NET.GLFW.KeyModifiers.Alt) != 0) engineMods |= Definitions.KeyModifiers.Alt;
            if ((glfwMods & Silk.NET.GLFW.KeyModifiers.Super) != 0) engineMods |= Definitions.KeyModifiers.Super;
            return engineMods;
        }

        private static Definitions.InputAction MapGlfwInputAction(Silk.NET.GLFW.InputAction glfwAction)
        {
            return (Definitions.InputAction)(int)glfwAction;
        }
    }
}