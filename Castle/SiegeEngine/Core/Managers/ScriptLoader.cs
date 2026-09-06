// Folder: SiegeEngine.Core.Managers
// File: ScriptLoader.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.GPU.ContextManagement;
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

        private static readonly HashSet<string> CoreDllNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SiegeEngine.dll",
            "Foundation.dll",
            "Trebuchet.dll",
            "Citadel.dll",
            "Citadel.exe"
        };

        private static bool IsCoreDll(string pathOrFileName)
        {
            if (string.IsNullOrEmpty(pathOrFileName)) return false;
            string name = Path.GetFileName(pathOrFileName);
            return CoreDllNames.Contains(name);
        }

        public static void ScanProjectScripts(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath)) return;
            string scriptsDir = Path.Combine(projectPath, "Scripts");
            if (!Directory.Exists(scriptsDir)) return;
            Console.WriteLine($"[ScriptLoader] Scanning project Scripts folder: {scriptsDir}");

            // Top-level Scripts/*.dll
            foreach (string dll in Directory.GetFiles(scriptsDir, "*.dll"))
            {
                if (IsCoreDll(dll)) continue;
                Console.WriteLine($"[ScriptLoader] Found custom DLL: {dll}");
                LoadAndRegister(dll);
            }

            // Built output lives in Scripts/Libs (BuildProjectScripts --output Libs\)
            string libsDir = Path.Combine(scriptsDir, "Libs");
            if (Directory.Exists(libsDir))
            {
                foreach (string dll in Directory.GetFiles(libsDir, "*.dll"))
                {
                    if (IsCoreDll(dll)) continue;
                    Console.WriteLine($"[ScriptLoader] Found custom DLL (Libs): {dll}");
                    LoadAndRegister(dll);
                }
            }

            // Rebuild is owned by BuildProjectScripts (Play / Editor EnsureProjectScriptsActivated).
            // Do not skip-load when Libs exists — caller builds first when sources changed.
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
                if (IsCoreDll(dll)) continue;
                string target = Path.Combine(runtimeTemp, Path.GetFileName(dll));
                File.Copy(dll, target, true);
                Console.WriteLine($"[ScriptLoader] Copied custom DLL to runtime temp: {target}");
            }

            string libsDir = Path.Combine(scriptsDir, "Libs");
            if (Directory.Exists(libsDir))
            {
                foreach (string dll in Directory.GetFiles(libsDir, "*.dll"))
                {
                    if (IsCoreDll(dll)) continue;
                    string target = Path.Combine(runtimeTemp, Path.GetFileName(dll));
                    File.Copy(dll, target, true);
                    Console.WriteLine($"[ScriptLoader] Copied custom DLL (Libs) to runtime temp: {target}");
                }
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
                if (IsCoreDll(dll)) continue;
                File.Copy(dll, Path.Combine(targetScripts, Path.GetFileName(dll)), true);
            }

            string libsDir = Path.Combine(scriptsDir, "Libs");
            if (Directory.Exists(libsDir))
            {
                string targetLibs = Path.Combine(targetScripts, "Libs");
                Directory.CreateDirectory(targetLibs);
                foreach (string dll in Directory.GetFiles(libsDir, "*.dll"))
                {
                    if (IsCoreDll(dll)) continue;
                    File.Copy(dll, Path.Combine(targetLibs, Path.GetFileName(dll)), true);
                }
            }
            Console.WriteLine($"[ScriptLoader] Copied Scripts to export folder");
        }

        public static string GetCustomAssemblyList(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return "";
            string scriptsDir = Path.Combine(projectPath, "Scripts");
            if (!Directory.Exists(scriptsDir)) return "";

            var dlls = new List<string>();
            dlls.AddRange(Directory.GetFiles(scriptsDir, "*.dll").Where(d => !IsCoreDll(d)));
            string libsDir = Path.Combine(scriptsDir, "Libs");
            if (Directory.Exists(libsDir))
                dlls.AddRange(Directory.GetFiles(libsDir, "*.dll").Where(d => !IsCoreDll(d)));

            return string.Join(";", dlls.ConvertAll(Path.GetFileName));
        }

        public static void LoadCustomAssemblies(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return;
            string runtimeTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuntimeTemp");
            bool loadedAny = false;
            if (Directory.Exists(runtimeTemp))
            {
                foreach (string dll in Directory.GetFiles(runtimeTemp, "*.dll"))
                {
                    if (IsCoreDll(dll)) continue;
                    LoadAndRegister(dll);
                    loadedAny = true;
                }
            }
            if (!loadedAny)
                ScanProjectScripts(projectPath);
        }

        private static void LoadAndRegister(string dllPath)
        {
            if (IsCoreDll(dllPath)) return;
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
                    if (type.GetCustomAttributes(typeof(RegisterHostedContentAttribute), false).Length > 0)
                        Console.WriteLine($"[ScriptLoader] Discovered [RegisterHostedContent]: {type.FullName}");
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

                        // Hosted content / HUD (content-only; host supplies chrome)
                        if (type.GetCustomAttributes(typeof(RegisterHostedContentAttribute), false).Length > 0 &&
                            typeof(IHostedContent).IsAssignableFrom(type))
                        {
                            try
                            {
                                string key = type.Name;
                                Type captured = type;
                                HostedContentRegistry.Register(key, (SceneContext c) =>
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
                                    return ResolveInstance(captured, localServices) as IHostedContent;
                                });
                                Console.WriteLine($"[ScriptLoader] Registered hosted content: {key}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ScriptLoader] Failed to register hosted content {type.Name}: {ex.Message}");
                            }
                        }
                    }
                }
            }

            HostedContentRegistry.OpenRegistered(ctx);
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

            ConstructorInfo parameterless = type.GetConstructor(Type.EmptyTypes);
            if (parameterless != null)
                return parameterless.Invoke(null);

            throw new InvalidOperationException($"No satisfiable constructor found for {type.FullName}");
        }

        public static void RegisterCustomSystems(EventBus eventBus, IGameServer server)
        {
            Console.WriteLine("[ScriptLoader] RegisterCustomSystems (legacy) – prefer ActivateProjectScripts(SceneContext)");
        }

        public static void ApplyCustomPlayerControllerIfPresent(Player player, ref PlayerMovement movement)
        {
            Console.WriteLine("[ScriptLoader] ApplyCustomPlayerControllerIfPresent (legacy) – activation deferred to ActivateProjectScripts");
        }

        public static void ApplyControllerByTypeName(string typeName, Player player, ref PlayerMovement movement)
        {
            Console.WriteLine("[ScriptLoader] ApplyControllerByTypeName (legacy) – activation deferred to ActivateProjectScripts");
        }


        private static void CopyIfNewer(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return;
            if (!File.Exists(source)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
            var srcInfo = new FileInfo(source);
            if (File.Exists(target))
            {
                var dstInfo = new FileInfo(target);
                if (dstInfo.Length == srcInfo.Length && dstInfo.LastWriteTimeUtc >= srcInfo.LastWriteTimeUtc)
                {
                    Console.WriteLine($"[ScriptLoader] {Path.GetFileName(target)} up to date ({dstInfo.Length} bytes) at {target}");
                    return;
                }
                File.SetAttributes(target, FileAttributes.Normal);
                File.Delete(target);
            }
            File.Copy(source, target, true);
            var copied = new FileInfo(target);
            Console.WriteLine($"[ScriptLoader] Copied {Path.GetFileName(target)} ({copied.Length} bytes, {srcInfo.LastWriteTimeUtc:u}) -> {target}");
        }

        public static bool BuildProjectScripts(string projectPath, string customOutputDir = null)
        {
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath)) return false;
            string scriptsDir = Path.Combine(projectPath, "Scripts");
            Directory.CreateDirectory(scriptsDir);
            string libsDir = Path.Combine(scriptsDir, "Libs");
            Directory.CreateDirectory(libsDir);
            string outputPath = customOutputDir ?? Path.Combine(scriptsDir, "BuildOut", DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            Directory.CreateDirectory(outputPath);
            PruneBuildOutStamps(Path.Combine(scriptsDir, "BuildOut"), outputPath);
            // Prefer the loaded engine assembly, not a leftover in BaseDirectory.
            string binDir = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                string loaded = typeof(SceneContext).Assembly.Location;
                if (!string.IsNullOrEmpty(loaded) && File.Exists(loaded))
                    binDir = Path.GetDirectoryName(loaded) ?? binDir;
            }
            catch { }

            string[] coreDlls = { "SiegeEngine.dll", "Foundation.dll" };
            foreach (string dllName in coreDlls)
            {
                string source = Path.Combine(binDir, dllName);
                if (!File.Exists(source))
                    source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dllName);
                if (!File.Exists(source))
                {
                    Console.WriteLine($"[ScriptLoader] Core DLL missing at runtime: {dllName}");
                    continue;
                }
                string[] targets =
                {
                    Path.Combine(scriptsDir, dllName),
                    Path.Combine(libsDir, dllName),
                    Path.Combine(outputPath, dllName)
                };
                foreach (string target in targets)
                {
                    try
                    {
                        CopyIfNewer(source, target);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ScriptLoader] FAILED copying {dllName} to {target}: {ex.Message}");
                    }
                }
            }

            string csprojPath = Path.Combine(scriptsDir, "SiegeScripts.csproj");
            string siegeHint = Path.Combine(scriptsDir, "SiegeEngine.dll");
            string foundationHint = Path.Combine(scriptsDir, "Foundation.dll");
            string csproj =
                "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
                + "  <PropertyGroup>\n"
                + "    <TargetFramework>net9.0</TargetFramework>\n"
                + "    <OutputType>Library</OutputType>\n"
                + "    <OutputPath>Libs\\</OutputPath>\n"
                + "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>\n"
                + "    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>\n"
                + "    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>\n"
                + "  </PropertyGroup>\n"
                + "  <ItemGroup>\n"
                + "    <Reference Include=\"SiegeEngine\">\n"
                + "      <HintPath>" + siegeHint + "</HintPath>\n"
                + "      <Private>false</Private>\n"
                + "    </Reference>\n"
                + "    <Reference Include=\"Foundation\">\n"
                + "      <HintPath>" + foundationHint + "</HintPath>\n"
                + "      <Private>false</Private>\n"
                + "    </Reference>\n"
                + "  </ItemGroup>\n"
                + "  <ItemGroup>\n"
                + "    <Compile Include=\"**/*.cs\" Exclude=\"obj/**/*.cs;Libs/**/*.cs;BuildOut/**/*.cs\" />\n"
                + "  </ItemGroup>\n"
                + "</Project>\n";
            File.WriteAllText(csprojPath, csproj);
            Console.WriteLine("[ScriptLoader] Wrote SiegeScripts.csproj HintPath=" + siegeHint);

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
                bool csharpFailed = ContainsCsharpError(output) || ContainsCsharpError(err);
                string builtDll = FindBuiltProjectDll(outputPath, scriptsDir);
                if (!csharpFailed && builtDll != null)
                {
                    string runtimeDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RuntimeTemp");
                    Directory.CreateDirectory(runtimeDir);
                    string runtimeTarget = Path.Combine(runtimeDir, Path.GetFileName(builtDll));
                    File.Copy(builtDll, runtimeTarget, true);
                    Console.WriteLine($"[ScriptLoader] Staged compiled scripts at {runtimeTarget}");
                    string libsTarget = Path.Combine(libsDir, Path.GetFileName(builtDll));
                    try
                    {
                        if (File.Exists(libsTarget))
                        {
                            File.SetAttributes(libsTarget, FileAttributes.Normal);
                            File.Delete(libsTarget);
                        }
                        File.Copy(builtDll, libsTarget, true);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("[ScriptLoader] Libs/" + Path.GetFileName(builtDll) + " in use — using RuntimeTemp");
                    }
                    LoadAndRegister(builtDll);
                    ScanProjectScripts(projectPath);
                    Console.WriteLine("[ScriptLoader] Build → DLL copy → reflection register COMPLETE. Custom controllers now active for Play/Export.");
                    CopyProjectScripts(projectPath);
                    return true;
                }
                Console.WriteLine($"[ScriptLoader] Build FAILED. Exit {process.ExitCode}. Not copying stale Libs DLL.");
                if (!string.IsNullOrEmpty(err))
                    Console.WriteLine($"[ScriptLoader] Build error: {err}");
                return false;
            }
            return false;
        }

        public static bool PrepareProjectForPlay(string projectPath)
        {
            if (!BuildProjectScripts(projectPath))
                return false;
            CopyProjectScripts(projectPath);
            return true;
        }

        private static void PruneBuildOutStamps(string buildOutRoot, string keepPath)
        {
            if (string.IsNullOrEmpty(buildOutRoot) || !Directory.Exists(buildOutRoot)) return;
            string keepName = Path.GetFileName(keepPath);
            var dirs = new DirectoryInfo(buildOutRoot).GetDirectories();
            Array.Sort(dirs, (a, b) => b.CreationTimeUtc.CompareTo(a.CreationTimeUtc));
            int kept = 0;
            for (int i = 0; i < dirs.Length; i++)
            {
                if (string.Equals(dirs[i].Name, keepName, StringComparison.OrdinalIgnoreCase))
                    continue;
                kept++;
                if (kept <= 1) continue; // keep one previous stamp
                try { dirs[i].Delete(true); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ScriptLoader] Could not prune {dirs[i].FullName}: {ex.Message}");
                }
            }
        }

        private static bool ContainsCsharpError(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf("error CS", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FindBuiltProjectDll(string outputPath, string scriptsDir)
        {
            string Pick(string dir)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
                foreach (string dll in Directory.GetFiles(dir, "*.dll"))
                {
                    if (IsCoreDll(dll)) continue;
                    return dll;
                }
                return null;
            }
            string found = Pick(outputPath);
            if (found != null) return found;
            found = Pick(Path.Combine(scriptsDir, "obj", "Release"));
            if (found != null) return found;
            found = Pick(Path.Combine(scriptsDir, "obj", "Release", "net9.0"));
            return found;
        }

    }

    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterGameSystemAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class CustomPlayerControllerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class CustomSceneEntryAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterHostedContentAttribute : Attribute { }
}