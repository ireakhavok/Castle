// SiegeEngine/Managers/ModInfo.cs
using SiegeEngine.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SiegeEngine.Core.Definitions
{
    public class ModInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }
    }
}