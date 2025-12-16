using SiegeEngine.Core.Interfaces;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SiegeEngine.Core.Managers
{
    public class WorkshopManager
    {
        private readonly ISteamEngine _steamEngine;

        public WorkshopManager(ISteamEngine steamEngine)
        {
            _steamEngine = steamEngine ?? throw new ArgumentNullException(nameof(steamEngine));
            nint steamUGC = SteamAPI_SteamUGC_v021();
            if (steamUGC == nint.Zero)
            {
                throw new InvalidOperationException("Failed to get ISteamUGC interface.");
            }
            Console.WriteLine("WorkshopManager: ISteamUGC interface acquired.");
        }

        public void PublishMod(string modJsonPath)
        {
            if (!File.Exists(modJsonPath))
            {
                Console.WriteLine($"WorkshopManager: Mod file not found at {modJsonPath}");
                return;
            }

            string json = File.ReadAllText(modJsonPath);
            var modInfo = JsonSerializer.Deserialize<Definitions.ModInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (modInfo == null)
            {
                Console.WriteLine($"WorkshopManager: Failed to deserialize mod info from {modJsonPath}");
                return;
            }

            string contentPath = modInfo.Path; // Use Path instead of BaseDirectory
            _steamEngine.CreateWorkshopItem(modInfo.Name, modInfo.Name, contentPath); // Remove Description
            Console.WriteLine($"WorkshopManager: Published mod '{modInfo.Name}' from {contentPath}");
        }

        public void ListSubscribedMods()
        {
            ulong[] subscribedItems = _steamEngine.GetSubscribedWorkshopItems();
            Console.WriteLine($"Found {subscribedItems.Length} subscribed Workshop items.");
            foreach (ulong itemId in subscribedItems)
            {
                string installInfo = _steamEngine.GetWorkshopItemInstallInfo(itemId);
                Console.WriteLine($"Subscribed Item ID: {itemId}, Install Info: {installInfo ?? "Not installed"}");
            }
        }

        [DllImport("steam_api64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_SteamUGC_v021")]
        private static extern nint SteamAPI_SteamUGC_v021();
    }
}