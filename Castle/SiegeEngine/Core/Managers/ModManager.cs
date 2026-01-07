// Folder: SiegeEngine.Managers
// File: ModManager.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.UnityAssetLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SiegeEngine.Core.Managers
{
    public class ModManager
    {
        private readonly string _modsDirectory;
        private readonly string _solutionDirectory;
        private readonly ISteamEngine _steamEngine;
        private readonly List<ModInfo> _loadedMods;
        public ModManager(string modsDirectory, ISteamEngine steamEngine)
        {
            _modsDirectory = modsDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods");
            _solutionDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            _steamEngine = steamEngine;
            _loadedMods = new List<ModInfo>();
            LoadLocalMods();
            if (_steamEngine != null) LoadWorkshopMods();
        }
        public IReadOnlyList<ModInfo> LoadedMods => _loadedMods.AsReadOnly();
        private void LoadLocalMods()
        {
            string localModsPath = _modsDirectory;
            if (!Directory.Exists(localModsPath))
            {
                Console.WriteLine($"ModManager: Mods directory not found at {localModsPath}, checking solution root.");
                localModsPath = Path.Combine(_solutionDirectory, "Mods");
                if (!Directory.Exists(localModsPath))
                {
                    Console.WriteLine($"ModManager: Solution mods directory not found at {localModsPath}, skipping local mod loading.");
                }
                else
                {
                    Console.WriteLine($"ModManager: Found solution mods directory at {localModsPath}, loading local mods.");
                }
            }
            if (Directory.Exists(localModsPath))
            {
                foreach (var dir in Directory.GetDirectories(localModsPath))
                {
                    string modJsonPath = Path.Combine(dir, "mod.json");
                    LoadMod(modJsonPath);
                    ProcessUnityAssets(dir);
                }
            }
            string assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            if (Directory.Exists(assetsPath))
            {
                foreach (var dir in Directory.GetDirectories(assetsPath))
                {
                    var modInfo = new ModInfo
                    {
                        Name = Path.GetFileName(dir),
                        Version = "1.0",
                        Path = dir
                    };
                    _loadedMods.Add(modInfo);
                    Console.WriteLine($"ModManager: Added asset pack as mod '{modInfo.Name}' from {dir}");
                    ProcessUnityAssets(dir);
                }
            }
        }
        private void LoadWorkshopMods()
        {
            ulong[] subscribedItems = _steamEngine.GetSubscribedWorkshopItems();
            foreach (ulong itemId in subscribedItems)
            {
                string installPath = _steamEngine.GetWorkshopItemInstallInfo(itemId);
                if (installPath != null)
                {
                    string modJsonPath = Path.Combine(installPath, "mod.json");
                    Console.WriteLine($"ModManager: Checking Workshop mod at: {modJsonPath}, Exists: {File.Exists(modJsonPath)}");
                    LoadMod(modJsonPath);
                    ProcessUnityAssets(installPath);
                }
            }
        }
        private void LoadMod(string modJsonPath)
        {
            if (!File.Exists(modJsonPath))
            {
                Console.WriteLine($"ModManager: No mod.json found at {modJsonPath}, skipping.");
                return;
            }
            try
            {
                string json = File.ReadAllText(modJsonPath);
                var modInfo = JsonSerializer.Deserialize<ModInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (modInfo != null)
                {
                    modInfo.Path = Path.GetDirectoryName(modJsonPath);
                    _loadedMods.Add(modInfo);
                    Console.WriteLine($"ModManager: Loaded mod '{modInfo.Name}' (Version: {modInfo.Version}) from {modJsonPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ModManager: Failed to load mod from {modJsonPath}: {ex.Message}");
            }
        }
        private void ProcessUnityAssets(string modPath)
        {
            try
            {
                var scanner = new UnityAssetScanner();
                var files = scanner.ScanDirectory(modPath);
                Console.WriteLine($"ModManager: Processed Unity assets in {modPath}");
                Console.WriteLine($"Found {files.Count} files");
                var modInfo = _loadedMods.Find(m => m.Path == modPath);
                if (modInfo != null)
                {
                    modInfo.UnityAssets = new UnityAssetData { Files = files };
                }
                var prefabs = files.Where(f => f.Value == UnityAssetFileType.Prefab).Select(f => f.Key).ToList();
                var prefabReader = new PrefabFileReader();
                foreach (var prefab in prefabs)
                {
                    try
                    {
                        int goCount = prefabReader.CountGameObjects(prefab);
                        Console.WriteLine($"ModManager: Prefab {prefab} has {goCount} GameObjects");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ModManager: Error reading prefab {prefab}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ModManager: Error processing Unity assets in {modPath}: {ex.Message}");
            }
        }
        public string ResolvePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            // Normalize path: trim leading backslashes, replace / with \
            relativePath = relativePath.TrimStart('\\', '/').Replace("/", "\\");
            if (Path.IsPathRooted(relativePath)) return File.Exists(relativePath) ? relativePath : null;
            string[] commonDirs = new[]
            {
                Path.Combine("Assets", "Characters", "Adventure_Character", "Textures"),
                Path.Combine("Assets", "Textures"),
                Path.Combine("Assets", "Static_Images"),
                Path.Combine("Assets", "Configs"),
                "Textures"
            };
            foreach (var dir in commonDirs)
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dir, relativePath);
                Console.WriteLine($"ModManager: Checking common path for {relativePath}: {fullPath}, Exists: {File.Exists(fullPath)}");
                if (File.Exists(fullPath))
                    return fullPath;
                string solutionFullPath = Path.Combine(_solutionDirectory, dir, relativePath);
                Console.WriteLine($"ModManager: Checking solution common path for {relativePath}: {solutionFullPath}, Exists: {File.Exists(solutionFullPath)}");
                if (File.Exists(solutionFullPath))
                    return solutionFullPath;
            }
            foreach (var mod in _loadedMods)
            {
                string modPath = Path.Combine(mod.Path, relativePath);
                Console.WriteLine($"ModManager: Checking mod path for {relativePath}: {modPath}, Exists: {File.Exists(modPath)}");
                if (File.Exists(modPath))
                    return modPath;
            }
            string solutionPath = Path.Combine(_solutionDirectory, relativePath);
            Console.WriteLine($"ModManager: Checking solution path for {relativePath}: {solutionPath}, Exists: {File.Exists(solutionPath)}");
            if (File.Exists(solutionPath))
                return solutionPath;
            string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            Console.WriteLine($"ModManager: Checking output path for {relativePath}: {outputPath}, Exists: {File.Exists(outputPath)}");
            if (File.Exists(outputPath))
                return outputPath;
            Console.WriteLine($"ModManager: Path not found for {relativePath}");
            return null;
        }
        public string GetMenuConfigPath()
        {
            foreach (var mod in _loadedMods)
            {
                string configPath = Path.Combine(mod.Path, "MainMenu.html");
                Console.WriteLine($"ModManager: Checking mod config path: {configPath}, Exists: {File.Exists(configPath)}");
                if (File.Exists(configPath))
                    return configPath;
            }
            string solutionPath = Path.Combine(_solutionDirectory, "Assets", "Configs", "MainMenu.html");
            Console.WriteLine($"ModManager: Checking solution config path: {solutionPath}, Exists: {File.Exists(solutionPath)}");
            if (File.Exists(solutionPath))
                return solutionPath;
            string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Configs", "MainMenu.html");
            Console.WriteLine($"ModManager: Checking output config path: {outputPath}, Exists: {File.Exists(outputPath)}");
            if (File.Exists(outputPath))
                return outputPath;
            Console.WriteLine($"ModManager: Menu config path not found");
            return null;
        }
        //public List<MenuDefinition> GetAllMenuExtensions()
        //{
        // var extensions = new List<MenuDefinition>();
        // foreach (var mod in _loadedMods)
        // {
        // extensions.AddRange(mod.Menus);
        // }
        // return extensions;
        //}
    }
}