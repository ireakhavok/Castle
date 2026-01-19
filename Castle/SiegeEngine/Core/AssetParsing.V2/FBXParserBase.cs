// Folder: SiegeEngine.Core
// File: AssetParsing.V2/FBXParserBase.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SiegeEngine.Core.AssetParsing.V2.Model;
using SiegeEngine.Core.Rendering;

namespace SiegeEngine.Core.AssetParsing.V2
{
    public static class FBXParserBase
    {
        private static int totalLogCount = 0;
        private const int MaxTotalLogs = 5000;
        public static void Log(string message)
        {
            if (totalLogCount >= MaxTotalLogs)
            {
                if (totalLogCount == MaxTotalLogs)
                {
                    Console.WriteLine($"FBXParser: Log limit reached, suppressing further logs");
                    totalLogCount++;
                }
                return;
            }
            Console.WriteLine(message);
            totalLogCount++;
        }

        public static FBXModel CreateDefaultCubeModel()
        {
            return new FBXModel();
        }

        // Additional base methods will be added iteratively
    }
}