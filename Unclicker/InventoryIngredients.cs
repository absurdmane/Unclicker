using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OS.Items;

namespace Unclicker
{
    /// <summary>
    /// Stations we handle also use ingredients from the player's inventory.
    /// - Listing and counting: while the open station lists its recipes or counts a batch, its slots are widened with copies of the
    ///   player's stacks (station first), so recipes show and the amount counts both. Nothing is changed there.
    /// - Starting (<c>StartGrow</c>, <c>StartProcess</c>, <c>Craft</c>, whichever button calls them): the player's stacks that fit the
    ///   recipe move into the station, the amount is clamped to what the station really holds, and the game crafts as usual
    ///   (quality, liquids, capacity). Then whatever the station has more of than before the move goes back to the player,
    ///   including empty bottles left by liquid ingredients.
    /// All three consume their ingredients right at the start, so the return can run straight after.
    /// </summary>
    [HarmonyPatch]
    internal static class InventoryIngredients
    {
        private static bool pulling;

        /// <remarks>
        /// Only while the station holds its own storage: Linked Terrariums briefly swaps in a neighbour's storage to count and plant
        /// from it, and the player's stacks must not be counted or pulled once per neighbour.
        /// </remarks>
        private static bool Applies(CraftingBase station) =>
            Plugin.IngredientsFromInventory.Value && station != null && station == CraftingBase.currentCraftingBase &&
            Stations.KindOf(station) != StationKind.None && station.InputOutputStorage != null && Inventory.instance != null &&
            station.InputOutputStorage == station.GetComponent<Storage>();

        // ---- listing and counting ----

