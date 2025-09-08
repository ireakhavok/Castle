// SiegeEngine.Definitions/InputEnums.cs
using System;

namespace SiegeEngine.Definitions
{
    public enum Key
    {
        Unknown,
        A,
        D,
        G,
        P,
        S,
        W,
        Tab,
        Space,
        LeftControl,
        LeftShift
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