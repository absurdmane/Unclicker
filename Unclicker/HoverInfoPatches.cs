using HarmonyLib;
using UnityEngine;

namespace Unclicker
{
    /// <summary>
    /// Steadies the hover info panel of our recipe buttons.
    /// Vanilla hides the panel on every pointer exit and shows it again on the next enter, so whatever plays when it appears
    /// replays on each button, and the panel is gone before the mouse can reach it to scroll it. Here a hide that comes from leaving
    /// one of our buttons is held while the mouse is over the recipe list or over the panel itself, and happens
    /// <see cref="HideDelay"/> after the mouse has left both. Another button showing a recipe drops it.
    /// Also, a redraw hides the old rows at once: Destroy only removes them at the end of the frame, so for one frame the old and
    /// the new rows were laid out together.
    /// </summary>
    [HarmonyPatch]
    internal static class HoverInfoPatches
    {
        private const float HideDelay = 0.3f;

        private static RecipeButton exitingOurButton;
        private static bool createBarLogged;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RecipeButton), nameof(RecipeButton.OnPointerExit))]
        private static void OnPointerExitPrefix(RecipeButton __instance) =>
            exitingOurButton = __instance.GetComponent<UnclickerButton>() != null ? __instance : null;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RecipeButton), nameof(RecipeButton.OnPointerExit))]
        private static void OnPointerExitPostfix() => exitingOurButton = null;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RecipeInfoUI), nameof(RecipeInfoUI.HideRecipe))]
        private static bool HideRecipePrefix(RecipeInfoUI __instance)
        {
            if (exitingOurButton == null || !__instance.gameObject.activeInHierarchy)
                return true;
            DelayedHide.For(__instance).Hold(exitingOurButton.GetComponentInParent<RecipeListUI>());
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RecipeInfoUI), nameof(RecipeInfoUI.ShowRecipe))]
        private static void ShowRecipePrefix(RecipeInfoUI __instance)
        {
            __instance.GetComponent<DelayedHide>()?.Cancel();
            if (__instance.recipeInfoListPanel == null)
                return;
            foreach (Transform row in __instance.recipeInfoListPanel.transform)
                row.gameObject.SetActive(false);
        }

        /// <summary>
        /// On our stations a click on the recipe already crafts, so the panel's own Craft / Plant / Start button is redundant: hide it
        /// with its bar. A process outside its loading stage keeps the panel's Abort / Fill button.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(RecipeInfoUI), nameof(RecipeInfoUI.ShowRecipe))]
        private static void ShowRecipePostfix(RecipeInfoUI __instance)
        {
            if (__instance.createButtonPanel == null)
                return;
            CraftingBase station = CraftingBase.currentCraftingBase;
            bool redundant = Plugin.HideCreateButton.Value && Stations.KindOf(station) != StationKind.None &&
                             !(station is manufacturingProcess process && process.stage != manufacturingProcess.Stage.LoadingItems);
            GameObject bar = CreateBar(__instance);
            if (bar.activeSelf == redundant)
            {
                bar.SetActive(!redundant);
                RowsArea.For(__instance, bar).Extend(redundant);
            }
        }

        /// <summary>Puts the info popup back as the game made it: create bar shown, rows area at its size, our helpers gone.</summary>
        public static void Unload()
        {
            foreach (RowsArea r in Object.FindObjectsByType<RowsArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.bar != null)
                    r.bar.gameObject.SetActive(true);
                r.Extend(false);
                Object.Destroy(r);
            }
            foreach (DelayedHide d in Object.FindObjectsByType<DelayedHide>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(d);
        }

        /// <summary>
        /// The scroll area of the recipe rows ends where the create bar begins. With the bar hidden, its bottom edge moves down by the
        /// bar's height so the rows fill the freed space; it moves back when the bar shows again.
        /// </summary>
        private sealed class RowsArea : MonoBehaviour
        {
            private RectTransform area;
            internal RectTransform bar;
            private Vector2 originalOffsetMin;
            private bool extended;

            public static RowsArea For(RecipeInfoUI info, GameObject bar)
            {
                RowsArea r = info.GetComponent<RowsArea>();
                if (r != null)
                    return r;
                r = info.gameObject.AddComponent<RowsArea>();
                r.bar = bar.transform as RectTransform;
                UnityEngine.UI.ScrollRect scroll = info.recipeInfoListPanel != null
                    ? info.recipeInfoListPanel.GetComponentInParent<UnityEngine.UI.ScrollRect>(true)
                    : null;
                r.area = scroll != null ? scroll.transform as RectTransform : null;
                if (r.area != null)
                    r.originalOffsetMin = r.area.offsetMin;
                Plugin.Log.LogInfo($"Info popup rows area: '{(r.area != null ? r.area.name : "none")}'");
                return r;
            }

            public void Extend(bool extend)
            {
                if (area == null || bar == null || extend == extended)
                    return;
                extended = extend;
                if (!extend)
                {
                    area.offsetMin = originalOffsetMin;
                    return;
                }
                // The bar's height in the area's parent units (they may sit under different scales).
                float parentScale = area.parent != null ? area.parent.lossyScale.y : 1f;
                float height = bar.rect.height * bar.lossyScale.y / (parentScale == 0f ? 1f : parentScale);
                area.offsetMin = originalOffsetMin - new Vector2(0f, height);
            }
        }

        /// <summary>
        /// The button sits in a bar with its own background. The bar is the topmost parent of the button panel that doesn't also
        /// hold the recipe rows; when the button panel is a direct child of the info panel, it's the button panel itself.
        /// </summary>
        private static GameObject CreateBar(RecipeInfoUI info)
        {
            Transform bar = info.createButtonPanel.transform;
            Transform rows = info.recipeInfoListPanel != null ? info.recipeInfoListPanel.transform : null;
            while (bar.parent != null && bar.parent != info.transform && (rows == null || !rows.IsChildOf(bar.parent)))
                bar = bar.parent;
            if (!createBarLogged)
            {
                createBarLogged = true;
                Plugin.Log.LogInfo($"Info popup create bar: '{bar.name}' (button panel '{info.createButtonPanel.name}', info '{info.name}')");
            }
            return bar.gameObject;
        }

        private sealed class DelayedHide : MonoBehaviour
        {
            private RecipeInfoUI info;
            private RectTransform list;
            private bool holding;
            private float leftAt;

            public static DelayedHide For(RecipeInfoUI info)
            {
                DelayedHide d = info.GetComponent<DelayedHide>() ?? info.gameObject.AddComponent<DelayedHide>();
                d.info = info;
                return d;
            }

            public void Hold(RecipeListUI recipeList)
            {
                list = recipeList != null ? recipeList.transform as RectTransform : null;
                holding = true;
                leftAt = Time.unscaledTime;
            }

            public void Cancel() => holding = false;

            // Runs while the panel is active; once it's hidden there is nothing left to do.
            private void Update()
            {
                if (!holding)
                    return;
                if (Hovered(list) || Hovered(transform as RectTransform))
                {
                    leftAt = Time.unscaledTime;
                    return;
                }
                if (Time.unscaledTime - leftAt < HideDelay)
                    return;
                holding = false;
                info.HideRecipe();
            }

            private static bool Hovered(RectTransform rect)
            {
                if (rect == null || !rect.gameObject.activeInHierarchy)
                    return false;
                Canvas canvas = rect.GetComponentInParent<Canvas>();
                Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera);
            }
        }
    }
}
