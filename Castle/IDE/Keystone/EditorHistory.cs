// Folder: Keystone
// File: EditorHistory.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.UI;
using SiegeEngine.Core.UI.Elements;
using System;
using System.Collections.Generic;

namespace Keystone
{
    public sealed class EditorHistory
    {
        public static EditorHistory Current { get; } = new EditorHistory();

        public const int MaxDepth = 256;
        private readonly List<IEditorCommand> _undo = new List<IEditorCommand>();
        private readonly List<IEditorCommand> _redo = new List<IEditorCommand>();
        private bool _isApplying;
        private bool _initialized;
        private EventBus _eventBus;
        private IControlContext _controlContext;
        private nint _window;
        private bool _prevZ;
        private bool _prevY;
        private bool _prevDelete;
        private bool _prevBackspace;
        private readonly Dictionary<int, (System.Numerics.Vector3 pos, System.Numerics.Quaternion rot)> _lastTransform
            = new Dictionary<int, (System.Numerics.Vector3, System.Numerics.Quaternion)>();

        public static System.Action<int, System.Numerics.Vector3, System.Numerics.Quaternion> TransformApplied;
        public static bool FlyCameraActive { get; set; }
        public bool IsApplying => _isApplying;
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;

        public void Initialize(EventBus eventBus)
        {
            if (_initialized) return;
            _initialized = true;
            _eventBus = eventBus;
            UIOverlay.ValueCommitted = OnUiValueCommitted;
            DockSplitNode.SplitterCommitted = OnSplitterCommitted;
            _eventBus.Subscribe<EntitySelectedEvent>(OnEntitySelected);
            _eventBus.Subscribe<EntityMovedEvent>(OnEntityMoved);
            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
        }

        public void BindInput(IControlContext controlContext, nint window)
        {
            _controlContext = controlContext;
            _window = window;
        }

        private bool IsHeld(Key key)
        {
            var action = _controlContext.GetKey(_window, key);
            return action != InputAction.Release;
        }

        public void Tick()
        {
            if (_controlContext == null || _window == IntPtr.Zero) return;
            if (FlyCameraActive)
            {
                _prevZ = false;
                _prevY = false;
                _prevDelete = false;
                _prevBackspace = false;
                return;
            }
            bool ctrl = IsHeld(Key.LeftControl) || IsHeld(Key.RightControl);
            bool shift = IsHeld(Key.LeftShift) || IsHeld(Key.RightShift);
            bool z = IsHeld(Key.Z);
            bool y = IsHeld(Key.Y);
            bool del = IsHeld(Key.Delete);
            bool back = IsHeld(Key.Backspace);
            bool textFocus = AnyTextInputFocused();

            if (ctrl && z && !_prevZ && !textFocus)
            {
                if (shift) Redo();
                else Undo();
            }
            if (ctrl && y && !_prevY && !textFocus)
                Redo();
            if (!ctrl && !textFocus && del && !_prevDelete)
                RequestDeleteSelection();
            // Backspace is reserved for text editing. Entity delete is Delete only.

            _prevZ = z;
            _prevY = y;
            _prevDelete = del;
            _prevBackspace = back;
        }

        public static bool AnyTextInputFocused()
        {
            var pm = PanelManager.Current;
            if (pm == null) return false;
            foreach (var panel in pm.GetAllPanels())
            {
                if (panel == null || !panel.Visible) continue;
                string typeName = panel.GetType().Name;
                if (typeName == "ScriptEditorPanel")
                    return true;
                if (panel is BasePanel bp && bp._uiOverlay != null && bp._uiOverlay.HasFocusedTextInput())
                    return true;
            }
            return false;
        }

        public void Execute(IEditorCommand command)
        {
            if (command == null) return;
            if (_isApplying)
            {
                command.Execute();
                return;
            }
            _isApplying = true;
            try { command.Execute(); }
            finally { _isApplying = false; }
            Push(command);
        }

