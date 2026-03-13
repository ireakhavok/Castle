// Folder: SiegeEngine/Core/Definitions
// File: TransformComponent.cs
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    public class TransformComponent : IComponent
    {
        private Vector3 _position = Vector3.Zero;
        private Quaternion _rotation = Quaternion.Identity;
        private Vector3 _scale = Vector3.One;
        private Matrix4x4 _localToWorld = Matrix4x4.Identity;
        private bool _isDirty = true;

        public TransformComponent Parent { get; private set; }
        public List<TransformComponent> Children { get; } = new List<TransformComponent>();

        public Vector3 Position
        {
            get => _position;
            set { _position = value; MarkDirty(); }
        }

        public Quaternion Rotation
        {
            get => _rotation;
            set { _rotation = value; MarkDirty(); }
        }

        public Vector3 Scale
        {
            get => _scale;
            set { _scale = value; MarkDirty(); }
        }

        public Vector3 WorldPosition => GetWorldPosition();
        public Quaternion WorldRotation => GetWorldRotation();
        public Vector3 WorldScale => GetWorldScale();

        public Matrix4x4 LocalToWorld
        {
            get
            {
                if (_isDirty) Recalculate();
                return _localToWorld;
            }
        }

        public void SetParent(TransformComponent newParent)
        {
            if (Parent == newParent) return;
            Parent?.Children.Remove(this);
            Parent = newParent;
            newParent?.Children.Add(this);
            MarkDirty();
        }

        public void AddChild(TransformComponent child)
        {
            if (child == null || child == this) return;
            child.SetParent(this);
        }

        public void RemoveChild(TransformComponent child)
        {
            if (child != null && Children.Remove(child))
            {
                child.Parent = null;
                child.MarkDirty();
            }
        }

        public Vector3 GetWorldPosition()
        {
            if (_isDirty) Recalculate();
            return Vector3.Transform(Vector3.Zero, _localToWorld);
        }

        public Quaternion GetWorldRotation()
        {
            if (_isDirty) Recalculate();
            Matrix4x4.Decompose(_localToWorld, out _, out var rot, out _);
            return rot;
        }

        public Vector3 GetWorldScale()
        {
            if (_isDirty) Recalculate();
            Matrix4x4.Decompose(_localToWorld, out var scale, out _, out _);
            return scale;
        }

        public void MarkDirty()
        {
            _isDirty = true;
            foreach (var child in Children)
            {
                child.MarkDirty();
            }
        }

        private void Recalculate()
        {
            Matrix4x4 local = Matrix4x4.CreateScale(_scale) *
                              Matrix4x4.CreateFromQuaternion(_rotation) *
                              Matrix4x4.CreateTranslation(_position);

            if (Parent != null)
            {
                _localToWorld = local * Parent.LocalToWorld;
            }
            else
            {
                _localToWorld = local;
            }

            _isDirty = false;
        }
    }
}