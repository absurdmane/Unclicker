using System.Linq;
using System.Reflection;
using BepInEx.Unity.Mono.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Unclicker
{
    internal static class RecipeButtonPatches
    {
        private const string InventoryTweaksGuid = "lomserman.obenseuer.inventorytweaks";
        private static bool inventoryTweaksChecked;

        /// <summary>
        /// A recipe whose conditions the station doesn't meet (required / excluded modifiers) gets the vanilla "can't make" button.
        /// Vanilla lists it as a normal button and only greys out the popup's create button, which we hide.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(RecipeListUI), nameof(RecipeListUI.CreateButton))]
        private static void CreateButtonPrefix(Recipe recipe, CraftingBase activeCraftingBase, bool disabled, ref bool missingIngredients)
        {
            if (disabled || missingIngredients || recipe == null || Stations.KindOf(activeCraftingBase) == StationKind.None)
                return;
            if (!RecipeInfoUI.CheckModifiers(recipe, activeCraftingBase.ModifiersWithUpgrades))
                missingIngredients = true;
        }

        /// <summary>Every recipe button of a station we handle gets hover info and an <see cref="UnclickerButton"/>.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(RecipeListUI), nameof(RecipeListUI.CreateButton))]
        private static void CreateButtonPostfix(RecipeListUI __instance, CraftingBase activeCraftingBase)
        {
            StationKind kind = Stations.KindOf(activeCraftingBase);
            if (kind == StationKind.None || __instance.recipeButtons.Count == 0)
                return;
            RecipeButton button = __instance.recipeButtons[__instance.recipeButtons.Count - 1].GetComponent<RecipeButton>();
            if (button == null)
                return;

            if (kind == StationKind.Growing)
                RemoveInventoryTweaksShiftClick();

            button.SetTogglePress(false);
            __instance.recipeInfo.toggleInfoPress = false;
            button.gameObject.AddComponent<UnclickerButton>().Init(button, kind);
        }

        /// <summary>A left click on our buttons crafts / plants right away instead of opening the info panel.</summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(RecipeButton), nameof(RecipeButton.OnPointerClick))]
        private static bool OnPointerClickPrefix(RecipeButton __instance, PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return true;
            UnclickerButton unclicker = __instance.GetComponent<UnclickerButton>();
            if (unclicker == null)
                return true;
            __instance.Pressed(); // click sound
            unclicker.Click(Input.GetKey(Plugin.ModifierKey.Value));
            return false;
        }

        /// <summary>Hover info shows our amount instead of the vanilla one.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(RecipeButton), nameof(RecipeButton.OnPointerEnter))]
        private static void OnPointerEnterPostfix(RecipeButton __instance) => __instance.GetComponent<UnclickerButton>()?.ShowInfo();

        /// <summary>Same after the list is rebuilt (<see cref="RecipeListUI.UpdateRecipes"/> re-shows the current recipe).</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(RecipeButton), nameof(RecipeButton.ShowRecipe))]
        private static void ShowRecipePostfix(RecipeButton __instance) => __instance.GetComponent<UnclickerButton>()?.ShowInfo();

        /// <summary>
        /// Inventory Tweaks plants the max on Shift+click. HarmonyX runs every prefix even after ours returns false,
        /// so on growing buttons both would fire. We own Shift+click there: remove only their prefix on this one method.
        /// </summary>
        private static void RemoveInventoryTweaksShiftClick()
        {
            if (inventoryTweaksChecked)
                return;
            inventoryTweaksChecked = true;
            if (Plugin.Harmony == null || !UnityChainloader.Instance.Plugins.TryGetValue(InventoryTweaksGuid, out var info) || info.Instance == null)
                return;

            Assembly theirs = info.Instance.GetType().Assembly;
            MethodBase target = AccessTools.Method(typeof(RecipeButton), nameof(RecipeButton.OnPointerClick));
            Patches patches = Harmony.GetPatchInfo(target);
            if (patches == null)
                return;
            foreach (Patch prefix in patches.Prefixes.Where(p => p.PatchMethod.DeclaringType?.Assembly == theirs).ToList())
            {
                Plugin.Harmony.Unpatch(target, prefix.PatchMethod);
                Plugin.Log.LogInfo($"Inventory Tweaks: removed its Shift+click prefix ({prefix.PatchMethod.DeclaringType?.Name}.{prefix.PatchMethod.Name}); Unclicker handles Shift+click on growing devices.");
            }
        }
    }
}
