using System;
using System.Runtime.InteropServices;

namespace SiegeEngine.Core.GPU
{
    /// <summary>
    /// Provides P/Invoke wrappers for Windows API functions related to window management.
    /// </summary>
    public static class WindowNative
    {
        private const string User32Dll = "user32.dll";

        // Window styles
        public const uint WS_OVERLAPPEDWINDOW = 0x00000000 | 0x00C00000 | 0x00080000 | 0x00040000 | 0x00020000 | 0x00010000;
        public const uint WS_VISIBLE = 0x10000000;
        public const uint CW_USEDEFAULT = 0x80000000;

        // Show window commands
        public const int SW_SHOWNORMAL = 1;

        /// <summary>
        /// Creates a new window or an extended window.
        /// </summary>
        /// <param name="dwExStyle">The extended window style.</param>
        /// <param name="lpClassName">The window class name.</param>
        /// <param name="lpWindowName">The window name.</param>
        /// <param name="dwStyle">The window style.</param>
        /// <param name="x">The initial x position of the window.</param>
        /// <param name="y">The initial y position of the window.</param>
        /// <param name="nWidth">The width of the window.</param>
        /// <param name="nHeight">The height of the window.</param>
        /// <param name="hWndParent">The handle to the parent window.</param>
        /// <param name="hMenu">The handle to the menu.</param>
        /// <param name="hInstance">The handle to the instance.</param>
        /// <param name="lpParam">Additional application data.</param>
        /// <returns>The handle to the new window.</returns>
        [DllImport(User32Dll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern nint CreateWindowExW(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x, int y,
            int nWidth, int nHeight,
            nint hWndParent,
            nint hMenu,
            nint hInstance,
            nint lpParam);

        /// <summary>
        /// Shows or hides a window.
        /// </summary>
        /// <param name="hWnd">The handle to the window.</param>
        /// <param name="nCmdShow">The show command.</param>
        /// <returns>True if the window was previously visible; false otherwise.</returns>
        [DllImport(User32Dll, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ShowWindow(nint hWnd, int nCmdShow);

        /// <summary>
        /// Updates the client area of the specified window.
        /// </summary>
        /// <param name="hWnd">The handle to the window.</param>
        /// <returns>True if the update was successful; false otherwise.</returns>
        [DllImport(User32Dll, CallingConvention = CallingConvention.StdCall)]
        public static extern bool UpdateWindow(nint hWnd);

        /// <summary>
        /// Destroys the specified window.
        /// </summary>
        /// <param name="hWnd">The handle to the window.</param>
        /// <returns>True if the window was destroyed; false otherwise.</returns>
        [DllImport(User32Dll, CallingConvention = CallingConvention.StdCall)]
        public static extern bool DestroyWindow(nint hWnd);

        /// <summary>
        /// Registers a window class for use in creating windows.
        /// </summary>
        /// <param name="lpWndClass">The window class structure.</param>
        /// <returns>The atom identifying the class, or zero if the function fails.</returns>
        [DllImport(User32Dll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern short RegisterClassW(ref WNDCLASSW lpWndClass);

        /// <summary>
        /// Provides the default window procedure.
        /// </summary>
        /// <param name="hWnd">The handle to the window.</param>
        /// <param name="msg">The message.</param>
        /// <param name="wParam">Additional message information.</param>
        /// <param name="lParam">Additional message information.</param>
        /// <returns>The result of the message processing.</returns>
        [DllImport(User32Dll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

        /// <summary>
        /// Retrieves a message from the message queue.
        /// </summary>
        /// <param name="lpMsg">The message structure to fill.</param>
        /// <param name="hWnd">The handle to the window whose messages are to be retrieved.</param>
        /// <param name="wMsgFilterMin">The minimum message value to retrieve.</param>
        /// <param name="wMsgFilterMax">The maximum message value to retrieve.</param>
        /// <returns>True if a message is retrieved; false if the message is WM_QUIT.</returns>
        [DllImport(User32Dll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern bool GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        /// <summary>
        /// Translates virtual-key messages into character messages.
        /// </summary>
        /// <param name="lpMsg">The message to translate.</param>
        /// <returns>True if the message was translated; false otherwise.</returns>
        [DllImport(User32Dll, CallingConvention = CallingConvention.StdCall)]
        public static extern bool TranslateMessage(ref MSG lpMsg);

        /// <summary>
        /// Dispatches a message to a window procedure.
        /// </summary>
        /// <param name="lpMsg">The message to dispatch.</param>
        /// <returns>The result of the message processing.</returns>
        [DllImport(User32Dll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern nint DispatchMessageW(ref MSG lpMsg);

        /// <summary>
        /// Defines the window class structure for Windows API.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSW
        {
            public uint style;
            public nint lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public nint hInstance;
            public nint hIcon;
            public nint hCursor;
            public nint hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
        }

        /// <summary>
        /// Defines the message structure for Windows API.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public nint hwnd;
            public uint message;
            public nint wParam;
            public nint lParam;
            public uint time;
            public POINT pt;
        }

        /// <summary>
        /// Defines a point structure for Windows API.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        /// <summary>
        /// Defines the delegate for a window procedure.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate nint WNDPROC(nint hWnd, uint msg, nint wParam, nint lParam);
    }
}