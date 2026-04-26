// File: SiegeEngine/Core/UI/UIInteractionLayer.cs
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.UI.Elements;
using SiegeEngine.Core.UI.JSParser;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        private RangeElement _draggingSlider = null;
        private float _sliderOldValue;
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
            _openSelects = _overlay.FindElementsByTag("select").Where(s => (s as SelectElement)?.IsOpen ?? false).Cast<SelectElement>().ToList();
            var clickablesSnapshot = _overlay._uiClickables.ToList();
            foreach (var clickable in clickablesSnapshot)
            {
                bool isDropdownElement = IsDropdownElement(clickable);
                Vector2 effectiveMouse = isDropdownElement ? relMousePos : scrolledMousePos;
                clickable.UpdateHover(effectiveMouse, vw, vh);
            }
            bool dropdownPressHandled = false;
            foreach (var select in _openSelects)
            {
                bool selectOver = select.IsHover;
                if (selectOver && mousePress)
                {
                    if (!string.IsNullOrEmpty(select.OnMouseDownJS))
                    {
                        _overlay._jsContext.RunWithThis(select.OnMouseDownJS, new JSElement(select, _overlay));
                    }
                    _overlay.InvokeListeners(select, "mousedown");
                    select.IsActive = true;
                    dropdownPressHandled = true;
                }
                var options = select.Children.Where(c => c.Tag.ToLower() == "option").Cast<OptionElement>().ToList();
                foreach (var opt in options)
                {
                    bool over = opt.IsHover;
                    if (over && mousePress)
                    {
                        if (!string.IsNullOrEmpty(opt.OnMouseDownJS))
                        {
                            _overlay._jsContext.RunWithThis(opt.OnMouseDownJS, new JSElement(opt, _overlay));
                        }
                        _overlay.InvokeListeners(opt, "mousedown");
                        opt.IsActive = true;
                        dropdownPressHandled = true;
                    }
                }
                if (dropdownPressHandled) break;
            }
            if (!dropdownPressHandled)
            {
                foreach (var clickable in clickablesSnapshot.Where(c => !IsDropdownElement(c)))
                {
                    bool over = clickable.IsHover;
                    if (over && mousePress)
                    {
                        if (!string.IsNullOrEmpty(clickable.OnMouseDownJS))
                        {
                            _overlay._jsContext.RunWithThis(clickable.OnMouseDownJS, new JSElement(clickable, _overlay));
                        }
                        _overlay.InvokeListeners(clickable, "mousedown");
                        clickable.IsActive = true;
                        if (clickable.Tag.ToLower() == "input" && (clickable as InputElement)?.Type == "range")
                        {
                            _draggingSlider = clickable as RangeElement;
                            _sliderOldValue = _draggingSlider.Value;
                        }
                    }
                }
            }
            bool dropdownReleaseHandled = false;
            foreach (var select in _openSelects)
            {
                bool selectOver = select.IsHover;
                if (selectOver && mouseRelease)
                {
                    if (!string.IsNullOrEmpty(select.OnMouseUpJS))
                    {
                        _overlay._jsContext.RunWithThis(select.OnMouseUpJS, new JSElement(select, _overlay));
                    }
                    _overlay.InvokeListeners(select, "mouseup");
                }
                bool selectWasActive = select.IsActive;
                if (selectOver && mouseRelease && selectWasActive)
                {
                    _overlay.HandleUIClick(select);
                    dropdownReleaseHandled = true;
                }
                if (mouseRelease)
                {
                    select.IsActive = false;
                }
                var options = select.Children.Where(c => c.Tag.ToLower() == "option").Cast<OptionElement>().ToList();
                foreach (var opt in options)
                {
                    bool over = opt.IsHover;
                    if (over && mouseRelease)
                    {
                        if (!string.IsNullOrEmpty(opt.OnMouseUpJS))
                        {
                            _overlay._jsContext.RunWithThis(opt.OnMouseUpJS, new JSElement(opt, _overlay));
                        }
                        _overlay.InvokeListeners(opt, "mouseup");
                    }
                    bool wasActive = opt.IsActive;
                    if (over && mouseRelease && wasActive)
                    {
                        _overlay.HandleUIClick(opt);
                        dropdownReleaseHandled = true;
                    }
                    if (mouseRelease)
                    {
                        opt.IsActive = false;
                    }
                }
                if (dropdownReleaseHandled) break;
            }
            if (!dropdownReleaseHandled)
            {
                foreach (var clickable in clickablesSnapshot.Where(c => !IsDropdownElement(c)))
                {
                    bool over = clickable.IsHover;
                    if (over && mouseRelease)
                    {
                        if (!string.IsNullOrEmpty(clickable.OnMouseUpJS))
                        {
                            _overlay._jsContext.RunWithThis(clickable.OnMouseUpJS, new JSElement(clickable, _overlay));
                        }
                        _overlay.InvokeListeners(clickable, "mouseup");
                    }
                    bool wasActive = clickable.IsActive;
                    if (over && mouseRelease && wasActive)
                    {
                        _overlay.HandleUIClick(clickable);
                    }
                    if (mouseRelease)
                    {
                        clickable.IsActive = false;
                    }
                }
            }
            if (currentMouseDown && _draggingSlider != null)
            {
                float relX = Math.Clamp(relMousePos.X - _draggingSlider.ComputedContentX, 0f, _draggingSlider.ComputedContentWidth);
                float percent = relX / _draggingSlider.ComputedContentWidth;
                float newValue = _draggingSlider.Min + percent * (_draggingSlider.Max - _draggingSlider.Min);
                if (_draggingSlider.Step > 0) newValue = (float)Math.Round(newValue / _draggingSlider.Step) * _draggingSlider.Step;
                newValue = Math.Clamp(newValue, _draggingSlider.Min, _draggingSlider.Max);
                if (Math.Abs(_draggingSlider.Value - newValue) > 0.0001f)
                {
                    _draggingSlider.Value = newValue;
                    string syncedValue = newValue.ToString(CultureInfo.InvariantCulture);
                    ((InputElement)_draggingSlider).Value = syncedValue;
                    _draggingSlider.Attributes["value"] = syncedValue;
                    _overlay.InvokeListeners(_draggingSlider, "input");
                }
            }
            if (mouseRelease && _draggingSlider != null)
            {
                if (Math.Abs(_sliderOldValue - _draggingSlider.Value) > 0.0001f)
                {
                    _overlay.TriggerChange(_draggingSlider);
                }
                _draggingSlider = null;
            }
            if (mouseRelease && _openSelects.Any() && !_justOpenedSelect && !dropdownReleaseHandled)
            {
                _overlay.CloseAllOpenSelects();
                _overlay.RefreshUI();
            }
            if (!currentMouseDown)
            {
                _justOpenedSelect = false;
            }
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
            if (_overlay._document != null)
            {
                _overlay._document.InvokeDocumentMousemove(relMousePos);
                if (mouseRelease)
                {
                    _overlay._document.InvokeDocumentMouseup(relMousePos);
                }
            }
            _prevMouseDown = currentMouseDown;
        }
        private bool IsDropdownElement(HtmlElement elem)
        {
            if (elem.Tag.ToLower() == "option" && elem.Parent is SelectElement s && s.IsOpen)
            {
                return true;
            }
            return false;
        }
    }
}