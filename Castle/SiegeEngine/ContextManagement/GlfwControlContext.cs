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
            return MapGlfwInputAction((Silk.NET.GLFW.InputAction)_glfw.GetKey((WindowHandle*)window, glfwKey));
        }
        public Definitions.InputAction GetMouseButton(IntPtr window, Definitions.MouseButton button)
        {
            Silk.NET.GLFW.MouseButton glfwButton = MapEngineMouseButton(button);
            return MapGlfwInputAction((Silk.NET.GLFW.InputAction)_glfw.GetMouseButton((WindowHandle*)window, (int)glfwButton));
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
        public void GetWindowSize(IntPtr window, out int width, out int height)
        {
            _glfw.GetWindowSize((WindowHandle*)window, out width, out height);
        }
        public double GetTime()
        {
            return _glfw.GetTime();
        }
        private static Key MapGlfwKey(Keys glfwKey)
        {
            return glfwKey switch
            {
                Keys.A => Key.A,
                Keys.B => Key.B,
                Keys.C => Key.C,
                Keys.D => Key.D,
                Keys.E => Key.E,
                Keys.F => Key.F,
                Keys.G => Key.G,
                Keys.H => Key.H,
                Keys.I => Key.I,
                Keys.J => Key.J,
                Keys.K => Key.K,
                Keys.L => Key.L,
                Keys.M => Key.M,
                Keys.N => Key.N,
                Keys.O => Key.O,
                Keys.P => Key.P,
                Keys.Q => Key.Q,
                Keys.R => Key.R,
                Keys.S => Key.S,
                Keys.T => Key.T,
                Keys.U => Key.U,
                Keys.V => Key.V,
                Keys.W => Key.W,
                Keys.X => Key.X,
                Keys.Y => Key.Y,
                Keys.Z => Key.Z,
                Keys.Number0 => Key.Key0,
                Keys.Number1 => Key.Key1,
                Keys.Number2 => Key.Key2,
                Keys.Number3 => Key.Key3,
                Keys.Number4 => Key.Key4,
                Keys.Number5 => Key.Key5,
                Keys.Number6 => Key.Key6,
                Keys.Number7 => Key.Key7,
                Keys.Number8 => Key.Key8,
                Keys.Number9 => Key.Key9,
                Keys.Escape => Key.Escape,
                Keys.Enter => Key.Enter,
                Keys.Tab => Key.Tab,
                Keys.Backspace => Key.Backspace,
                Keys.Insert => Key.Insert,
                Keys.Delete => Key.Delete,
                Keys.Right => Key.Right,
                Keys.Left => Key.Left,
                Keys.Down => Key.Down,
                Keys.Up => Key.Up,
                Keys.PageUp => Key.PageUp,
                Keys.PageDown => Key.PageDown,
                Keys.Home => Key.Home,
                Keys.End => Key.End,
                Keys.CapsLock => Key.CapsLock,
                Keys.ScrollLock => Key.ScrollLock,
                Keys.NumLock => Key.NumLock,
                Keys.PrintScreen => Key.PrintScreen,
                Keys.Pause => Key.Pause,
                Keys.F1 => Key.F1,
                Keys.F2 => Key.F2,
                Keys.F3 => Key.F3,
                Keys.F4 => Key.F4,
                Keys.F5 => Key.F5,
                Keys.F6 => Key.F6,
                Keys.F7 => Key.F7,
                Keys.F8 => Key.F8,
                Keys.F9 => Key.F9,
                Keys.F10 => Key.F10,
                Keys.F11 => Key.F11,
                Keys.F12 => Key.F12,
                Keys.Keypad0 => Key.KeyPad0,
                Keys.Keypad1 => Key.KeyPad1,
                Keys.Keypad2 => Key.KeyPad2,
                Keys.Keypad3 => Key.KeyPad3,
                Keys.Keypad4 => Key.KeyPad4,
                Keys.Keypad5 => Key.KeyPad5,
                Keys.Keypad6 => Key.KeyPad6,
                Keys.Keypad7 => Key.KeyPad7,
                Keys.Keypad8 => Key.KeyPad8,
                Keys.Keypad9 => Key.KeyPad9,
                Keys.KeypadDecimal => Key.KeyPadDecimal,
                Keys.KeypadDivide => Key.KeyPadDivide,
                Keys.KeypadMultiply => Key.KeyPadMultiply,
                Keys.KeypadSubtract => Key.KeyPadSubtract,
                Keys.KeypadAdd => Key.KeyPadAdd,
                Keys.KeypadEnter => Key.KeyPadEnter,
                Keys.KeypadEqual => Key.KeyPadEqual,
                Keys.ShiftLeft => Key.LeftShift,
                Keys.ControlLeft => Key.LeftControl,
                Keys.AltLeft => Key.LeftAlt,
                Keys.SuperLeft => Key.LeftSuper,
                Keys.ShiftRight => Key.RightShift,
                Keys.ControlRight => Key.RightControl,
                Keys.AltRight => Key.RightAlt,
                Keys.SuperRight => Key.RightSuper,
                Keys.Menu => Key.Menu,
                Keys.Space => Key.Space,
                Keys.Apostrophe => Key.Apostrophe,
                Keys.Comma => Key.Comma,
                Keys.Minus => Key.Minus,
                Keys.Period => Key.Period,
                Keys.Slash => Key.Slash,
                Keys.Semicolon => Key.Semicolon,
                Keys.Equal => Key.Equal,
                Keys.LeftBracket => Key.LeftBracket,
                Keys.BackSlash => Key.Backslash,
                Keys.RightBracket => Key.RightBracket,
                Keys.GraveAccent => Key.GraveAccent,
                _ => Key.Unknown
            };
        }
        private static Keys MapEngineKey(Key engineKey)
        {
            return engineKey switch
            {
                Key.A => Keys.A,
                Key.B => Keys.B,
                Key.C => Keys.C,
                Key.D => Keys.D,
                Key.E => Keys.E,
                Key.F => Keys.F,
                Key.G => Keys.G,
                Key.H => Keys.H,
                Key.I => Keys.I,
                Key.J => Keys.J,
                Key.K => Keys.K,
                Key.L => Keys.L,
                Key.M => Keys.M,
                Key.N => Keys.N,
                Key.O => Keys.O,
                Key.P => Keys.P,
                Key.Q => Keys.Q,
                Key.R => Keys.R,
                Key.S => Keys.S,
                Key.T => Keys.T,
                Key.U => Keys.U,
                Key.V => Keys.V,
                Key.W => Keys.W,
                Key.X => Keys.X,
                Key.Y => Keys.Y,
                Key.Z => Keys.Z,
                Key.Key0 => Keys.Number0,
                Key.Key1 => Keys.Number1,
                Key.Key2 => Keys.Number2,
                Key.Key3 => Keys.Number3,
                Key.Key4 => Keys.Number4,
                Key.Key5 => Keys.Number5,
                Key.Key6 => Keys.Number6,
                Key.Key7 => Keys.Number7,
                Key.Key8 => Keys.Number8,
                Key.Key9 => Keys.Number9,
                Key.Escape => Keys.Escape,
                Key.Enter => Keys.Enter,
                Key.Tab => Keys.Tab,
                Key.Backspace => Keys.Backspace,
                Key.Insert => Keys.Insert,
                Key.Delete => Keys.Delete,
                Key.Right => Keys.Right,
                Key.Left => Keys.Left,
                Key.Down => Keys.Down,
                Key.Up => Keys.Up,
                Key.PageUp => Keys.PageUp,
                Key.PageDown => Keys.PageDown,
                Key.Home => Keys.Home,
                Key.End => Keys.End,
                Key.CapsLock => Keys.CapsLock,
                Key.ScrollLock => Keys.ScrollLock,
                Key.NumLock => Keys.NumLock,
                Key.PrintScreen => Keys.PrintScreen,
                Key.Pause => Keys.Pause,
                Key.F1 => Keys.F1,
                Key.F2 => Keys.F2,
                Key.F3 => Keys.F3,
                Key.F4 => Keys.F4,
                Key.F5 => Keys.F5,
                Key.F6 => Keys.F6,
                Key.F7 => Keys.F7,
                Key.F8 => Keys.F8,
                Key.F9 => Keys.F9,
                Key.F10 => Keys.F10,
                Key.F11 => Keys.F11,
                Key.F12 => Keys.F12,
                Key.KeyPad0 => Keys.Keypad0,
                Key.KeyPad1 => Keys.Keypad1,
                Key.KeyPad2 => Keys.Keypad2,
                Key.KeyPad3 => Keys.Keypad3,
                Key.KeyPad4 => Keys.Keypad4,
                Key.KeyPad5 => Keys.Keypad5,
                Key.KeyPad6 => Keys.Keypad6,
                Key.KeyPad7 => Keys.Keypad7,
                Key.KeyPad8 => Keys.Keypad8,
                Key.KeyPad9 => Keys.Keypad9,
                Key.KeyPadDecimal => Keys.KeypadDecimal,
                Key.KeyPadDivide => Keys.KeypadDivide,
                Key.KeyPadMultiply => Keys.KeypadMultiply,
                Key.KeyPadSubtract => Keys.KeypadSubtract,
                Key.KeyPadAdd => Keys.KeypadAdd,
                Key.KeyPadEnter => Keys.KeypadEnter,
                Key.KeyPadEqual => Keys.KeypadEqual,
                Key.LeftShift => Keys.ShiftLeft,
                Key.LeftControl => Keys.ControlLeft,
                Key.LeftAlt => Keys.AltLeft,
                Key.LeftSuper => Keys.SuperLeft,
                Key.RightShift => Keys.ShiftRight,
                Key.RightControl => Keys.ControlRight,
                Key.RightAlt => Keys.AltRight,
                Key.RightSuper => Keys.SuperRight,
                Key.Menu => Keys.Menu,
                Key.Space => Keys.Space,
                Key.Apostrophe => Keys.Apostrophe,
                Key.Comma => Keys.Comma,
                Key.Minus => Keys.Minus,
                Key.Period => Keys.Period,
                Key.Slash => Keys.Slash,
                Key.Semicolon => Keys.Semicolon,
                Key.Equal => Keys.Equal,
                Key.LeftBracket => Keys.LeftBracket,
                Key.Backslash => Keys.BackSlash,
                Key.RightBracket => Keys.RightBracket,
                Key.GraveAccent => Keys.GraveAccent,
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