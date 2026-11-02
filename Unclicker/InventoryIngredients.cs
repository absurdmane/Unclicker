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
    /// - Processes and workstations (<c>StartProcess</c>, <c>Craft</c>): for the start call the station's slots are widened with copies of
    ///   the player's stacks, the game consumes from them, and the changes are written back to the player's stacks. No slot limit.
    /// - Growing devices: not touched. Since 0.5.13 the game itself adds the player's slots to their input while the panel is open
    ///   (<c>Growing.ExtraInputSlots</c>).
    /// </summary>
    [HarmonyPatch]
    internal static class InventoryIngredients
    {
        /// <remarks>
        /// Only while the station holds its own storage: another mod may briefly swap in a neighbour's storage, and the player's stacks
        /// must not be counted once per neighbour.
        /// </remarks>
        private static bool Applies(CraftingBase station) =>
            Plugin.IngredientsFromInventory.Value && station != null && station == CraftingBase.currentCraftingBase && !(station is Growing) &&
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
            if (session != null || !Applies(station))
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

        // Processes and workstations consume straight from the player's stacks: a barrel's few slots can't hold 100 L of water in
        // bottles, so moving the ingredients in first cut the batch to what fitted. For the start call only, the station's slots are
        // widened with copies of the player's stacks; the game removes from them in place (amount, emptied stack, bottle -> empty
        // bottle, liquid left), and the changes are written back to the player's real stacks after. Same nesting rule as listing:
        // our prefix first, our finalizer last, and the station always gets its own array back.

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(manufacturingProcess), nameof(manufacturingProcess.StartProcess))]
        private static void StartProcessPrefix(manufacturingProcess __instance, out Session __state)
        {
            Recipe recipe = __instance.ActiveRecipe.Key;
            __state = recipe == null ? null : Begin(__instance);
            if (__state != null)
            {
                int max = __instance.GetRecipeMultiplierShowValue(recipe);
                __instance.ActiveRecipe = new KeyValuePair<Recipe, int>(recipe, System.Math.Max(1, System.Math.Min(__instance.ActiveRecipe.Value, max)));
            }
        }

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(manufacturingProcess), nameof(manufacturingProcess.StartProcess))]
        private static void StartProcessFinalizer(Session __state) => End(__state);

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(manufacturing), nameof(manufacturing.Craft))]
        private static void CraftPrefix(manufacturing __instance, out Session __state) =>
            __state = __instance.selectedRecipe == null ? null : Begin(__instance);

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(manufacturing), nameof(manufacturing.Craft))]
        private static void CraftFinalizer(Session __state) => End(__state);

        private sealed class Session
        {
            public CraftingBase Station;
            public StorageSlot[] Own;
            public StorageSlot[] Widened;
            public List<Link> Links;
        }

        private sealed class Link
        {
            public ItemStack Real;
            public StorageSlot Copy;
            public int Id;
            public int Amount;
            public object[] Meta;
        }

        private static Session session;

        private static Session Begin(CraftingBase station)
        {
            if (session != null || !Applies(station))
                return null;
            List<Link> links = PlayerSlots().Select(s => new Link
            {
                Real = s.itemStack,
                Copy = Copy(s.itemStack),
                Id = s.itemStack.itemId,
                Amount = s.itemStack.itemAmount,
                Meta = s.itemStack._meta
            }).ToList();
            if (links.Count == 0)
                return null;
            ForgetWidened();
            Storage storage = station.InputOutputStorage;
            session = new Session { Station = station, Own = storage.storageSlots, Links = links };
            session.Widened = session.Own.Concat(links.Select(l => l.Copy)).ToArray();
            storage.storageSlots = session.Widened;
            return session;
        }

        private static void End(Session s)
        {
            if (s == null)
                return;
            session = null;
            Storage storage = s.Station.InputOutputStorage;
            try
            {
                foreach (Link link in s.Links)
                    WriteBack(link);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Writing used ingredients back to the inventory failed: {e}");
            }
            finally
            {
                if (storage.storageSlots != s.Widened)
                    Plugin.Log.LogWarning("Another mod left a different slot array on the station after the start; the station's own slots are restored.");
                storage.storageSlots = s.Own;
                ForgetWidened();
            }
            try
            {
                Inventory.instance.UpdateAllSlotsNow();
                s.Station.CheckItems(force: true);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Refreshing after the start failed: {e}");
            }
        }

        /// <summary>Copies what the game did to the copy onto the player's stack. Liquid meta is shared, so its changes are there already.</summary>
        private static void WriteBack(Link link)
        {
            StorageSlot copy = link.Copy;
            ItemStack real = link.Real;
            if (copy.itemId == link.Id && copy.itemAmount == link.Amount && ReferenceEquals(copy._meta, link.Meta))
                return;
            if (copy.IsEmpty())
            {
                real.RemoveStack(); // fires the slot's OnTaken, as a removal from the inventory would
                return;
            }
            if (real.itemId != copy.itemId)
            {
                real.itemId = copy.itemId;
                real.itemReference = new ItemReference(ItemDatabase.instance.FetchItemByID(copy.itemId)); // the game changes only the id (bottle -> empty bottle)
            }
            real.itemAmount = copy.itemAmount;
            real._meta = copy._meta;
            real._metaset = copy._metaset;
            real.ownerId = copy.ownerId;
        }
    }
}
