// SiegeEngine/Managers/MenuRegistry.cs
using SiegeEngine.Rendering.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SiegeEngine.Managers
{
    public class MenuRegistry
    {
        private readonly List<MenuDefinition> _allMenus = new List<MenuDefinition>();

        public void RegisterBaseMenus(List<MenuDefinition> baseMenus)
        {
            _allMenus.AddRange(baseMenus);
            Console.WriteLine($"MenuRegistry: Registered {baseMenus.Count} base menus.");
        }

        public void RegisterExtensions(List<MenuDefinition> extensions)
        {
            foreach (var extMenu in extensions)
            {
                var existingMenu = _allMenus.FirstOrDefault(m => m.Name == extMenu.Name);
                if (existingMenu != null)
                {
                    // Merge: Append buttons and elements
                    if (extMenu.Buttons != null)
                    {
                        existingMenu.Buttons = existingMenu.Buttons ?? new List<ButtonDefinition>();
                        existingMenu.Buttons.AddRange(extMenu.Buttons);
                    }
                    if (extMenu.Elements != null)
                    {
                        existingMenu.Elements = existingMenu.Elements ?? new List<Dictionary<string, object>>();
                        existingMenu.Elements.AddRange(extMenu.Elements);
                    }
                    if (extMenu.Tabs != null)
                    {
                        existingMenu.Tabs = existingMenu.Tabs ?? new List<TabDefinition>();
                        existingMenu.Tabs.AddRange(extMenu.Tabs);
                    }
                    Console.WriteLine($"MenuRegistry: Merged extension into existing menu '{extMenu.Name}'.");
                }
                else
                {
                    _allMenus.Add(extMenu);
                    Console.WriteLine($"MenuRegistry: Added new menu '{extMenu.Name}' from extension.");
                }
            }
        }

        public List<MenuDefinition> GetAllMenus()
        {
            return _allMenus.ToList();
        }

        public MenuDefinition GetMenuByName(string name)
        {
            return _allMenus.FirstOrDefault(m => m.Name == name);
        }
    }
}