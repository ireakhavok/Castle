// Folder: SiegeEngine.Core.UI
// File: DataHookProcessor.cs
using System;
using System.IO;
using System.Reflection;
using SiegeEngine.Core.ContextManagement;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Rendering;
using SiegeEngine.Core.UI; // for BasePanel

namespace SiegeEngine.Core.UI
{
    public static class DataHookProcessor
    {
        public static void Process(string hook, IRenderContext renderContext, IControlContext controlContext, nint window, EventBus eventBus, UIOverlay overlayForFormData = null, BasePanel callerPanel = null)
        {
            if (string.IsNullOrEmpty(hook)) return;

            Console.WriteLine($"[DataHookProcessor] Processing data-hook: {hook}");

            var parts = hook.Split('.');
            if (parts.Length > 2)
            {
                string dllName = parts[0] + ".dll";
                string typeName = string.Join(".", parts, 0, parts.Length - 1);
                string methodName = parts[parts.Length - 1];

                string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dllName);
                Assembly ass = null;
                if (File.Exists(dllPath))
                {
                    try
                    {
                        ass = Assembly.LoadFrom(dllPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"DataHookProcessor: Failed to load {dllPath}: {ex.Message}");
                    }
                }

                Type type = ass?.GetType(typeName) ?? Type.GetType(typeName);
                if (type != null)
                {
                    if (overlayForFormData != null)
                    {
                        MethodInfo mi5 = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public, null,
                            new Type[] { typeof(IRenderContext), typeof(IControlContext), typeof(nint), typeof(EventBus), typeof(UIOverlay) }, null);
                        if (mi5 != null)
                        {
                            try
                            {
                                mi5.Invoke(null, new object[] { renderContext, controlContext, window, eventBus, overlayForFormData });
                                Console.WriteLine($"[DataHookProcessor] SUCCESS (5-param): {hook}");
                                goto autoClose;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"DataHookProcessor: Error calling 5-param {methodName}: {ex.Message}");
                            }
                        }
                    }

                    MethodInfo mi = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public, null,
                        new Type[] { typeof(IRenderContext), typeof(IControlContext), typeof(nint), typeof(EventBus) }, null);
                    if (mi != null)
                    {
                        mi.Invoke(null, new object[] { renderContext, controlContext, window, eventBus });
                        Console.WriteLine($"[DataHookProcessor] SUCCESS (4-param): {hook}");
                        goto autoClose;
                    }
                    else
                    {
                        Console.WriteLine($"DataHookProcessor: Failed to find static method {methodName} in type {typeName}");
                    }
                }
                else
                {
                    Console.WriteLine($"DataHookProcessor: Failed to find type {typeName}");
                }
            }

            if (eventBus != null)
            {
                eventBus.Publish(new GenericEvent { Hook = hook });
                Console.WriteLine($"[DataHookProcessor] Published GenericEvent with hook {hook}");
            }
            else
            {
                Console.WriteLine($"[DataHookProcessor] WARNING: No EventBus available for hook {hook}");
            }

            // Auto-close only when a callerPanel is provided (MenuPanel passes itself, other panels do not)
        autoClose:
            if (callerPanel != null && eventBus != null)
            {
                eventBus.Publish(new ClosePanelEvent(callerPanel));
                Console.WriteLine($"[DataHookProcessor] Auto-closed caller panel after hook: {hook}");
            }
        }
    }
}