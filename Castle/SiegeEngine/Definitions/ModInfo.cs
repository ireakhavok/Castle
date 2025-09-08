// SiegeEngine/Managers/ModInfo.cs
using SiegeEngine.Interfaces;
using SiegeEngine.UnityAssetLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SiegeEngine.Definitions
{
    public class ModInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }
        public UnityAssetData UnityAssets { get; set; }
    }

    public class UnityAssetData
    {
        public Dictionary<string, UnityAssetFileType> Files { get; set; }
        public Dictionary<string, string> GuidMap { get; set; }
    }
}