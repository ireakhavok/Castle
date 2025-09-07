// SiegeEngine.PlayerSystem/InputHandler.cs
using SiegeEngine.Interfaces;
using SiegeEngine.Networking;
using Silk.NET.GLFW;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace SiegeEngine.PlayerSystem
{
    public unsafe class InputHandler
    {
        private readonly IControlContext _controlContext;
        private readonly WindowHandle* _window;
        private readonly SteamEngine _steamEngine;
        private Vector2 _mousePos;
        private bool _mouseDown;
        private bool _mouseReleased;
        private readonly List<(string Id, Action<MouseButton, InputAction> Callback)> _mouseCallbacks = new();
        private readonly List<(string Id, Action<Keys, InputAction> Callback)> _keyCallbacks = new();

        public InputHandler(IControlContext controlContext, WindowHandle* window, SteamEngine steamEngine)
        {
            if (controlContext == null) throw new ArgumentNullException(nameof(controlContext));
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (steamEngine == null) throw new ArgumentNullException(nameof(steamEngine));

            _controlContext = controlContext;
            _window = window;
            _steamEngine = steamEngine;

            SetupInputCallbacks();
        }

        public Vector2 MousePosition => _mousePos;
        public bool MouseDown => _mouseDown;
        public bool MouseReleased => _mouseReleased;

        public void ResetMouseReleased()
        {
            _mouseReleased = false;
        }

        public void SetMouseCallback(string id, Action<MouseButton, InputAction> callback)
        {
            if (callback != null && !_mouseCallbacks.Exists(c => c.Id == id))
            {
                _mouseCallbacks.Add((id, callback));
                Console.WriteLine($"InputHandler: Added mouse callback with ID: {id}");
            }
            else
            {
                Console.WriteLine($"InputHandler: Failed to add mouse callback with ID: {id} (null or duplicate)");
            }
        }

        public void SetKeyCallback(string id, Action<Keys, InputAction> callback)
        {
            if (callback != null && !_keyCallbacks.Exists(c => c.Id == id))
            {
                _keyCallbacks.Add((id, callback));
                Console.WriteLine($"InputHandler: Added key callback with ID: {id}");
            }
            else
            {
                Console.WriteLine($"InputHandler: Failed to add key callback with ID: {id} (null or duplicate)");
            }
        }

        private void SetupInputCallbacks()
        {
            _controlContext.SetCursorPosCallback(_window, (w, x, y) =>
            {
                _mousePos = new Vector2((float)x, (float)y);
                SendMousePosition();
            });

            _controlContext.SetMouseButtonCallback(_window, (w, button, action, mods) =>
            {
                Console.WriteLine($"InputHandler: Mouse callback - Button: {button}, Action: {action}, Pos: {_mousePos}, Callbacks: {_mouseCallbacks.Count}");
                if (button == MouseButton.Left)
                {
                    if (action == InputAction.Press)
                    {
                        _mouseDown = true;
                        _mouseReleased = false;
                    }
                    else if (action == InputAction.Release)
                    {
                        _mouseDown = false;
                        _mouseReleased = true;
                    }
                }
                SendMouseButton(button, action);
                foreach (var (id, callback) in _mouseCallbacks)
                {
                    try
                    {
                        Console.WriteLine($"InputHandler: Invoking mouse callback with ID: {id}");
                        callback?.Invoke(button, action);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"InputHandler: Error invoking mouse callback with ID: {id}, Exception: {ex.Message}");
                    }
                }
            });

            _controlContext.SetKeyCallback(_window, (w, key, scancode, action, mods) =>
            {
                Console.WriteLine($"InputHandler: Key callback - Key: {key}, Action: {action}, Callbacks: {_keyCallbacks.Count}");
                SendKeyInput(key, action);
                foreach (var (id, callback) in _keyCallbacks)
                {
                    try
                    {
                        Console.WriteLine($"InputHandler: Invoking key callback with ID: {id}");
                        callback?.Invoke(key, action);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"InputHandler: Error invoking key callback with ID: {id}, Exception: {ex.Message}");
                    }
                }
            });

            Console.WriteLine("InputHandler: Registered mouse and key callbacks");
        }

        private void SendMousePosition()
        {
            string message = $"Input:MousePosition:{_mousePos.X}:{_mousePos.Y}:{_steamEngine.GetSteamId()}";
            byte[] data = Encoding.UTF8.GetBytes(message);
            _steamEngine.SendP2PMessage(data);
            Console.WriteLine($"InputHandler: Sent mouse position over Steam network: {message}");
        }

        private void SendMouseButton(MouseButton button, InputAction action)
        {
            string message = $"Input:MouseButton:{(int)button}:{(int)action}:{_steamEngine.GetSteamId()}";
            byte[] data = Encoding.UTF8.GetBytes(message);
            _steamEngine.SendP2PMessage(data);
            Console.WriteLine($"InputHandler: Sent mouse button input over Steam network: {message}");
        }

        private void SendKeyInput(Keys key, InputAction action)
        {
            string message = $"Input:Key:{(int)key}:{(int)action}:{_steamEngine.GetSteamId()}";
            byte[] data = Encoding.UTF8.GetBytes(message);
            _steamEngine.SendP2PMessage(data);
            Console.WriteLine($"InputHandler: Sent key input over Steam network: {message}");
        }
    }
}