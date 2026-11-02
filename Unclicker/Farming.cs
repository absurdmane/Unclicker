using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OS.Items;
using UnityEngine;

namespace Unclicker
{
    internal enum HarvestOverflow
    {
        StopWhenFull,
        DropRest
    }

    internal enum PlantingOrder
    {
        WorstFirst,
        Vanilla
    }

    /// <summary>
    /// Farming: harvest all (Shift + interact on a growing device, or the panel's Harvest all button), harvest from the panel's plant
    /// cells into the player's inventory, worst items planted first.
    /// </summary>
    [HarmonyPatch]
    internal static class Farming
    {
        private enum Ripeness
        {
            None,
            Fruit, // ready fruit hanging on a multi-harvest plant
            Ripe, // a fully grown single-produce plant
            Dead,
            Collectible
        }

        // ---- harvest all ----

        /// <summary>Anything harvest-all would take from this device.</summary>
        public static bool HasHarvest(Growing growing) =>
            growing != null && growing.GrowingSlots != null && growing.GrowingSlots.Any(s => RipenessOf(s, out _) != Ripeness.None);

        /// <summary>
        /// Harvests every ripe plant and clears dead ones, plant by plant. A plant whose harvest doesn't fit stops it (or, with
        /// <see cref="HarvestOverflow.DropRest"/>, is harvested and the rest dropped). <paramref name="fromPanel"/>: what doesn't fit the
        /// player goes into the device's storage, as the panel's own harvest does.
        /// </summary>
        public static void HarvestAll(IReadOnlyList<Growing> devices, bool fromPanel)
        {
            int harvested = 0;
            int left = 0;
            foreach (Growing device in devices)
            {
                if (device == null || device.GrowingSlots == null)
                    continue;
                foreach (GrowingSpot spot in device.GrowingSlots.ToList())
                {
                    Ripeness ripeness = RipenessOf(spot, out grow plant);
                    if (ripeness == Ripeness.None)
                        continue;
                    // Every plant gets its try: a smaller harvest may still fit after a bigger one didn't.
                    if (HarvestPlant(device, spot, plant, ripeness, fromPanel))
                        harvested++;
                    else
                        left++;
                }
                device.StartCoroutine(device.UpdateItemsInProgressDelayed());
            }

            if (left > 0)
            {
                UseText.instance?.WarningUseEffect();
                Notifications.instance?.CreateNotification("Inventory full", $"{left} plant{(left == 1 ? "" : "s")} left to harvest");
            }
            Plugin.Log.LogDebug($"Harvest all: {harvested} plants, {left} left");
        }

        private static Ripeness RipenessOf(GrowingSpot spot, out grow plant)
        {
            plant = null;
            if (spot == null || spot.Plant == null)
                return Ripeness.None;
            plant = spot.Plant.GetComponentInChildren<grow>();
            if (plant == null)
                return spot.Plant.GetComponentInChildren<CollectibleItem>() != null ? Ripeness.Collectible : Ripeness.None;
            if (plant.HasVisibleItems())
                return Ripeness.Fruit;
            if (plant.growSpoil != null && plant.growSpoil.spoiled)
                return Plugin.HarvestDeadPlants.Value ? Ripeness.Dead : Ripeness.None;
            if ((plant.visibleGrownItems == null || plant.visibleGrownItems.Length == 0) && plant.Age >= plant.FullGrownAge)
                return Ripeness.Ripe;
            return Ripeness.None;
        }

        /// <summary>False when it doesn't fit (nothing taken from this plant, or only the fruit that fitted).</summary>
        private static bool HarvestPlant(Growing device, GrowingSpot spot, grow plant, Ripeness ripeness, bool fromPanel)
        {
            switch (ripeness)
            {
                case Ripeness.Fruit:
                    return PickFruit(plant);

                case Ripeness.Ripe:
                case Ripeness.Dead:
                    if (Plugin.Overflow.Value == HarvestOverflow.StopWhenFull && !FitsAll(plant, ripeness == Ripeness.Dead))
                        return false; // stays on the plant
                    List<ItemStack> stacks = plant.HarvestExternal(out bool destroy);
                    foreach (ItemStack stack in stacks)
                        Deliver(stack, device, fromPanel);
                    if (destroy)
                        spot.HarvestPlant();
                    return true;

                case Ripeness.Collectible:
                    CollectibleItem collectible = spot.Plant.GetComponentInChildren<CollectibleItem>();
                    Item item = collectible.Item.Item;
                    if (Plugin.Overflow.Value == HarvestOverflow.StopWhenFull && FreeSlots() < SlotsFor(item, collectible.amount))
                        return false; // stays on the plant
                    if (fromPanel)
                        device.CreateItems(Enumerable.Repeat(item, collectible.amount).ToList());
                    else
                    {
                        int rest = AddToPlayer(item, collectible.amount, -1, null);
                        if (rest > 0)
                            ItemOperations.DropItem(item, rest);
                    }
                    spot.HarvestPlant();
                    return true;
            }
            return true;
        }

