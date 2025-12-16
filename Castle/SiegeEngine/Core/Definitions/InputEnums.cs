using System;

namespace SiegeEngine.Core.Definitions
{
    public enum Key
    {
        Unknown,
        A,
        B,
        C,
        D,
        E,
        F,
        G,
        H,
        I,
        J,
        K,
        L,
        M,
        N,
        O,
        P,
        Q,
        R,
        S,
        T,
        U,
        V,
        W,
        X,
        Y,
        Z,
        Key0,
        Key1,
        Key2,
        Key3,
        Key4,
        Key5,
        Key6,
        Key7,
        Key8,
        Key9,
        Escape,
        Enter,
        Tab,
        Backspace,
        Insert,
        Delete,
        Right,
        Left,
        Down,
        Up,
        PageUp,
        PageDown,
        Home,
        End,
        CapsLock,
        ScrollLock,
        NumLock,
        PrintScreen,
        Pause,
        F1,
        F2,
        F3,
        F4,
        F5,
        F6,
        F7,
        F8,
        F9,
        F10,
        F11,
        F12,
        KeyPad0,
        KeyPad1,
        KeyPad2,
        KeyPad3,
        KeyPad4,
        KeyPad5,
        KeyPad6,
        KeyPad7,
        KeyPad8,
        KeyPad9,
        KeyPadDecimal,
        KeyPadDivide,
        KeyPadMultiply,
        KeyPadSubtract,
        KeyPadAdd,
        KeyPadEnter,
        KeyPadEqual,
        LeftShift,
        LeftControl,
        LeftAlt,
        LeftSuper,
        RightShift,
        RightControl,
        RightAlt,
        RightSuper,
        Menu,
        Space,
        Apostrophe,
        Comma,
        Minus,
        Period,
        Slash,
        Semicolon,
        Equal,
        LeftBracket,
        Backslash,
        RightBracket,
        GraveAccent,
        World1,
        World2
    }
    public enum MouseButton
    {
        Left,
        Right,
        Middle
    }
    public enum InputAction
    {
        Release,
        Press,
        Repeat
    }
    [Flags]
    public enum KeyModifiers
    {
        None = 0,
        Shift = 1,
        Control = 2,
        Alt = 4,
        Super = 8
    }
    public enum CursorAttribute
    {
        Cursor
    }
    public enum CursorMode
    {
        Normal,
        Disabled
    }
    public enum WindowAttribute
    {
        Focused
    }
}