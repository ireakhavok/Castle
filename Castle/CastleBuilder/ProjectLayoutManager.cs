// Folder: CastleBuilder
// File: ProjectLayoutManager.cs
using SiegeEngine.Core.Managers;
using System;
using System.IO;
using System.Text.Json;

namespace CastleBuilder
{
    public static class ProjectLayoutManager
    {
        private static string GetLayoutPath(string contextName)
        {
            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath))
            {
                // Step 2: Silent global temp fallback when no project is open
                string tempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CastleBuilder", "TempLayouts");
                Directory.CreateDirectory(tempDir);
                return Path.Combine(tempDir, $"layout.{contextName}.json");
            }
            return Path.Combine(projectPath, $"layout.{contextName}.json");
        }

        public static void SaveCurrentLayout(string contextName)
        {
            Console.WriteLine($"[ProjectLayoutManager] SaveCurrentLayout START - Context: '{contextName}'");

            string layoutPath = GetLayoutPath(contextName);
            Console.WriteLine($"[ProjectLayoutManager] Writing full docking layout to: {layoutPath}");

            var strategy = PanelManager.Current?.IDEStrategy;
            if (strategy == null)
            {
                Console.WriteLine("[ProjectLayoutManager] WARNING: No active IDEDockingStrategy");
                return;
            }

            try
            {
                string fullState = strategy.SerializeState();
                File.WriteAllText(layoutPath, fullState);
                Console.WriteLine($"[ProjectLayoutManager] SUCCESS: Full docking layout saved for '{contextName}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR: Failed to write layout: {ex.Message}");
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

            string layoutPath = GetLayoutPath(contextName);
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
                Console.WriteLine($"[ProjectLayoutManager] SUCCESS: Full docking layout restored for '{contextName}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR: Failed to restore layout: {ex.Message}");
            }
        }
    }
}