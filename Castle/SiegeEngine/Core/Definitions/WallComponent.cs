using SiegeEngine.Core.Interfaces;
using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace SiegeEngine.Core.Definitions
{
    public class WallComponent : IComponent
    {
        public Vector3 StartVertex { get; set; }
        public Vector3 EndVertex { get; set; }
        public bool IsPreview { get; set; }

        public Vector3 Position
        {
            get => (StartVertex + EndVertex) / 2;
            set
            {
                Vector3 delta = value - Position;
                StartVertex += delta;
                EndVertex += delta;
            }
        }

        public Vector3 Size
        {
            get
            {
                Vector3 diff = EndVertex - StartVertex;
                return new Vector3(MathF.Abs(diff.X), MathF.Abs(diff.Y), 1.0f);
            }
        }

        public virtual bool Validate(IGameServer server)
        {
            return server.ValidateAndUpdateMovement(0, new Vector2(Position.X, Position.Y), Quaternion.Identity, 0);
        }

        public virtual void Render(GL gl, bool isPreview)
        {
            // Handled by EditorScene.Render
        }
    }
}