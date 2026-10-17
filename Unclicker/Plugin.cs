using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;

namespace Unclicker
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "obenseuer.absurdmane.unclicker";
        public const string Name = "Unclicker";
        public const string Version = "1.1.0";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> Growing;
        internal static ConfigEntry<bool> Manufacturing;
        internal static ConfigEntry<bool> ManufacturingProcess;
        internal static ConfigEntry<KeyCode> ModifierKey;
        internal static ConfigEntry<bool> TakeAll;
        internal static ConfigEntry<bool> HideCreateButton;
        internal static ConfigEntry<bool> IngredientsFromInventory;
        internal static ConfigEntry<bool> HarvestAll;
        internal static ConfigEntry<bool> HarvestAllHint;
        internal static ConfigEntry<bool> HarvestAllButton;
        internal static ConfigEntry<bool> HarvestDeadPlants;
        internal static ConfigEntry<HarvestOverflow> Overflow;
        internal static ConfigEntry<bool> CellHarvestToPlayer;
        internal static ConfigEntry<PlantingOrder> PlantOrder;
        internal static ConfigEntry<bool> InstantDeadRemoval;

        private void Awake()
        {
            Log = Logger;

            Growing = Config.Bind("Stations", "Growing", true,
                "Beds, terrariums and other growing devices: amount on the button (Shift+wheel), click plants, Shift+click opens the amount popup.");
            Manufacturing = Config.Bind("Stations", "Manufacturing", true,
                "Workstations that craft one item per click: click crafts, no amount.");
            ManufacturingProcess = Config.Bind("Stations", "ManufacturingProcess", true,
                "Brewing, distilling and other batch processes: amount on the button (Shift+wheel), click starts the process.");
            ModifierKey = Config.Bind("Controls", "ModifierKey", KeyCode.LeftShift,
                "Hold to change the amount with the mouse wheel, and for Shift+click on growing devices.");
            TakeAll = Config.Bind("Panels", "TakeAllButton", true,
                "A \"Take all\" button on station panels (not generators): moves everything from the station to your inventory. Needs a game restart.");

            HideCreateButton = Config.Bind("Panels", "HideCreateButton", true,
                "Hide the Craft / Plant / Start button in the recipe info popup on stations Unclicker handles (a click on the recipe already does it). " +
                "Its in-progress bar (e.g. Craft 2/3) goes with it.");

            IngredientsFromInventory = Config.Bind("Ingredients", "FromInventory", true,
                "Stations Unclicker handles also use ingredients from your inventory: recipes and amounts count them, and on start they move " +
                "into the station; what's left over (and empty bottles) comes back to you.");

            HarvestAll = Config.Bind("Farming", "HarvestAll", true,
                "ModifierKey + interact on a bed, terrarium or other growing device harvests every ripe plant into your inventory. " +
                "With nothing ripe it opens the device as usual.");
            HarvestAllHint = Config.Bind("Farming", "HarvestAllHint", true,
                "Show \"ModifierKey + E: harvest all\" under the device's name when there is something to harvest.");
            HarvestAllButton = Config.Bind("Farming", "HarvestAllButton", true,
                "A \"Harvest all\" button in the growing panel, next to Take all. On Linked Terrariums it harvests the whole group. Needs a game restart.");
            HarvestDeadPlants = Config.Bind("Farming", "HarvestDeadPlants", true,
                "Harvest all also clears dead plants (you get what uprooting gives).");
            Overflow = Config.Bind("Farming", "Overflow", HarvestOverflow.StopWhenFull,
                "When your inventory fills up during harvest all. StopWhenFull: stop before a plant whose harvest doesn't fit, the rest stays " +
                "on the plants. DropRest: keep going and drop what doesn't fit at your feet. From the panel, what doesn't fit you goes " +
                "into the device's storage first, as in vanilla.");
            CellHarvestToPlayer = Config.Bind("Farming", "CellHarvestToPlayer", true,
                "A click on a plant cell in the growing panel puts the harvest into your inventory when it fits (vanilla: into the device's storage).");
            PlantOrder = Config.Bind("Farming", "PlantingOrder", PlantingOrder.WorstFirst,
                "Which items planting uses up. WorstFirst: lowest quality first, then the least fresh, so the good ones stay. Vanilla: slot order.");
            InstantDeadRemoval = Config.Bind("Farming", "InstantDeadRemoval", true,
                "A dead plant is removed with a press instead of a hold. Uprooting a live plant stays a hold.");

            Enabled = Config.Bind("Mod", "Enabled", true,
                "Off = the mod does nothing at all: no patches, everything as in vanilla. Needs a game restart.");
            if (!Enabled.Value)
            {
                Log.LogInfo($"{Name} {Version} is off ([Mod] Enabled = false)");
                return;
            }

            Harmony = new Harmony(Guid);
            Harmony.PatchAll(typeof(RecipeButtonPatches));
            Harmony.PatchAll(typeof(HoverInfoPatches));
            Harmony.PatchAll(typeof(TakeAllButton));
            Harmony.PatchAll(typeof(InventoryIngredients));
            Harmony.PatchAll(typeof(Farming));
            TakeAllButton.AddToExisting();
            Log.LogInfo($"{Name} {Version} loaded");
        }

        // Undoes everything the mod did, so a fresh copy can be loaded next to it (DevMenu's script reload).
        private void OnDestroy()
        {
            if (Harmony == null)
                return;
            Harmony.UnpatchSelf();
            Harmony = null;
            TakeAllButton.Unload();
            HoverInfoPatches.Unload();
            UnclickerButton.UnloadAll();
        }
    }
}
