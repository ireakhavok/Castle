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
        public static void SaveCurrentLayout(string contextName)
        {
            Console.WriteLine($"[ProjectLayoutManager] SaveCurrentLayout START - Context: '{contextName}'");

            string projectPath = ProjectSettings.Current.ActiveProject;
            Console.WriteLine($"[ProjectLayoutManager] ActiveProject path from settings: '{projectPath}'");

            if (string.IsNullOrEmpty(projectPath))
            {
                Console.WriteLine("[ProjectLayoutManager] ERROR: ActiveProject is null or empty - cannot save layout");
                return;
            }

            if (!Directory.Exists(projectPath))
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR: Project directory does not exist: {projectPath}");
                return;
            }

            string layoutPath = Path.Combine(projectPath, $"layout.{contextName}.json");
            Console.WriteLine($"[ProjectLayoutManager] Writing full docking layout to: {layoutPath}");

            // Use the public IDEStrategy getter added to PanelManager
            var strategy = PanelManager.Current?.IDEStrategy;
            if (strategy == null)
            {
                Console.WriteLine("[ProjectLayoutManager] WARNING: No active IDEDockingStrategy - falling back to basic snapshot");
                var snapshot = new Dictionary<string, object>
                {
                    ["context"] = contextName,
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["activeProject"] = projectPath,
                    ["lastSaved"] = DateTime.UtcNow.ToString("o")
                };
                File.WriteAllText(layoutPath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            try
            {
                string fullState = strategy.SerializeState();
                File.WriteAllText(layoutPath, fullState);
                Console.WriteLine($"[ProjectLayoutManager] SUCCESS: Full docking layout saved for '{contextName}' ({fullState.Length} bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProjectLayoutManager] ERROR: Failed to write layout: {ex.Message}");
            }
        }

        public static void LoadLayoutForContext(string contextName)
        {
            Console.WriteLine($"[ProjectLayoutManager] LoadLayoutForContext START - Context: '{contextName}'");

            string projectPath = ProjectSettings.Current.ActiveProject;
            Console.WriteLine($"[ProjectLayoutManager] ActiveProject path: '{projectPath}'");

            if (string.IsNullOrEmpty(projectPath))
            {
                Console.WriteLine("[ProjectLayoutManager] ERROR: ActiveProject is null");
                return;
            }

            string layoutPath = Path.Combine(projectPath, $"layout.{contextName}.json");
            Console.WriteLine($"[ProjectLayoutManager] Looking for layout file: {layoutPath}");

            if (!File.Exists(layoutPath))
            {
                Console.WriteLine($"[ProjectLayoutManager] No layout file found for '{contextName}' (using default)");
                return;
            }

            // Use the public IDEStrategy getter added to PanelManager
            var strategy = PanelManager.Current?.IDEStrategy;
            if (strategy == null)
            {
                Console.WriteLine("[ProjectLayoutManager] WARNING: No active IDEDockingStrategy - cannot restore layout");
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