        // Other mods widen the same array the same way (Obenseuer Modding Framework adds containers for Craft From Storage).
        // Harmony runs finalizers in the same order as prefixes, not in reverse, so at equal priority the swaps don't nest:
        // one side restores to the other's temporary array and it sticks in the station (and in the save), growing with every
        // call. We stay outermost on both ends: our prefix first, our finalizer last.

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(CraftingBase), "CountItems")]
        private static void CountItemsPrefix(CraftingBase __instance, out StorageSlot[] __state) => __state = Widen(__instance);

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(CraftingBase), "CountItems")]
        private static void CountItemsFinalizer(CraftingBase __instance, StorageSlot[] __state) => Restore(__instance, __state);

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(CraftingBase), "CompareRecipeToItems")]
        private static void ComparePrefix(CraftingBase __instance, ItemStack[] items, out StorageSlot[] __state) =>
            __state = items == null ? Widen(__instance) : null;

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(CraftingBase), "CompareRecipeToItems")]
        private static void CompareFinalizer(CraftingBase __instance, StorageSlot[] __state) => Restore(__instance, __state);

        /// <summary>Puts copies of the player's stacks after the station's own; returns the original array, or null if nothing changed.</summary>
        private static StorageSlot[] Widen(CraftingBase station)
        {
            if (pulling || !Applies(station))
                return null;
            StorageSlot[] own = station.InputOutputStorage.storageSlots;
            // The same array while the player's stacks are the same (a recipe list runs dozens of checks): Craft From Storage
            // caches its own widening by the array it gets, and a new one each time makes it rescan every container per recipe.
            int player = PlayerFingerprint();
            if (widenedOwn != own || widenedPlayer != player)
            {
                List<StorageSlot> extra = PlayerSlots()
                    .Select(s => Copy(s.itemStack))
                    .ToList();
                widened = extra.Count == 0 ? null : own.Concat(extra).ToArray();
                widenedOwn = own;
                widenedPlayer = player;
            }
            if (widened == null)
                return null;
            station.InputOutputStorage.storageSlots = widened;
            return own;
        }

        private static StorageSlot[] widened;
        private static StorageSlot[] widenedOwn;
        private static int widenedPlayer;

        /// <summary>Changes whenever a player stack appears, goes or changes amount.</summary>
        private static int PlayerFingerprint()
        {
            unchecked
            {
                int hash = 17;
                foreach (SlotController slot in Inventory.instance.Slots)
                {
                    ItemStack stack = slot?.itemStack;
                    hash = hash * 31 + (stack == null ? 0 : (stack.itemId * 397) ^ stack.itemAmount ^ stack.GetHashCode());
                }
                return hash;
            }
        }

        /// <summary>After we move items ourselves: the next check builds the widening anew.</summary>
        private static void ForgetWidened() => widened = widenedOwn = null;

        private static void Restore(CraftingBase station, StorageSlot[] original)
        {
            if (original != null)
                station.InputOutputStorage.storageSlots = original;
        }

        private static StorageSlot Copy(ItemStack s) => new StorageSlot
        {
            itemReference = s.itemReference,
            itemId = s.itemId,
            itemAmount = s.itemAmount,
            ownerId = s.ownerId,
            refigerated = s.refigerated,
            spoilDisabled = s.spoilDisabled,
            _meta = s._meta,
            _metaset = s._metaset
        };

        private static IEnumerable<SlotController> PlayerSlots() =>
            (Inventory.instance?.Slots ?? new SlotController[0])
            .Where(s => s?.itemStack != null && s.itemStack.itemId >= 0 && s.itemStack.itemAmount > 0 && s.itemStack.itemReference?.Item != null);

        // ---- starting ----

        private sealed class Pull
        {
            public Dictionary<int, int> Before;
        }

        /// <remarks>First, so the seeds are in before Linked Terrariums' prefix plants across its group.</remarks>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(Growing), nameof(Growing.StartGrow))]
        private static void StartGrowPrefix(Growing __instance, ref int amount, out Pull __state)
        {
            __state = PullIn(__instance, __instance.selectedRecipe);
            if (__state == null)
                return;
            int max = LinkedTerrariums.IsLinked(__instance)
                ? LinkedTerrariums.Max(__instance.selectedRecipe)
                : System.Math.Min(__instance.GetRecipeMultiplier(__instance.selectedRecipe), __instance.FreeGrowingSlotCount());
            amount = System.Math.Max(1, System.Math.Min(amount, max));
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Growing), nameof(Growing.StartGrow))]
        private static void StartGrowFinalizer(Growing __instance, Pull __state) => PutBack(__instance, __state);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(manufacturingProcess), nameof(manufacturingProcess.StartProcess))]
        private static void StartProcessPrefix(manufacturingProcess __instance, out Pull __state)
        {
            Recipe recipe = __instance.ActiveRecipe.Key;
            __state = PullIn(__instance, recipe);
            if (__state != null)
            {
                int max = __instance.GetRecipeMultiplierShowValue(recipe);
                __instance.ActiveRecipe = new KeyValuePair<Recipe, int>(recipe, System.Math.Max(1, System.Math.Min(__instance.ActiveRecipe.Value, max)));
            }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(manufacturingProcess), nameof(manufacturingProcess.StartProcess))]
        private static void StartProcessFinalizer(manufacturingProcess __instance, Pull __state) => PutBack(__instance, __state);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(manufacturing), nameof(manufacturing.Craft))]
        private static void CraftPrefix(manufacturing __instance, out Pull __state) => __state = PullIn(__instance, __instance.selectedRecipe);

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(manufacturing), nameof(manufacturing.Craft))]
        private static void CraftFinalizer(manufacturing __instance, Pull __state) => PutBack(__instance, __state);

        /// <summary>Moves the player's stacks that fit the recipe into the station. Null when nothing moved.</summary>
        private static Pull PullIn(CraftingBase station, Recipe recipe)
        {
            // Nested start (Linked Terrariums plants each member through its own StartGrow): the outer call already pulled.
            if (pulling || recipe == null || !Applies(station))
                return null;
            SlotController[] stationSlots = station.InputOutputStorage.Slots;
            if (stationSlots == null || stationSlots.Length == 0)
                return null;

            ForgetWidened();
            var pull = new Pull { Before = Count(station) };
            int moved = 0;
            foreach (SlotController slot in PlayerSlots().Where(s => Fits(recipe, s.itemStack)).ToList())
            {
                slot.MoveToOtherInventory(stationSlots[0], slot.itemStack.itemAmount, out int n);
                moved += n;
            }
            if (moved == 0)
                return null;
            pulling = true; // the game's own checks inside the start see the station as it really is now
            return pull;
        }

        private static void PutBack(CraftingBase station, Pull pull)
        {
            if (pull == null)
                return;
            pulling = false;
            ForgetWidened();
            try
            {
                Dictionary<int, int> now = Count(station);
                SlotController[] slots = station.InputOutputStorage.Slots;
                foreach (KeyValuePair<int, int> kv in now)
                {
                    pull.Before.TryGetValue(kv.Key, out int before);
                    int excess = kv.Value - before;
                    for (int i = slots.Length - 1; i >= 0 && excess > 0; i--)
                    {
                        ItemStack stack = slots[i].itemStack;
                        if (stack == null || stack.itemId != kv.Key || stack.itemAmount <= 0)
                            continue;
                        slots[i].MoveToInventory(out int n, System.Math.Min(excess, stack.itemAmount));
                        if (n == 0)
                            break; // the player's inventory is full: the rest stays in the station
                        excess -= n;
                    }
                }
                station.CheckItems(force: true);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Returning ingredients failed: {e}");
            }
        }

        private static Dictionary<int, int> Count(CraftingBase station)
        {
            var counts = new Dictionary<int, int>();
            foreach (SlotController s in station.InputOutputStorage.Slots)
            {
                ItemStack stack = s?.itemStack;
                if (stack == null || stack.itemId < 0 || stack.itemAmount <= 0)
                    continue;
                counts.TryGetValue(stack.itemId, out int c);
                counts[stack.itemId] = c + stack.itemAmount;
            }
            return counts;
        }

        /// <summary>The same matching as <c>CraftingBase.RemoveItems</c>: the item itself, a member of an item group, or a liquid in a container.</summary>
        private static bool Fits(Recipe recipe, ItemStack stack)
        {
            if (recipe.Input == null)
                return false;
            int liquidId = stack.GetMetaOfType<ItemLiquidData>()?.liquidItem?.ID ?? -1;
            foreach (Item required in recipe.Input.Keys)
            {
                if (required == null)
                    continue;
                ItemGroupData group = required.GetMetaOfType<ItemGroupData>();
                if (group != null)
                {
                    if (group.itemGroup.Any(g => g?.item != null && (g.item.ID == stack.itemId || g.item.ID == liquidId)))
                        return true;
                }
                else if (required.ID == stack.itemId || required.ID == liquidId)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
