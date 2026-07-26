// Folder: SiegeEngine.Core.Managers
// File: ScriptLoader.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Scenes;
using SiegeEngine.Systems;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SiegeEngine.Core.Managers
{
    public static class ScriptLoader
    {
        private static readonly List<Assembly> _loadedAssemblies = new List<Assembly>();
        private static readonly object _assemblyLock = new object();

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
                lock (_assemblyLock)
                {
                    if (!_loadedAssemblies.Contains(ass))
                        _loadedAssemblies.Add(ass);
                }
                Console.WriteLine($"[ScriptLoader] Successfully loaded custom assembly: {dllPath}");
                foreach (Type type in ass.GetTypes())
                {
                    if (type.GetCustomAttributes(typeof(RegisterGameSystemAttribute), false).Length > 0)
                        Console.WriteLine($"[ScriptLoader] Discovered [RegisterGameSystem]: {type.FullName}");
                    if (type.GetCustomAttributes(typeof(CustomPlayerControllerAttribute), false).Length > 0)
                        Console.WriteLine($"[ScriptLoader] Discovered [CustomPlayerController]: {type.FullName}");
                    if (type.GetCustomAttributes(typeof(CustomSceneEntryAttribute), false).Length > 0)
                        Console.WriteLine($"[ScriptLoader] Discovered [CustomSceneEntry]: {type.FullName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptLoader] Warning loading {dllPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Single activation entry point for pure-client and editor runtime.
        /// Resolves constructors against live services, registers systems, swaps controllers, registers scenes.
        /// </summary>
        public static void ActivateProjectScripts(SceneContext ctx, InputHandler inputHandler = null, ClientPredictionSystem predictionSystem = null)
        {
            if (ctx == null) return;

            var services = new Dictionary<Type, object>();
            void AddService(Type t, object instance)
            {
                if (t != null && instance != null && !services.ContainsKey(t))
                    services[t] = instance;
            }

            AddService(typeof(IGameServer), ctx.Server);
            AddService(typeof(EventBus), ctx.EventBus);
            AddService(typeof(IRenderContext), ctx.RenderContext);
            AddService(typeof(IControlContext), ctx.ControlContext);
            AddService(typeof(SceneContext), ctx);
            AddService(typeof(Player), ctx.Player);
            AddService(typeof(ModelManager), ctx.ModelManager);
            AddService(typeof(Level), ctx.CurrentLevel);
            AddService(typeof(InputHandler), inputHandler);
            AddService(typeof(ClientPredictionSystem), predictionSystem);
            if (ctx.PlayerMovement != null)
                AddService(typeof(PlayerMovement), ctx.PlayerMovement);

            // Honour explicit ControllerTypeName first (SceneData.Settings)
            string controllerTypeName = ctx.SceneData?.Settings?.ControllerTypeName;
            if (!string.IsNullOrWhiteSpace(controllerTypeName))
            {
                Type namedType = FindTypeByName(controllerTypeName);
                if (namedType != null && typeof(PlayerMovement).IsAssignableFrom(namedType))
                {
                    try
                    {
                        var custom = ResolveInstance(namedType, services) as PlayerMovement;
                        if (custom != null)
                        {
                            ctx.PlayerMovement = custom;
                            AddService(typeof(PlayerMovement), custom);
                            Console.WriteLine($"[ScriptLoader] SUCCESS: Swapped to named PlayerController '{namedType.Name}' from ControllerTypeName");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ScriptLoader] Named controller '{controllerTypeName}' construction failed: {ex.Message}");
                    }
                }
            }

            lock (_assemblyLock)
            {
                foreach (Assembly ass in _loadedAssemblies)
                {
                    Type[] types;
                    try { types = ass.GetTypes(); }
                    catch { continue; }

                    foreach (Type type in types)
                    {
                        if (type.IsAbstract || type.IsInterface) continue;

                        // GameSystems
                        if (type.GetCustomAttributes(typeof(RegisterGameSystemAttribute), false).Length > 0 &&
                            typeof(GameSystem).IsAssignableFrom(type))
                        {
                            try
                            {
                                var system = ResolveInstance(type, services) as GameSystem;
                                if (system != null && ctx.Server != null)
                                {
                                    ctx.Server.AddSystem(system);
                                    Console.WriteLine($"[ScriptLoader] Registered custom GameSystem: {type.Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ScriptLoader] Failed to construct GameSystem {type.Name}: {ex.Message}");
                            }
                        }

                        // Player controllers (attribute path – only if no ControllerTypeName already applied)
                        if (string.IsNullOrWhiteSpace(controllerTypeName) &&
                            type.GetCustomAttributes(typeof(CustomPlayerControllerAttribute), false).Length > 0 &&
                            typeof(PlayerMovement).IsAssignableFrom(type))
                        {
                            try
                            {
                                var custom = ResolveInstance(type, services) as PlayerMovement;
                                if (custom != null)
                                {
                                    ctx.PlayerMovement = custom;
                                    AddService(typeof(PlayerMovement), custom);
                                    Console.WriteLine($"[ScriptLoader] SUCCESS: Swapped to custom PlayerController '{type.Name}' - full override active for Play/Export");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ScriptLoader] CustomPlayerController {type.Name} construction failed: {ex.Message}");
                            }
                        }

                        // Custom scenes
                        if (type.GetCustomAttributes(typeof(CustomSceneEntryAttribute), false).Length > 0)
                        {
                            try
                            {
                                string sceneName = type.Name;
                                if (!SceneRegistry.IsRegistered(sceneName))
                                {
                                    SceneRegistry.Register(sceneName, (SceneContext c) =>
                                    {
                                        var localServices = new Dictionary<Type, object>(services);
                                        if (c != null)
                                        {
                                            if (c.Server != null) localServices[typeof(IGameServer)] = c.Server;
                                            if (c.EventBus != null) localServices[typeof(EventBus)] = c.EventBus;
                                            if (c.RenderContext != null) localServices[typeof(IRenderContext)] = c.RenderContext;
                                            if (c.ControlContext != null) localServices[typeof(IControlContext)] = c.ControlContext;
                                            localServices[typeof(SceneContext)] = c;
                                            if (c.Player != null) localServices[typeof(Player)] = c.Player;
                                            if (c.ModelManager != null) localServices[typeof(ModelManager)] = c.ModelManager;
                                            if (c.CurrentLevel != null) localServices[typeof(Level)] = c.CurrentLevel;
                                        }
                                        return ResolveInstance(type, localServices) as IScene
                                               ?? throw new InvalidOperationException($"Could not construct custom scene {type.Name}");
                                    });
                                    Console.WriteLine($"[ScriptLoader] Registered custom Scene: {sceneName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ScriptLoader] Failed to register custom scene {type.Name}: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        private static Type FindTypeByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string target = name.Trim();
            lock (_assemblyLock)
            {
                foreach (Assembly ass in _loadedAssemblies)
                {
                    try
                    {
                        foreach (Type t in ass.GetTypes())
                        {
                            if (string.Equals(t.Name, target, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(t.FullName, target, StringComparison.OrdinalIgnoreCase))
                                return t;
                        }
                    }
                    catch { }
                }
            }
            return null;
        }

        private static object ResolveInstance(Type type, IDictionary<Type, object> services)
        {
            ConstructorInfo[] ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (ctors.Length == 0)
                throw new InvalidOperationException($"Type {type.FullName} has no public constructors");

            // Prefer the constructor with the largest number of parameters that can be fully satisfied
            ConstructorInfo best = null;
            object[] bestArgs = null;
            int bestScore = -1;

            foreach (ConstructorInfo ctor in ctors.OrderByDescending(c => c.GetParameters().Length))
            {
                ParameterInfo[] parms = ctor.GetParameters();
                object[] args = new object[parms.Length];
                bool allSatisfied = true;
                for (int i = 0; i < parms.Length; i++)
                {
                    Type pt = parms[i].ParameterType;
                    object resolved = null;
                    if (services.TryGetValue(pt, out resolved))
                    {
                        args[i] = resolved;
                        continue;
                    }
                    // Allow assignable matches
                    foreach (var kv in services)
                    {
                        if (pt.IsAssignableFrom(kv.Key))
                        {
                            args[i] = kv.Value;
                            resolved = kv.Value;
                            break;
                        }
                    }
                    if (resolved == null)
                    {
                        // Optional parameters with defaults
                        if (parms[i].HasDefaultValue)
                        {
                            args[i] = parms[i].DefaultValue;
                            continue;
                        }
                        allSatisfied = false;
                        break;
                    }
                }
                if (allSatisfied && parms.Length > bestScore)
                {
                    best = ctor;
                    bestArgs = args;
                    bestScore = parms.Length;
                }
            }

            if (best != null)
                return best.Invoke(bestArgs);

            // Last resort: parameterless
            ConstructorInfo parameterless = type.GetConstructor(Type.EmptyTypes);
            if (parameterless != null)
                return parameterless.Invoke(null);

            throw new InvalidOperationException($"No satisfiable constructor found for {type.FullName}");
        }

        public static void RegisterCustomSystems(EventBus eventBus, IGameServer server)
        {
            // Legacy entry point – full activation now requires SceneContext.
            // Callers that still use this signature receive a no-op; the real work is done by ActivateProjectScripts.
            Console.WriteLine("[ScriptLoader] RegisterCustomSystems (legacy) – prefer ActivateProjectScripts(SceneContext)");
        }

        public static void ApplyCustomPlayerControllerIfPresent(Player player, ref PlayerMovement movement)
        {
            // Legacy path kept for binary compatibility. Real swap occurs inside ActivateProjectScripts.
            Console.WriteLine("[ScriptLoader] ApplyCustomPlayerControllerIfPresent (legacy) – activation deferred to ActivateProjectScripts");
        }

        public static void ApplyControllerByTypeName(string typeName, Player player, ref PlayerMovement movement)
        {
            // Legacy path – ControllerTypeName is now honoured inside ActivateProjectScripts via SceneContext.
            Console.WriteLine("[ScriptLoader] ApplyControllerByTypeName (legacy) – activation deferred to ActivateProjectScripts");
        }

        public static void BuildProjectScripts(string projectPath, string customOutputDir = null)
        {
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath)) return;
            string scriptsDir = Path.Combine(projectPath, "Scripts");
            Directory.CreateDirectory(scriptsDir);
            string libsDir = Path.Combine(scriptsDir, "Libs");
            Directory.CreateDirectory(libsDir);
            string outputPath = customOutputDir ?? libsDir;
            string binDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] coreDlls = { "SiegeEngine.dll", "Foundation.dll" };
            foreach (string dllName in coreDlls)
            {
                string source = Path.Combine(binDir, dllName);
                string target = Path.Combine(scriptsDir, dllName);
                if (File.Exists(source))
                {
                    File.Copy(source, target, true);
                    Console.WriteLine($"[ScriptLoader] Copied core DLL {dllName} to Scripts/ for build reference");
                }
            }
            string csprojPath = Path.Combine(scriptsDir, "SiegeScripts.csproj");
            if (!File.Exists(csprojPath))
            {
                string template = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Library</OutputType>
    <OutputPath>Libs\</OutputPath>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""SiegeEngine"">
      <HintPath>SiegeEngine.dll</HintPath>
    </Reference>
    <Reference Include=""Foundation"">
      <HintPath>Foundation.dll</HintPath>
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
                Arguments = $"build \"{csprojPath}\" --configuration Release --no-incremental --output \"{outputPath}\"",
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
                    foreach (string dll in Directory.GetFiles(outputPath, "*.dll"))
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