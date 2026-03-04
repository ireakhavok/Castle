// Folder: SiegeEngine.Core.UI
// File: UIInteractionLayer.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.UI.JSParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    public class UIInteractionLayer
    {
        private readonly UIOverlay _overlay;
        private readonly IControlContext _controlContext;
        private readonly nint _window;
        private bool _prevMouseDown = false;
        public bool _justOpenedSelect = false;
        public List<SelectElement> _openSelects = new List<SelectElement>();
        public HtmlElement _currentFocused;
        private readonly Dictionary<Key, double> _keyDownTime = new Dictionary<Key, double>();
        private readonly Dictionary<Key, double> _lastAddTime = new Dictionary<Key, double>();
        private const double InitialRepeatDelay = 0.5;
        private const double RepeatRate = 0.05;

        public UIInteractionLayer(UIOverlay overlay, IControlContext controlContext, nint window)
        {
            _overlay = overlay;
            _controlContext = controlContext;
            _window = window;
        }

        public void Update(float deltaTime, Vector2 relMousePos, bool currentMouseDown, float panelW, float panelH)
        {
            if (_overlay._uiRoot == null) return;
            _overlay.DidHandleClick = false;
            _overlay.PanelWidth = panelW;
            _overlay.PanelHeight = panelH;
            Vector2 scrolledMousePos = new Vector2(relMousePos.X, relMousePos.Y + _overlay.ScrollOffsetY);
            bool mousePress = !_prevMouseDown && currentMouseDown;
            bool mouseRelease = _prevMouseDown && !currentMouseDown;
            float vw = panelW;
            float vh = panelH;
            HtmlElement clickedElem = null;
            bool isClickOnOpenSelect = false;
            _openSelects = _overlay.FindElementsByTag("select").Where(s => (s as SelectElement)?.IsOpen ?? false).Cast<SelectElement>().ToList();
            SelectElement openSelect = _openSelects.FirstOrDefault();
            if (openSelect != null)
            {
                if (openSelect.HandleClick(scrolledMousePos, vw, vh))
                {
                    isClickOnOpenSelect = true;
                }
            }

            // === HOVER PASS FIRST (new clean lifecycle) ===
            var clickablesSnapshot = _overlay._uiClickables.ToList();
            foreach (var clickable in clickablesSnapshot)
            {
                clickable.UpdateHover(scrolledMousePos, vw, vh);
            }

            // === CLICK PASS (only real clicks) ===
            foreach (var clickable in clickablesSnapshot)
            {
                if (openSelect != null && !clickable.IsDescendantOf(openSelect) && !(clickable == openSelect))
                {
                    continue;
                }
                bool wasActive = clickable.IsActive;
                bool over = clickable.IsHover;
                if (over && mousePress)
                {
                    if (!string.IsNullOrEmpty(clickable.OnMouseDownJS))
                    {
                        _overlay._jsContext.RunWithThis(clickable.OnMouseDownJS, new JSElement(clickable, _overlay));
                    }
                    _overlay.InvokeListeners(clickable, "mousedown");
                    clickable.IsActive = true;
                }
                if (over && mouseRelease)
                {
                    if (!string.IsNullOrEmpty(clickable.OnMouseUpJS))
                    {
                        _overlay._jsContext.RunWithThis(clickable.OnMouseUpJS, new JSElement(clickable, _overlay));
                    }
                    _overlay.InvokeListeners(clickable, "mouseup");
                }
                if (over && mouseRelease && wasActive)
                {
                    clickedElem = clickable;
                }
                if (mouseRelease)
                {
                    clickable.IsActive = false;
                }
            }

            if (clickedElem != null)
            {
                _overlay.DidHandleClick = true;
                bool focusable = clickedElem.Tag.ToLower() == "input" || clickedElem.Tag.ToLower() == "select" || clickedElem.Tag.ToLower() == "button" || clickedElem.Attributes.ContainsKey("tabindex") || !string.IsNullOrEmpty(clickedElem.OnFocusJS) || !string.IsNullOrEmpty(clickedElem.OnBlurJS);
                if (focusable)
                {
                    if (_currentFocused != null && _currentFocused != clickedElem)
                    {
                        if (!string.IsNullOrEmpty(_currentFocused.OnBlurJS))
                        {
                            _overlay._jsContext.RunWithThis(_currentFocused.OnBlurJS, new JSElement(_currentFocused, _overlay));
                        }
                        _overlay.InvokeListeners(_currentFocused, "blur");
                        _currentFocused.IsFocused = false;
                    }
                    if (!clickedElem.IsFocused)
                    {
                        if (!string.IsNullOrEmpty(clickedElem.OnFocusJS))
                        {
                            _overlay._jsContext.RunWithThis(clickedElem.OnFocusJS, new JSElement(clickedElem, _overlay));
                        }
                        _overlay.InvokeListeners(clickedElem, "focus");
                        clickedElem.IsFocused = true;
                        _currentFocused = clickedElem;
                    }
                }
                _overlay.HandleUIClick(clickedElem);
            }
            else if (mouseRelease && openSelect != null && !isClickOnOpenSelect && !_justOpenedSelect)
            {
                _overlay.CloseAllOpenSelects();
                _overlay.RefreshUI();
            }
            _justOpenedSelect = false;
            _prevMouseDown = currentMouseDown;

            bool needsRefresh = false;
            bool changed = false;
            if (_currentFocused is InputElement input && (input.Type == "text" || input.Type == "number"))
            {
                bool shiftPressed = _controlContext.GetKey(_window, Key.LeftShift) == InputAction.Press ||
                                    _controlContext.GetKey(_window, Key.RightShift) == InputAction.Press;
                double currentTime = _controlContext.GetTime();
                changed = false;
                foreach (Key key in Enum.GetValues(typeof(Key)))
                {
                    InputAction state = _controlContext.GetKey(_window, key);
                    if (state == InputAction.Press)
                    {
                        if (!_keyDownTime.ContainsKey(key))
                        {
                            _keyDownTime[key] = currentTime;
                            _lastAddTime[key] = currentTime;
                            if (key == Key.Backspace)
                            {
                                if (input.Value.Length > 0)
                                {
                                    input.Value = input.Value.Substring(0, input.Value.Length - 1);
                                    changed = true;
                                }
                            }
                            else
                            {
                                char? ch = InputElement.GetCharFromKey(key, shiftPressed, input.Type);
                                if (ch.HasValue)
                                {
                                    if (input.Type == "number")
                                    {
                                        if (ch == '.' && input.Value.Contains('.')) continue;
                                        if (ch == '-' && input.Value.Length > 0 && !input.Value.StartsWith("-")) continue;
                                        if (ch == '-' && input.Value.StartsWith("-")) continue;
                                    }
                                    input.Value += ch.Value;
                                    changed = true;
                                }
                            }
                        }
                        else if (currentTime - _keyDownTime[key] > InitialRepeatDelay && currentTime - _lastAddTime[key] > RepeatRate)
                        {
                            _lastAddTime[key] = currentTime;
                            if (key == Key.Backspace)
                            {
                                if (input.Value.Length > 0)
                                {
                                    input.Value = input.Value.Substring(0, input.Value.Length - 1);
                                    changed = true;
                                }
                            }
                            else
                            {
                                char? ch = InputElement.GetCharFromKey(key, shiftPressed, input.Type);
                                if (ch.HasValue)
                                {
                                    if (input.Type == "number")
                                    {
                                        if (ch == '.' && input.Value.Contains('.')) continue;
                                        if (ch == '-' && input.Value.Length > 0 && !input.Value.StartsWith("-")) continue;
                                        if (ch == '-' && input.Value.StartsWith("-")) continue;
                                    }
                                    input.Value += ch.Value;
                                    changed = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        _keyDownTime.Remove(key);
                        _lastAddTime.Remove(key);
                    }
                }
                if (changed)
                {
                    _overlay.RefreshUI();
                    _overlay.InvokeListeners(input, "input");
                    _overlay.TriggerChange(input);
                }
                needsRefresh = input.Update(deltaTime, _controlContext, _window);
            }
            if (needsRefresh)
            {
                _overlay.RefreshUI();
            }
        }
    }
}