// Folder: SiegeEngine.Core
// File: AssetParsing.V2/Model/FBXModel.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.AssetObjects;
using SiegeEngine.Core.Definitions;
namespace SiegeEngine.Core.AssetParsing.V2.Model
{
    public class FBXModel
    {
        public List<MeshData> Meshes { get; set; } = new List<MeshData>();
        public Skeleton Skeleton { get; set; } = new Skeleton();
        public List<Animation> Animations { get; set; } = new List<Animation>();
        public bool HasSkin { get; set; } = false;
        public bool HasRestPose { get; set; }
        public bool AutoCorrected { get; set; } = false;
    }
}