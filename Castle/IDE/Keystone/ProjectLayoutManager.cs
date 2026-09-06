// Folder: CastleBuilder
// File: ProjectLayoutManager.cs
using SiegeEngine.Core.Managers;
using System;
using System.Collections.Generic;
using System.IO;

namespace Keystone
{
    public static class ProjectLayoutManager
    {
        private static readonly Dictionary<string, string> _memoryCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static Action<string> OpenDefaultCompanions;

        public static string LayoutFilePath(string contextName)
        {
            if (string.IsNullOrEmpty(contextName)) return null;
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                return null;
            return Path.Combine(projectPath, $"layout.{contextName}.json");
        }

        public static bool LayoutFileExists(string contextName)
        {
            string path = LayoutFilePath(contextName);
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public static bool HasDiskLayout(string contextName) => LayoutFileExists(contextName);

        public static void ClearMemory()
        {
            _memoryCache.Clear();
            Console.WriteLine("[ProjectLayoutManager] Memory cache wiped");
        }

        public static void OnProjectOpened(string lastContext)
        {
            ClearMemory();
            var strategy = PanelManager.Current?.IDEStrategy;
            strategy?.ClearBladeCaches();
            if (!string.IsNullOrEmpty(lastContext))
                strategy?.SetActiveBlade(lastContext);
            LoadLayoutForContext(lastContext);
            if (!LayoutFileExists(lastContext))
                EnsureLayoutForContext(lastContext);
        }

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

                string layoutPath = LayoutFilePath(contextName);
                if (!string.IsNullOrEmpty(layoutPath))
                {
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

            string layoutPath = LayoutFilePath(contextName);
            if (!string.IsNullOrEmpty(layoutPath) && File.Exists(layoutPath))
            {
                try
                {
                    string json = File.ReadAllText(layoutPath);
                    strategy.DeserializeState(json);
                    _memoryCache[contextName] = json;
                    Console.WriteLine($"[ProjectLayoutManager] SUCCESS: Restored saved layout file for '{contextName}'");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProjectLayoutManager] ERROR: Failed to restore layout file: {ex.Message}");
                    return;
                }
            }

            Console.WriteLine("[ProjectLayoutManager] No layout file - leaving workspace blank (no seed)");
        }

        public static void EnsureLayoutForContext(string contextName)
        {
            if (string.IsNullOrEmpty(contextName)) return;

            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                return;

            if (LayoutFileExists(contextName))
                return;

            Console.WriteLine($"[ProjectLayoutManager] Creating new layout JSON for '{contextName}'");
            OpenDefaultCompanions?.Invoke(contextName);
            SaveCurrentLayout(contextName);
        }

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