        /// <summary>
        /// The game's pick-all on the plant root, but only fruit that fits: the game adds fruit straight to the player. False when some
        /// fruit had to stay on the plant.
        /// </summary>
        private static bool PickFruit(grow plant)
        {
            int picked = 0;
            bool skipped = false;
            foreach (GrowItem fruit in plant.visibleGrownItems)
            {
                if (fruit == null || !fruit.gameObject.activeInHierarchy || !fruit.readyToPick)
                    continue;
                // The game adds the fruit itself and drops what doesn't fit.
                if (FreeSlots() < SlotsFor(fruit.item.item.Item, fruit.itemAmount))
                {
                    skipped = true;
                    continue;
                }
                fruit.GetItem();
                fruit.PlayHarvestAnimation();
                fruit.ToggleVisibility(vis: false);
                fruit.ResetGrow();
                picked++;
            }
            if (picked > 0)
            {
                if (plant.HarvestSound != null)
                    plant.HarvestSound.PlayMisc(plant.gameObject);
                else
                    RandomAudioLibrary.instance?.harvest.PlayRandomClip(plant.gameObject);
            }
            return !skipped;
        }

        /// <summary>
        /// The plant's whole harvest fits: the most it can give, per item, first topping up the player's stacks of that item, the rest in
        /// free slots. Grown items scale with Age / FullGrownAge (at most 1 in practice).
        /// </summary>
        private static bool FitsAll(grow plant, bool dead)
        {
            float age = Mathf.Max(1f, plant.Age / (float)Mathf.Max(1, plant.FullGrownAge));
            IEnumerable<(grow.ItemAmount Amount, float Scale)> amounts = dead
                ? plant.uprootItems.Select(a => (a, 1f))
                : plant.grownItems.Select(a => (a, age)).Append((plant.seedItem, 1f));
            int needed = amounts
                .Where(x => x.Amount?.item?.Item != null && x.Amount.amount.y > 0)
                .GroupBy(x => x.Amount.item.Item.ID)
                .Sum(g => SlotsFor(g.First().Amount.item.Item, g.Sum(x => Mathf.CeilToInt(x.Amount.amount.y * x.Scale))));
            return needed <= FreeSlots();
        }

        /// <summary>
        /// Free slots an amount needs after topping up the player's stacks of the same item (main inventory and backpack). Assumes they
        /// merge; one that doesn't (another quality) goes where the harvest's overflow goes.
        /// </summary>
        private static int SlotsFor(Item item, int amount)
        {
            int stackable = Mathf.Max(1, item.Stackable);
            foreach (SlotController slot in Inventory.instance.AllSlots(ignoreBackpack: false, ignoreCharacterSlots: true))
            {
                ItemStack stack = slot?.itemStack;
                if (amount <= 0)
                    break;
                if (stack != null && stack.itemId == item.ID && stack.itemAmount > 0 && stack.itemAmount < stackable)
                    amount -= stackable - stack.itemAmount;
            }
            return amount <= 0 ? 0 : Mathf.CeilToInt(amount / (float)stackable);
        }

        /// <summary>Empty slots of the main inventory and the worn backpack (the harvest overflows into it), not the equipment slots.</summary>
        private static int FreeSlots() => Inventory.instance.GetEmptySlotsAmount(ignoreBackpack: false, ignoreCharacterSlots: true);

        /// <summary>
        /// Into the player's main inventory. What still doesn't fit goes into the device's storage (panel) or onto the floor.
        /// </summary>
        private static void Deliver(ItemStack stack, Growing device, bool fromPanel)
        {
            Item item = stack?.itemReference?.Item;
            if (item == null || stack.itemAmount <= 0)
                return;
            int rest = AddToPlayer(item, stack.itemAmount, stack.ownerId, stack.Meta);
            if (rest <= 0)
                return;
            if (fromPanel)
                device.CreateItems(new List<ItemStack> { ItemStack.CreateStackFromItem(item, rest, stack.ownerId, stack.Meta) });
            else
                ItemOperations.DropItem(item, rest, stack.ownerId, stack.Meta);
        }

        /// <summary>Into the player's main inventory; returns what didn't fit.</summary>
        private static int AddToPlayer(Item item, int amount, int owner, object[] meta) =>
            ItemOperations.AddItemAndReturnRemaining(item, owner, null, meta, amount);

