using System;
using System.IO;
using System.Text.Json;
using SiegeEngine.Core.Definitions;
using Trebuchet;

namespace GameHost
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseDir = AppContext.BaseDirectory;
            string payloadPath = Path.Combine(baseDir, "play_payload.json");
            string projectPath = baseDir;
            string levelName = "Main";
            string levelData = null;
            string sceneData = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--play-project" && i + 1 < args.Length)
                    projectPath = args[++i];
                else if (args[i] == "--load-level" && i + 1 < args.Length)
                    levelName = args[++i];
                else if (args[i] == "--play-payload-file" && i + 1 < args.Length)
                    payloadPath = args[++i];
            }

            if (!File.Exists(payloadPath))
                payloadPath = Path.Combine(projectPath, "play_payload.json");

            if (File.Exists(payloadPath))
            {
                try
                {
                    string json = File.ReadAllText(payloadPath);
                    using JsonDocument doc = JsonDocument.Parse(json);
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("LevelName", out JsonElement nameElem))
                    {
                        string name = nameElem.GetString();
                        if (!string.IsNullOrEmpty(name)) levelName = name;
                    }
                    if (root.TryGetProperty("LevelDataBase64", out JsonElement levelElem))
                    {
                        string b64 = levelElem.GetString();
                        if (!string.IsNullOrEmpty(b64)) levelData = b64;
                    }
                    if (root.TryGetProperty("SceneData", out JsonElement sceneElem))
                    {
                        sceneData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sceneElem.GetRawText()));
                    }
                    RuntimeSettings.ApplyFromPayloadRoot(root);
                    RuntimeSettings.TryLoadFile(Path.Combine(baseDir, "RuntimeTemp", "runtime_settings.json"));
                    RuntimeSettings.TryLoadFile(Path.Combine(baseDir, "runtime_settings.json"));
                    Console.WriteLine("[GameHost] Loaded " + payloadPath + " level=" + levelName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[GameHost] Payload failed: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine("[GameHost] No play_payload.json next to the exe.");
            }

            var launcher = new Launcher();
            launcher.Start("OpenGL", false, 0, 0, false, 0, true, projectPath, levelName, levelData, sceneData);
        }
    }
}
