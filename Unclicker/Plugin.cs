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
        public const string Version = "1.0.0";

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
            Log.LogInfo($"{Name} {Version} loaded");
        }
    }
}
