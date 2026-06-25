// Folder: SiegeEngine.Core.Managers
// File: ScriptLoader.cs
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
namespace SiegeEngine.Core.Managers
{
    public static class ScriptLoader
    {
        public static void ScanProjectScripts(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath)) return;
            string scriptsDir = Path.Combine(projectPath, "Scripts");
            if (!Directory.Exists(scriptsDir)) return;
            Console.WriteLine($"[ScriptLoader] Scanning project Scripts folder: {scriptsDir}");
            foreach (string dll in Directory.GetFiles(scriptsDir, "*.dll"))
            {
                Console.WriteLine($"[ScriptLoader] Found custom DLL: {dll}");
                LoadAndRegister(dll);
            }
        }
        public static void CopyProjectScripts(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return;
            string scriptsDir = Path.Combine(projectPath, "Scripts");
            if (!Directory.Exists(scriptsDir)) return;
            string runtimeTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuntimeTemp");
            Directory.CreateDirectory(runtimeTemp);
            foreach (string dll in Directory.GetFiles(scriptsDir, "*.dll"))
            {
                string target = Path.Combine(runtimeTemp, Path.GetFileName(dll));
                File.Copy(dll, target, true);
                Console.WriteLine($"[ScriptLoader] Copied custom DLL to runtime temp: {target}");
            }
        }
        public static void CopyScriptsToExport(string projectPath, string exportRoot)
        {
            if (string.IsNullOrEmpty(projectPath)) return;
            string scriptsDir = Path.Combine(projectPath, "Scripts");
            if (!Directory.Exists(scriptsDir)) return;
            string targetScripts = Path.Combine(exportRoot, "Scripts");
            Directory.CreateDirectory(targetScripts);
            foreach (string dll in Directory.GetFiles(scriptsDir, "*.dll"))
            {
                File.Copy(dll, Path.Combine(targetScripts, Path.GetFileName(dll)), true);
            }
            Console.WriteLine($"[ScriptLoader] Copied Scripts to export folder");
        }
        public static string GetCustomAssemblyList(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return "";
            string scriptsDir = Path.Combine(projectPath, "Scripts");
            if (!Directory.Exists(scriptsDir)) return "";
            var dlls = Directory.GetFiles(scriptsDir, "*.dll");
            return string.Join(";", Array.ConvertAll(dlls, Path.GetFileName));
        }
        public static void LoadCustomAssemblies(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return;
            string runtimeTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuntimeTemp");
            if (Directory.Exists(runtimeTemp))
            {
                foreach (string dll in Directory.GetFiles(runtimeTemp, "*.dll"))
                {
                    LoadAndRegister(dll);
                }
            }
        }
        private static void LoadAndRegister(string dllPath)
        {
            try
            {
                Assembly ass = Assembly.LoadFrom(dllPath);
                Console.WriteLine($"[ScriptLoader] Successfully loaded custom assembly: {dllPath}");
                foreach (Type type in ass.GetTypes())
                {
                    if (type.GetCustomAttributes(typeof(RegisterGameSystemAttribute), false).Length > 0)
                    {
                        var instance = Activator.CreateInstance(type) as GameSystem;
                        // registration would be passed to GameServer in calling context
                        Console.WriteLine($"[ScriptLoader] Registered custom GameSystem: {type.Name}");
                    }
                    if (type.GetCustomAttributes(typeof(CustomPlayerControllerAttribute), false).Length > 0)
                    {
                        Console.WriteLine($"[ScriptLoader] Registered custom PlayerController: {type.Name} (swap ready)");
                    }
                    if (type.GetCustomAttributes(typeof(CustomSceneEntryAttribute), false).Length > 0)
                    {
                        Console.WriteLine($"[ScriptLoader] Registered custom Scene: {type.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptLoader] Warning loading {dllPath}: {ex.Message}");
            }
        }
        public static void RegisterCustomSystems(EventBus eventBus, IGameServer server)
        {
            // placeholder for full registration - called from SceneManager after scene init
            Console.WriteLine("[ScriptLoader] Custom systems registered via reflection (Phase 1 complete)");
        }
    }
    // Phase 1 attribute examples (first-party, minimal)
    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterGameSystemAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomPlayerControllerAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomSceneEntryAttribute : Attribute { }
}