        public void Record(IEditorCommand command)
        {
            if (command == null || _isApplying) return;
            Push(command);
        }

        private void Push(IEditorCommand command)
        {
            if (_undo.Count > 0 && _undo[_undo.Count - 1].TryMerge(command))
            {
                _redo.Clear();
                return;
            }
            _undo.Add(command);
            if (_undo.Count > MaxDepth)
                _undo.RemoveAt(0);
            _redo.Clear();
        }

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            var cmd = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _isApplying = true;
            try { cmd.Undo(); }
            finally { _isApplying = false; }
            _redo.Add(cmd);
            return true;
        }

        public bool Redo()
        {
            if (_redo.Count == 0) return false;
            var cmd = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            _isApplying = true;
            try { cmd.Execute(); }
            finally { _isApplying = false; }
            _undo.Add(cmd);
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            _lastTransform.Clear();
        }

        public static void RequestDeleteSelection()
        {
            var bus = Current._eventBus;
            if (bus == null) return;
            bus.Publish(new GenericEvent { Hook = "EditorDeleteSelection" });
        }

        public static void RequestDeleteScene()
        {
            var bus = Current._eventBus;
            if (bus == null) return;
            bus.Publish(new GenericEvent { Hook = "EditorDeleteScene" });
        }

        private void OnUiValueCommitted(HtmlElement elem, UIOverlay overlay)
        {
            if (_isApplying || elem == null) return;
            if (elem is RangeElement range)
            {
                float oldValue = range.CommittedValue;
                float newValue = range.Value;
                if (Math.Abs(oldValue - newValue) < 0.0001f) return;
                string id = range.Attributes != null && range.Attributes.TryGetValue("id", out var eid) ? eid : "";
                string hook = range.Attributes != null && range.Attributes.TryGetValue("data-hook", out var h) ? h : "";
                Execute(new SliderValueCommand(range, overlay, oldValue, newValue, id + ":" + hook));
                range.CommittedValue = newValue;
                return;
            }
            if (elem is InputElement input && (input.Type == "text" || input.Type == "number" || input.Type == "checkbox"))
            {
                string oldValue = input.CommittedValue ?? "";
                string newValue = input.Type == "checkbox" ? (input.Checked ? "true" : "false") : (input.Value ?? "");
                if (oldValue == newValue) return;
                Execute(new InputValueCommand(input, overlay, oldValue, newValue));
                input.CommittedValue = newValue;
            }
        }

        private void OnSplitterCommitted(DockSplitNode node, float oldRatio, float newRatio)
        {
            if (_isApplying || node == null) return;
            if (Math.Abs(oldRatio - newRatio) < 0.0001f) return;
            Execute(new SplitterRatioCommand(node, oldRatio, newRatio));
        }

        private void OnEntitySelected(EntitySelectedEvent e)
        {
            if (e?.SelectedEntityIds == null) return;
            var level = ProjectSettings.Current.CurrentLevel;
            if (level == null) return;
            foreach (var id in e.SelectedEntityIds)
            {
                var entity = level.Entities.Find(ent => ent.Id == id);
                var physics = entity?.GetComponent<PhysicsComponent>();
                if (physics != null)
                    _lastTransform[id] = (physics.Position, physics.Rotation);
            }
        }

        private void OnEntityMoved(EntityMovedEvent e)
        {
            if (_isApplying || e == null) return;
            var level = ProjectSettings.Current.CurrentLevel;
            if (level == null) return;
            var entity = level.Entities.Find(ent => ent.Id == e.EntityId);
            var physics = entity?.GetComponent<PhysicsComponent>();
            if (physics == null) return;
            if (!_lastTransform.TryGetValue(e.EntityId, out var oldT))
                oldT = (physics.Position, physics.Rotation);
            var newT = (physics.Position, physics.Rotation);
            if ((oldT.pos - newT.Item1).LengthSquared() < 1e-10f && oldT.rot == newT.Item2)
                return;
            Execute(new TransformCommand(e.EntityId, oldT.pos, oldT.rot, newT.Item1, newT.Item2));
            _lastTransform[e.EntityId] = newT;
        }

