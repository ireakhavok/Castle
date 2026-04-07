// Folder: CastleBuilder
// File: ProjectLayoutManager.cs
using SiegeEngine.Core.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ToolChest;


namespace CastleBuilder
{
    public static class ProjectLayoutManager
    {
        // Pure in-memory cache for every blade (Blender-style workspaces)
        // ALWAYS active — even when no project is loaded
        private static readonly Dictionary<string, string> _memoryCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void SaveCurrentLayout(string contextName)
        {
            Console.WriteLine($"[ProjectLayoutManager] SaveCurrentLayout (MEMORY) - Context: '{contextName}'");

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

                // Only write to disk if a project is loaded (never on blade switch)
                string projectPath = ProjectSettings.Current.ActiveProject;
                if (!string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath))
                {
                    string layoutPath = Path.Combine(projectPath, $"layout.{contextName}.json");
                    File.WriteAllText(layoutPath, fullState);
                    Console.WriteLine($"[ProjectLayoutManager] Also committed to disk (project active)");
                }
                else
                {
                    Console.WriteLine($"[ProjectLayoutManager] Saved to memory only (no project loaded)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR saving '{contextName}': {ex.Message}");
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

            // Memory cache first (hotswap works even with no project)
            if (_memoryCache.TryGetValue(contextName, out string cachedState) && !string.IsNullOrEmpty(cachedState))
            {
                strategy.DeserializeState(cachedState);
                Console.WriteLine($"[ProjectLayoutManager] SUCCESS: Restored '{contextName}' from MEMORY cache");
                return;
            }

            // Disk fallback only when project exists
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
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
                _memoryCache[contextName] = json;
                Console.WriteLine($"[ProjectLayoutManager] SUCCESS: Full docking layout restored for '{contextName}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR: Failed to restore layout: {ex.Message}");
            }
        }

        // Called ONLY by explicit Save / SaveProjectAs
        public static void FlushAllToDisk()
        {
            Console.WriteLine("[ProjectLayoutManager] FlushAllToDisk - committing ALL blades from memory to disk");

            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                Console.WriteLine("[ProjectLayoutManager] No project - cannot flush to disk");
                return;
            }

            foreach (var kv in _memoryCache)
            {
                try
                {
                    string layoutPath = Path.Combine(projectPath, $"layout.{kv.Key}.json");
                    File.WriteAllText(layoutPath, kv.Value);
                    Console.WriteLine($"  Saved blade '{kv.Key}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR saving blade '{kv.Key}': {ex.Message}");
                }
            }
            Console.WriteLine("[ProjectLayoutManager] All blades committed to disk");
        }
    }
}