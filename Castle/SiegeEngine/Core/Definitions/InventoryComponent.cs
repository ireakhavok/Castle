using System;
using System.Collections.Generic;

namespace SiegeEngine.Core.Definitions
{
    public class InventoryComponent : IComponent
    {
        public class Item
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int Tier { get; set; } // 1-5, like Diablo
            public Rarity Rarity { get; set; } // Color-coded borders
            public Dictionary<string, float> Stats { get; set; } // e.g., { "Damage": 10, "Speed": 1.5 }
            public int Level { get; set; } // Upgrade level
            public int StackSize { get; set; } // For stackable items
        }

        public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

        public Dictionary<string, Item> Items { get; } = new Dictionary<string, Item>();
        public int Cash { get; set; } // Cash pouch
        public int MaxSlots { get; set; } = 20;

        public InventoryComponent()
        {
            Cash = 0;
        }

        public bool AddItem(Item item)
        {
            if (Items.Count >= MaxSlots) return false;
            if (Items.ContainsKey(item.Id))
            {
                if (item.StackSize > 1) Items[item.Id].StackSize++;
                return true;
            }
            Items[item.Id] = item;
            return true;
        }

        public bool RemoveItem(string itemId)
        {
            if (!Items.ContainsKey(itemId)) return false;
            if (Items[itemId].StackSize > 1) Items[itemId].StackSize--;
            else Items.Remove(itemId);
            return true;
        }

        public bool UpgradeItem(string itemId)
        {
            if (!Items.ContainsKey(itemId)) return false;
            Items[itemId].Level++;
            foreach (var stat in Items[itemId].Stats)
            {
                Items[itemId].Stats[stat.Key] *= 1.1f; // 10% stat boost per level
            }
            return true;
        }
    }
}