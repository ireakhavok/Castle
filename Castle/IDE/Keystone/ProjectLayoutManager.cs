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
            PreloadFromDisk();
            var strategy = PanelManager.Current?.IDEStrategy;
            strategy?.ClearBladeCaches();
            if (!string.IsNullOrEmpty(lastContext))
                strategy?.SetActiveBlade(lastContext);
            LoadLayoutForContext(lastContext);
            if (!_memoryCache.ContainsKey(lastContext ?? ""))
                EnsureLayoutForContext(lastContext);
        }

        static void PreloadFromDisk()
        {
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                return;
            foreach (string path in Directory.GetFiles(projectPath, "layout.*.json"))
            {
                string name = Path.GetFileName(path);
                if (!name.StartsWith("layout.", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;
                string context = name.Substring("layout.".Length, name.Length - "layout.".Length - ".json".Length);
                if (string.IsNullOrEmpty(context)) continue;
                try
                {
                    _memoryCache[context] = File.ReadAllText(path);
                    Console.WriteLine($"[ProjectLayoutManager] Preloaded blade '{context}' into memory");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProjectLayoutManager] Failed to preload '{context}': {ex.Message}");
                }
            }
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
                _memoryCache[contextName] = strategy.SerializeState();
                Console.WriteLine($"[ProjectLayoutManager] Cached '{contextName}' in memory (disk flush is Save only)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR saving '{contextName}': {ex.Message}");
            }
        }

        public static bool TryRestoreFromMemory(string contextName)
        {
            if (string.IsNullOrEmpty(contextName)) return false;
            if (!_memoryCache.TryGetValue(contextName, out string json) || string.IsNullOrEmpty(json))
                return false;
            var strategy = PanelManager.Current?.IDEStrategy;
            if (strategy == null) return false;
            try
            {
                strategy.DeserializeState(json);
                Console.WriteLine($"[ProjectLayoutManager] Restored '{contextName}' from memory cache");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR restoring '{contextName}' from memory: {ex.Message}");
                return false;
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

            if (TryRestoreFromMemory(contextName))
                return;

            Console.WriteLine("[ProjectLayoutManager] No cached layout - leaving workspace blank (no disk read on switch)");
        }

        public static void EnsureLayoutForContext(string contextName)
        {
            if (string.IsNullOrEmpty(contextName)) return;
            if (_memoryCache.ContainsKey(contextName))
                return;

            Console.WriteLine($"[ProjectLayoutManager] Seeding default layout for '{contextName}' into memory");
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
