// Engine.Core/Definitions/ModelComponent.cs
using SiegeEngine.Core.AssetParsing.Model;
using System;

namespace SiegeEngine.Core.Definitions
{
    public class ModelComponent : IComponent
    {
        public FBXModel Model { get; set; }
        public string Key { get; set; } // Added to store model identifier
    }
}