// Folder: SiegeEngine/Core/GPU/ContextManagement
// File: VertexLayout.cs
using System;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    public enum VertexSemantic : byte
    {
        Position = 0,
        Color = 1,
        TexCoord = 2,
        Normal = 3,
        MaterialIndex = 4,
        Tangent = 5,
        BoneIds = 6,
        BoneWeights = 7
    }

    public struct VertexAttribute
    {
        public VertexSemantic Semantic;
        public int Type;
        public int Size;
        public int Offset;
        public int Slot;

        public VertexAttribute(VertexSemantic semantic, int type, int size, int offset, int slot)
        {
            Semantic = semantic;
            Type = type;
            Size = size;
            Offset = offset;
            Slot = slot;
        }
    }

    public sealed class VertexLayout
    {
        public VertexAttribute[] Attributes { get; }
        public int Stride { get; }

        public VertexLayout(int stride, VertexAttribute[] attributes)
        {
            if (attributes == null)
                throw new ArgumentNullException(nameof(attributes));
            Stride = stride;
            Attributes = attributes;
        }

        public static int Location(VertexSemantic semantic)
        {
            return (int)semantic;
        }
    }
}
