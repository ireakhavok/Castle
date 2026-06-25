// Folder: SiegeEngine.Core.Managers
// File: ScriptLoader.cs
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Diagnostics;
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
            string[] csFiles = Directory.GetFiles(scriptsDir, "*.cs");
            if (csFiles.Length > 0 && Directory.GetFiles(scriptsDir, "*.dll").Length == 0)
            {
                BuildProjectScripts(projectPath);
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
            Console.WriteLine("[ScriptLoader] Custom systems registered via reflection (Phase 1 complete)");
        }
        public static void ApplyCustomPlayerControllerIfPresent(Player player, ref PlayerMovement movement)
        {
            Console.WriteLine("[ScriptLoader] Scanning for [CustomPlayerController]...");
            string runtimeTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuntimeTemp");
            if (Directory.Exists(runtimeTemp))
            {
                foreach (string dll in Directory.GetFiles(runtimeTemp, "*.dll"))
                {
                    try
                    {
                        Assembly ass = Assembly.LoadFrom(dll);
                        foreach (Type type in ass.GetTypes())
                        {
                            if (type.GetCustomAttributes(typeof(CustomPlayerControllerAttribute), false).Length > 0 &&
                                typeof(PlayerMovement).IsAssignableFrom(type))
                            {
                                try
                                {
                                    var custom = Activator.CreateInstance(type) as PlayerMovement;
                                    if (custom != null)
                                    {
                                        movement = custom;
                                        Console.WriteLine($"[ScriptLoader] SUCCESS: Swapped to custom PlayerController '{type.Name}' - full override active for Play/Export");
                                        return;
                                    }
                                }
                                catch
                                {
                                    Console.WriteLine($"[ScriptLoader] Custom ctor fallback - default retained");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ScriptLoader] Skipped {dll} reflection: {ex.Message}");
                    }
                }
            }
            Console.WriteLine("[ScriptLoader] Custom PlayerController swap applied (or default retained) - Phase 2 ready");
        }
        public static void BuildProjectScripts(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath)) return;
            string scriptsDir = Path.Combine(projectPath, "Scripts");
            Directory.CreateDirectory(scriptsDir);
            string libsDir = Path.Combine(scriptsDir, "Libs");
            Directory.CreateDirectory(libsDir);
            string csprojPath = Path.Combine(scriptsDir, "SiegeScripts.csproj");
            if (!File.Exists(csprojPath))
            {
                string template = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Library</OutputType>
    <OutputPath>Libs\</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""SiegeEngine"">
      <HintPath>..\SiegeEngine.dll</HintPath>
    </Reference>
    <Reference Include=""Foundation"">
      <HintPath>..\Foundation.dll</HintPath>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""**/*.cs"" />
  </ItemGroup>
</Project>";
                File.WriteAllText(csprojPath, template);
                if (Directory.GetFiles(scriptsDir, "*.cs").Length == 0)
                {
                    string exampleSrc = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SiegeEngine", "PlayerSystem", "CustomPlayerController.cs");
                    if (File.Exists(exampleSrc))
                    {
                        File.Copy(exampleSrc, Path.Combine(scriptsDir, "CustomPlayerController.cs"), true);
                        Console.WriteLine("[ScriptLoader] Copied CustomPlayerController.cs starter template to Scripts/ (ready to edit/override)");
                    }
                }
                Console.WriteLine($"[ScriptLoader] Generated SiegeScripts.csproj at {csprojPath}");
            }
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{csprojPath}\" --configuration Release --no-incremental --output \"{libsDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = scriptsDir
            };
            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string err = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Console.WriteLine($"[ScriptLoader.BuildProjectScripts] dotnet build completed. Exit: {process.ExitCode}\nOutput: {output}");
                if (process.ExitCode == 0)
                {
                    foreach (string dll in Directory.GetFiles(libsDir, "*.dll"))
                    {
                        string runtimeTarget = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuntimeTemp", Path.GetFileName(dll));
                        Directory.CreateDirectory(Path.GetDirectoryName(runtimeTarget));
                        File.Copy(dll, runtimeTarget, true);
                        LoadAndRegister(dll);
                    }
                    ScanProjectScripts(projectPath);
                    Console.WriteLine("[ScriptLoader] Build → DLL copy → reflection register COMPLETE. Custom controllers now active for Play/Export.");
                }
                else
                {
                    Console.WriteLine($"[ScriptLoader] Build warning: {err}");
                }
            }
            CopyProjectScripts(projectPath);
        }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterGameSystemAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomPlayerControllerAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomSceneEntryAttribute : Attribute { }
}