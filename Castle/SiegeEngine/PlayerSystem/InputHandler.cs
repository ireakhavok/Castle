// SiegeEngine.PlayerSystem/InputHandler.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.PlayerSystem
{
    public class InputHandler
    {
        private readonly IControlContext _controlContext;
        private readonly IntPtr _window;
        private readonly SteamEngine _steamEngine;
        private Vector2 _mousePos;
        private bool _mouseDown;
        private bool _mouseReleased;
        private readonly List<(string Id, Action<MouseButton, InputAction> Callback)> _mouseCallbacks = new();
        private readonly List<(string Id, Action<Key, InputAction> Callback)> _keyCallbacks = new();

        public event Action<Key, InputAction> KeyEvent;

        public InputHandler(IControlContext controlContext, IntPtr window, SteamEngine steamEngine = null)
        {
            if (controlContext == null) throw new ArgumentNullException(nameof(controlContext));
            if (window == IntPtr.Zero) throw new ArgumentNullException(nameof(window));
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

        public void SetKeyCallback(string id, Action<Key, InputAction> callback)
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
                        //Console.WriteLine($"InputHandler: Invoking mouse callback with ID: {id}");
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
                KeyEvent?.Invoke(key, action);
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
            if (_steamEngine == null) return;
            string message = $"Input:MousePosition:{_mousePos.X}:{_mousePos.Y}:{_steamEngine.GetSteamId()}";
            byte[] data = Encoding.UTF8.GetBytes(message);
            _steamEngine.SendP2PMessage(data);
            //Console.WriteLine($"InputHandler: Sent mouse position over Steam network: {message}");
        }

        private void SendMouseButton(MouseButton button, InputAction action)
        {
            if (_steamEngine == null) return;
            string message = $"Input:MouseButton:{(int)button}:{(int)action}:{_steamEngine.GetSteamId()}";
            byte[] data = Encoding.UTF8.GetBytes(message);
            _steamEngine.SendP2PMessage(data);
            Console.WriteLine($"InputHandler: Sent mouse button input over Steam network: {message}");
        }

        private void SendKeyInput(Key key, InputAction action)
        {
            if (_steamEngine == null) return;
            string message = $"Input:Key:{(int)key}:{(int)action}:{_steamEngine.GetSteamId()}";
            byte[] data = Encoding.UTF8.GetBytes(message);
            _steamEngine.SendP2PMessage(data);
            //Console.WriteLine($"InputHandler: Sent key input over Steam network: {message}");
        }
    }
}