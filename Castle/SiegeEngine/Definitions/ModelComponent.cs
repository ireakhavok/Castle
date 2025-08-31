// Engine.Core/Definitions/ModelComponent.cs
using SiegeEngine.AssetParsing;
using System;

namespace SiegeEngine.Definitions
{
    public class ModelComponent : IComponent
    {
        public FBXModel Model { get; set; }
        public string Key { get; set; } // Added to store model identifier
    }
}