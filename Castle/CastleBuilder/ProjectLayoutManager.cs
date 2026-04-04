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
        public static void SaveCurrentLayout(string contextName)
        {
            Console.WriteLine($"[ProjectLayoutManager] SaveCurrentLayout START - Context: '{contextName}'");

            string projectPath = ProjectSettings.Current.ActiveProject;
            if (string.IsNullOrEmpty(projectPath))
            {
                Console.WriteLine("[ProjectLayoutManager] No active project - skipping save");
                return;
            }

            if (!Directory.Exists(projectPath))
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR: Project directory does not exist: {projectPath}");
                return;
            }

            string layoutPath = Path.Combine(projectPath, $"layout.{contextName}.json");
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
                Console.WriteLine($"[ProjectLayoutManager] SUCCESS: Full docking layout saved for '{contextName}' (JSON length: {fullState.Length} bytes)");
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
                Console.WriteLine($"[ProjectLayoutManager] SUCCESS: Full docking layout restored for '{contextName}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR: Failed to restore layout: {ex.Message}");
            }
        }
    }
}