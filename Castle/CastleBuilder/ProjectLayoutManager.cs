// Folder: CastleBuilder
// File: ProjectLayoutManager.cs
using SiegeEngine.Core.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CastleBuilder
{
    public static class ProjectLayoutManager
    {
        // Pure in-memory cache for all blades (Blender-style)
        // Survives switching even with no project loaded
        private static readonly Dictionary<string, string> _memoryCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void SaveCurrentLayout(string contextName)
        {
            Console.WriteLine($"[ProjectLayoutManager] SaveCurrentLayout (MEMORY ONLY) - Context: '{contextName}'");

            var strategy = PanelManager.Current?.IDEStrategy;
            if (strategy == null)
            {
                Console.WriteLine("[ProjectLayoutManager] WARNING: No active IDEDockingStrategy");
                return;
            }

            try
            {
                string fullState = strategy.SerializeState();
                _memoryCache[contextName] = fullState;

                // ONLY write to disk if a project is actually loaded
                string projectPath = ProjectSettings.Current.ActiveProject;
                if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
                {
                    string layoutPath = Path.Combine(projectPath, $"layout.{contextName}.json");
                    File.WriteAllText(layoutPath, fullState);
                    Console.WriteLine($"[ProjectLayoutManager] Also committed to disk (project loaded)");
                }
                else
                {
                    Console.WriteLine($"[ProjectLayoutManager] Saved to memory only (no project loaded)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR: Failed to save '{contextName}': {ex.Message}");
            }
        }

        public static void LoadLayoutForContext(string contextName)
        {
            Console.WriteLine($"[ProjectLayoutManager] LoadLayoutForContext START - Context: '{contextName}'");

            var strategy = PanelManager.Current?.IDEStrategy;
            if (strategy == null)
            {
                Console.WriteLine("[ProjectLayoutManager] WARNING: No active IDEDockingStrategy");
                return;
            }

            strategy.ClearAll();

            // Memory first (instant switch, even with no project)
            if (_memoryCache.TryGetValue(contextName, out string cachedState) && !string.IsNullOrEmpty(cachedState))
            {
                strategy.DeserializeState(cachedState);
                Console.WriteLine($"[ProjectLayoutManager] SUCCESS: Restored from MEMORY cache");
                return;
            }

            // Disk fallback (last saved state only)
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath))
            {
                Console.WriteLine("[ProjectLayoutManager] No active project - workspace cleared for new blade");
                return;
            }

            string layoutPath = Path.Combine(projectPath, $"layout.{contextName}.json");
            Console.WriteLine($"[ProjectLayoutManager] Looking for layout file: {layoutPath}");

            if (!File.Exists(layoutPath))
            {
                Console.WriteLine($"[ProjectLayoutManager] No layout file found for '{contextName}' (using default blank workspace)");
                return;
            }

            try
            {
                string json = File.ReadAllText(layoutPath);
                strategy.DeserializeState(json);
                _memoryCache[contextName] = json; // prime memory
                Console.WriteLine($"[ProjectLayoutManager] SUCCESS: Full docking layout restored for '{contextName}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR: Failed to restore layout: {ex.Message}");
            }
        }
    }
}