        // ---- Shift + interact on the device ----

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftingBase), nameof(CraftingBase.Interact))]
        private static bool InteractPrefix(CraftingBase __instance)
        {
            if (!Plugin.HarvestAll.Value || !(__instance is Growing growing) || !Input.GetKey(Plugin.ModifierKey.Value))
                return true;
            if (__instance._Lock != null && !__instance._Lock.PlayerCanOpenOrUnlock())
                return true;
            // On Linked Terrariums: the whole group, as its panel would show it.
            IReadOnlyList<Growing> devices = LinkedTerrariums.WorldGroup(growing);
            if (!devices.Any(HasHarvest))
                return true; // nothing to harvest: open the device as usual
            HarvestAll(devices, fromPanel: false);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CraftingBase), nameof(CraftingBase.UpdateUseMessage))]
        private static void UseMessagePostfix(CraftingBase __instance)
        {
            // Only the device looked at: its Linked Terrariums group is built on Shift + interact, not every frame for the hint.
            if (!Plugin.HarvestAll.Value || !Plugin.HarvestAllHint.Value || !(__instance is Growing growing) || !HasHarvest(growing))
                return;
            UseText text = UseText.instance;
            if (text == null || text.overridden || text.useText == null)
                return;
            string interact = InputManager.instance != null ? InputManager.instance.GetKeyCodeDisplayText("Interact") : "E";
            // Same line as the device's own action: a second line grows the box up into the device's name.
            string key = Plugin.ModifierKey.Value.ToString().Replace("Left", "").Replace("Right", "");
            string hint = $"{key}+{interact}: harvest";
            string current = text.useText.text ?? "";
            text.useText.text = current.EndsWith(")")
                ? $"{current.Substring(0, current.Length - 1)} · {hint})"
                : string.IsNullOrEmpty(current) ? $"({hint})" : $"{current} ({hint})";
        }

        // ---- panel: plant cells and the Harvest all button ----

        /// <summary>A click on a plant cell: the harvest goes to the player when it fits, otherwise into the device as in vanilla.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Growing), nameof(Growing.Harvest), typeof(GameObject))]
        private static bool HarvestCellPrefix(Growing __instance, GameObject go)
        {
            if (!Plugin.CellHarvestToPlayer.Value || __instance.ManualHarvest || go == null)
                return true;
            grow plant = go.GetComponent<grow>();
            if (plant == null)
                return true; // collectibles: vanilla
            List<ItemStack> stacks = plant.HarvestExternal(out bool destroy);
            foreach (ItemStack stack in stacks)
                Deliver(stack, __instance, fromPanel: true);
            if (destroy)
            {
                go.GetComponentInParent<GrowingSpot>()?.HarvestPlant(dontDestroy: true);
                Object.Destroy(go);
            }
            __instance.StartCoroutine(__instance.UpdateItemsInProgressDelayed());
            return false;
        }

        /// <summary>Something for the panel's Harvest all button to harvest.</summary>
        public static bool CanHarvestAllOpen() =>
            CraftingBase.currentCraftingBase is Growing growing && LinkedTerrariums.Group(growing).Any(HasHarvest);

        /// <summary>The panel's Harvest all button: the open device, or its whole Linked Terrariums group.</summary>
        public static void HarvestAllOpen()
        {
            if (!(CraftingBase.currentCraftingBase is Growing growing))
                return;
            HarvestAll(LinkedTerrariums.Group(growing), fromPanel: true);
        }

        // ---- worst first ----

        /// <summary>
        /// <c>RemoveItems</c> takes the recipe's input in the order of <c>InputSlots</c> (the bed's slots, then the player's while the
        /// panel is open), so for the planting call that list is sorted worst first (quality, then freshness, ascending). The sorted
        /// array goes into the game's per-frame cache of <c>InputSlots</c>: same stacks, other order; the cache is dropped after.
        /// Only the panel's planting (<c>StartGrow(int)</c>); the game's quick plant picks its stack itself.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.VeryHigh)]
        [HarmonyPatch(typeof(Growing), nameof(Growing.StartGrow), typeof(int))]
        private static void StartGrowPrefix(Growing __instance, out bool __state)
        {
            __state = false;
            if (Plugin.PlantOrder.Value != PlantingOrder.WorstFirst || __instance.InputOutputStorage?.storageSlots == null)
                return;
            ItemStack[] slots = __instance.InputSlots;
            if (slots == null || slots.Length < 2)
                return;
            __instance.inputSlots = slots
                .OrderBy(s => s.GetMetaOfType<ItemQualityData>()?.quality ?? 1f)
                .ThenBy(s => s.GetMetaOfType<ItemPerishabledata>()?.integrity ?? 1f)
                .ToArray();
            __instance.inputSlotsFrame = Time.frameCount;
            __state = true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Growing), nameof(Growing.StartGrow), typeof(int))]
        private static void StartGrowFinalizer(Growing __instance, bool __state)
        {
            if (__state)
                __instance.ClearInputSlotsCache();
        }
    }
}
