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
        public const string Version = "1.4.0";

        internal static Plugin Instance;
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
        internal static ConfigEntry<bool> AutolootEnabled;
        internal static ConfigEntry<string> AutolootItems;
        internal static ConfigEntry<KeyCode> AutolootKey;
        internal static ConfigEntry<bool> PaintEnabled;
        internal static ConfigEntry<KeyCode> PaintKey;

        private void Awake()
        {
            Instance = this;
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
                "When your inventory fills up during harvest all. StopWhenFull: everything that fits your inventory is harvested, plant by " +
                "plant and fruit by fruit; a plant whose whole harvest doesn't fit stays as it is. DropRest: harvest everything and drop " +
                "what doesn't fit at your feet (from the panel: into the device's storage first, as in vanilla).");
            CellHarvestToPlayer = Config.Bind("Farming", "CellHarvestToPlayer", true,
                "A click on a plant cell in the growing panel puts the harvest into your inventory when it fits (vanilla: into the device's storage).");
            PlantOrder = Config.Bind("Farming", "PlantingOrder", PlantingOrder.WorstFirst,
                "Which items planting uses up. WorstFirst: lowest quality first, then the least fresh, so the good ones stay. Vanilla: slot order.");
            InstantDeadRemoval = Config.Bind("Farming", "InstantDeadRemoval", true,
                "A dead plant is removed with a press instead of a hold. Uprooting a live plant stays a hold.");

            AutolootEnabled = Config.Bind("Autoloot", "Enabled", true,
                "MarkKey on an item in an inventory panel adds its kind to the autoloot list (again: removes it). Opening a container " +
                "outside the tenement takes those items into your inventory, except ones that would be stolen.");
            AutolootKey = Config.Bind("Autoloot", "MarkKey", KeyCode.Mouse2,
                "Press over an item to mark it for autoloot or unmark it. Mouse2 = middle mouse button.");
            AutolootItems = Config.Bind("Autoloot", "Items", "",
                "Item IDs on the autoloot list, comma-separated. Filled by MarkKey.");

            PaintEnabled = Config.Bind("QuickMovePaint", "Enabled", true,
                "Hold Key and move the mouse over items: each one is quick-moved, whole stack, between your inventory and the open container " +
                "or station. Not in trade, bottle recycling or liquid storage; clothes and hand slots are left alone.");
            PaintKey = Config.Bind("QuickMovePaint", "Key", KeyCode.LeftAlt,
                "Hold to quick-move every item the mouse passes over.");

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
            Harmony.PatchAll(typeof(Autoloot));
            TakeAllButton.AddToExisting();
            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void Update()
        {
            if (Harmony == null)
                return;
            Autoloot.Update();
            QuickMovePaint.Update();
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
            Autoloot.Unload();
            QuickMovePaint.Unload();
        }
    }
}