        private void OnGenericEvent(GenericEvent e)
        {
            if (e == null) return;
            if (e.Hook == "LoadProject" || e.Hook == "CastleBuilder.NewProject")
                Clear();
        }
    }

    public sealed class DelegateCommand : IEditorCommand
    {
        public string Description { get; }
        private readonly Action _execute;
        private readonly Action _undo;
        public DelegateCommand(string description, Action execute, Action undo)
        {
            Description = description ?? "";
            _execute = execute;
            _undo = undo;
        }
        public void Execute() => _execute?.Invoke();
        public void Undo() => _undo?.Invoke();
        public bool TryMerge(IEditorCommand incoming) => false;
    }

    public sealed class SliderValueCommand : IEditorCommand
    {
        public string Description { get; }
        private readonly RangeElement _range;
        private readonly UIOverlay _overlay;
        private readonly float _oldValue;
        private float _newValue;
        public SliderValueCommand(RangeElement range, UIOverlay overlay, float oldValue, float newValue, string key)
        {
            _range = range;
            _overlay = overlay;
            _oldValue = oldValue;
            _newValue = newValue;
            Description = "Slider " + key;
        }
        public void Execute() => Apply(_newValue);
        public void Undo() => Apply(_oldValue);
        public bool TryMerge(IEditorCommand incoming)
        {
            if (incoming is SliderValueCommand other && other._range == _range)
            {
                _newValue = other._newValue;
                return true;
            }
            return false;
        }
        private void Apply(float value)
        {
            if (_range == null) return;
            _range.Value = value;
            _range.CommittedValue = value;
            if (_range.Attributes != null)
                _range.Attributes["value"] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _overlay?.TriggerChange(_range);
        }
    }

    public sealed class InputValueCommand : IEditorCommand
    {
        public string Description => "Input";
        private readonly InputElement _input;
        private readonly UIOverlay _overlay;
        private readonly string _oldValue;
        private string _newValue;
        public InputValueCommand(InputElement input, UIOverlay overlay, string oldValue, string newValue)
        {
            _input = input;
            _overlay = overlay;
            _oldValue = oldValue ?? "";
            _newValue = newValue ?? "";
        }
        public void Execute() => Apply(_newValue);
        public void Undo() => Apply(_oldValue);
        public bool TryMerge(IEditorCommand incoming)
        {
            if (incoming is InputValueCommand other && other._input == _input)
            {
                _newValue = other._newValue;
                return true;
            }
            return false;
        }
        private void Apply(string value)
        {
            if (_input == null) return;
            if (_input.Type == "checkbox")
            {
                _input.Checked = value == "true" || value == "1" || value == "on";
                _input.CommittedValue = _input.Checked ? "true" : "false";
            }
            else
            {
                _input.Value = value;
                _input.CommittedValue = value;
                if (_input.Attributes != null)
                    _input.Attributes["value"] = value ?? "";
            }
            _overlay?.TriggerChange(_input);
        }
    }

    public sealed class SplitterRatioCommand : IEditorCommand
    {
        public string Description => "Panel splitter";
        private readonly DockSplitNode _node;
        private readonly float _oldRatio;
        private float _newRatio;
        public SplitterRatioCommand(DockSplitNode node, float oldRatio, float newRatio)
        {
            _node = node;
            _oldRatio = oldRatio;
            _newRatio = newRatio;
        }
        public void Execute() { if (_node != null) _node.SplitRatio = _newRatio; }
        public void Undo() { if (_node != null) _node.SplitRatio = _oldRatio; }
        public bool TryMerge(IEditorCommand incoming)
        {
            if (incoming is SplitterRatioCommand other && other._node == _node)
            {
                _newRatio = other._newRatio;
                return true;
            }
            return false;
        }
    }

    public sealed class TransformCommand : IEditorCommand
    {
        public string Description => "Transform entity";
        private readonly int _entityId;
        private readonly System.Numerics.Vector3 _oldPos;
        private readonly System.Numerics.Quaternion _oldRot;
        private System.Numerics.Vector3 _newPos;
        private System.Numerics.Quaternion _newRot;
        public TransformCommand(int entityId, System.Numerics.Vector3 oldPos, System.Numerics.Quaternion oldRot, System.Numerics.Vector3 newPos, System.Numerics.Quaternion newRot)
        {
            _entityId = entityId;
            _oldPos = oldPos;
            _oldRot = oldRot;
            _newPos = newPos;
            _newRot = newRot;
        }
        public void Execute() => Apply(_newPos, _newRot);
        public void Undo() => Apply(_oldPos, _oldRot);
        public bool TryMerge(IEditorCommand incoming)
        {
            if (incoming is TransformCommand other && other._entityId == _entityId)
            {
                _newPos = other._newPos;
                _newRot = other._newRot;
                return true;
            }
            return false;
        }
        private void Apply(System.Numerics.Vector3 pos, System.Numerics.Quaternion rot)
        {
            var level = ProjectSettings.Current.CurrentLevel;
            if (level == null) return;
            var entity = level.Entities.Find(e => e.Id == _entityId);
            var physics = entity?.GetComponent<PhysicsComponent>();
            if (physics == null) return;
            physics.Position = pos;
            physics.Rotation = rot;
            EditorHistory.TransformApplied?.Invoke(_entityId, pos, rot);
        }
    }

    public sealed class TerrainStrokeCommand : IEditorCommand
    {
        public string Description => "Terrain stroke";
        private readonly float[,] _before;
        private float[,] _after;
        private readonly Action<float[,]> _apply;
        public TerrainStrokeCommand(float[,] before, float[,] after, Action<float[,]> apply)
        {
            _before = before;
            _after = after;
            _apply = apply;
        }
        public void Execute() => _apply?.Invoke(CloneMap(_after));
        public void Undo() => _apply?.Invoke(CloneMap(_before));
        public bool TryMerge(IEditorCommand incoming)
        {
            if (incoming is TerrainStrokeCommand other && other._apply == _apply)
            {
                _after = other._after;
                return true;
            }
            return false;
        }
        public static float[,] CloneMap(float[,] src)
        {
            if (src == null) return null;
            int w = src.GetLength(0);
            int h = src.GetLength(1);
            var dst = new float[w, h];
            Array.Copy(src, dst, src.Length);
            return dst;
        }
    }

    public sealed class SetPropertyCommand : IEditorCommand
    {
        public string Description => "Set property";
        private readonly int _entityId;
        private readonly string _componentName;
        private readonly string _propertyName;
        private readonly object _oldValue;
        private object _newValue;
        private readonly Action<int, string, string, object> _apply;
        public SetPropertyCommand(int entityId, string componentName, string propertyName, object oldValue, object newValue, Action<int, string, string, object> apply)
        {
            _entityId = entityId;
            _componentName = componentName;
            _propertyName = propertyName;
            _oldValue = oldValue;
            _newValue = newValue;
            _apply = apply;
        }
        public void Execute() => _apply?.Invoke(_entityId, _componentName, _propertyName, _newValue);
        public void Undo() => _apply?.Invoke(_entityId, _componentName, _propertyName, _oldValue);
        public bool TryMerge(IEditorCommand incoming)
        {
            if (incoming is SetPropertyCommand other
                && other._entityId == _entityId
                && other._componentName == _componentName
                && other._propertyName == _propertyName)
            {
                _newValue = other._newValue;
                return true;
            }
            return false;
        }
    }
}
