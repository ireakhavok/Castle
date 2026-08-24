// Folder: SiegeEngine/Core/ContextManagement
// File: Viewport.cs
using System.Numerics;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    public readonly struct Viewport
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        public Viewport(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Vector2 Center => new Vector2(X + Width * 0.5f, Y + Height * 0.5f);

        public static Viewport FullWindow(int windowWidth, int windowHeight)
            => new Viewport(0, 0, windowWidth, windowHeight);
    }
}