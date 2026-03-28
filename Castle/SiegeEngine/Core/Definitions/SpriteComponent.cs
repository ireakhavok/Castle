// Folder: SiegeEngine/Core/Definitions
// File: SpriteComponent.cs
using System.Numerics;
namespace SiegeEngine.Core.Definitions
{
    public class SpriteComponent : IComponent
    {
        public string TexturePath { get; set; }
        public Vector2 Size { get; set; } = new Vector2(1f, 1f);
    }
}