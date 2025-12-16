using System.Numerics;

namespace SiegeEngine.Core.Rendering
{
    public struct Vertex
    {
        public float X, Y, Z;
        public float R, G, B, A;

        public Vertex(float x, float y, float z, float r, float g, float b, float a)
        {
            X = x; Y = y; Z = z;
            R = r; G = g; B = b; A = a;
        }
    }
}