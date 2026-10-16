using System.Collections.Generic;
using System.Globalization;
using OS.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Unclicker
{
    /// <summary>
    /// Added to a recipe button: keeps the amount, handles Shift+wheel, runs the craft on click.
    /// The recipe list is rebuilt after every craft, so each instance lives until the next craft and starts at max.
    /// </summary>
    internal sealed class UnclickerButton : MonoBehaviour, IScrollHandler
    {
        private RecipeButton button;
        private StationKind kind;
        private TextMeshProUGUI label;
        private string baseText;
        private bool hasAmount;
        private int amount;
        private int max;

        public void Init(RecipeButton recipeButton, StationKind stationKind)
        {
            button = recipeButton;
            kind = stationKind;
            label = GetComponentInChildren<TextMeshProUGUI>();
            baseText = label != null ? label.text : "";

            if (!button.disabledButton)
            {
                switch (button.activeCraftingBase)
                {
                    case Growing growing when kind == StationKind.Growing:
                        hasAmount = true;
                        max = Mathf.Min(growing.GetRecipeMultiplier(button.recipe), growing.FreeGrowingSlotCount());
                        break;
                    case manufacturingProcess process when kind == StationKind.Process
                                                           && process.stage == manufacturingProcess.Stage.LoadingItems:
                        hasAmount = true;
                        max = process.GetRecipeMultiplierShowValue(button.recipe);
                        break;
                }
            }
            amount = max;
            UpdateLabel();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (hasAmount && max > 1 && Input.GetKey(Plugin.ModifierKey.Value))
            {
                int step = eventData.scrollDelta.y > 0 ? 1 : -1;
                amount = Mathf.Clamp(amount + step, 1, max);
                UpdateLabel();
                ShowInfo();
                return;
            }
            // A plain wheel scrolls the recipe list: pass the event up to the parent ScrollRect.
            if (transform.parent != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.scrollHandler);
        }

        public void Click(bool modifierHeld)
        {
            CraftingBase station = button.activeCraftingBase;
            Recipe recipe = button.recipe;
            if (station == null || recipe == null || button.disabledButton)
                return;
            if (!RecipeInfoUI.CheckModifiers(recipe, Modifiers()))
                return;

            switch (station)
            {
                case Growing growing:
                    growing.SetSelectedRecipe(recipe);
                    if (modifierHeld)
                        growing.Grow(); // vanilla path: the amount popup (or plants at once when only one fits)
                    else if (amount > 0)
                        growing.StartGrow(amount);
                    break;

                case manufacturingProcess process:
                    if (modifierHeld || process.stage != manufacturingProcess.Stage.LoadingItems || amount <= 0)
                        return;
                    process.SetSelectedRecipe(recipe);
                    process.ActiveRecipe = new KeyValuePair<Recipe, int>(process.ActiveRecipe.Key ?? recipe, amount);
                    process.StartProcess();
                    break;

                case manufacturing manufacturing:
                    if (modifierHeld || button.recipeInfo.craftingDisabled)
                        return;
                    manufacturing.SetSelectedRecipe(recipe);
                    manufacturing.Craft();
                    break;
            }
        }

        /// <summary>
        /// Redraws the hover info panel for our amount, so inputs and outputs show the totals.
        /// Vanilla shows growing recipes ×1 and processes at max.
        /// </summary>
        public void ShowInfo()
        {
            if (!hasAmount || max <= 0 || button.recipeInfo == null)
                return;
            CraftingBase station = button.activeCraftingBase;
            float qualityFactor = station.GetQualityFactor(button.recipe, craftingWasSuccess: true, amount);
            button.recipeInfo.ShowRecipe(button.recipe, amount, button.equipmentQuality, qualityFactor, dontHideOnDestroy: false, button.disabledButton);
            button.recipeInfo.currentButton = button;
        }

        /// <summary>The conditions the info panel checks the recipe against (Clean, Steam, ...).</summary>
        private List<RecipeCondition> Modifiers()
        {
            List<RecipeCondition> modifiers = button.recipeInfo.modifiers;
            return modifiers != null && modifiers.Count > 0 ? modifiers : button.activeCraftingBase.ModifiersWithUpgrades;
        }

        private void UpdateLabel()
        {
            if (label == null)
                return;
            // The amount goes first: long recipe names get cut off at the end.
            label.text = hasAmount && max > 0 ? $"{AmountText()} {baseText}" : baseText;
        }

        /// <summary>Liters for a liquid process ("5L"), otherwise the recipe multiplier ("6×").</summary>
        private string AmountText()
        {
            if (button.activeCraftingBase is manufacturingProcess process)
            {
                float perBatch = process.GetRecipeOutputLiquidAmount(button.recipe);
                if (perBatch <= 0f)
                    perBatch = process.GetRecipeInputLiquidAmount(button.recipe);
                if (perBatch > 0f)
                    return ItemLiquidData.SetAndGetLiquidAmount(perBatch * amount).ToString("0.##", CultureInfo.InvariantCulture) + "L";
            }
            return $"{amount}×";
        }
    }
}
