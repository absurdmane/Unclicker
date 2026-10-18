using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.Mono.Bootstrap;
using OS.Items;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Unclicker
{
    /// <summary>
    /// Hold the paint key (default LeftAlt) and move the mouse: every inventory slot the cursor passes over is quick-moved, whole stack,
    /// as the vanilla double-click would. Only plain moves between the player and an open container or station; not in trade, bottle
    /// recycling or liquid storage, never character slots. With nothing open it does nothing, unless Backpack In Inventory is installed
    /// (then quick move goes main ↔ backpack; equipables, which vanilla would equip, are skipped).
    /// Each slot is moved once per key hold, and the first moved slot fixes the direction, so items never travel back.
    /// </summary>
    internal static class QuickMovePaint
    {
        private const string BackpackInInventoryGuid = "obenseuer.absurdmane.backpackininventory";
        private const float SampleStep = 8f;
        private const int MaxSamples = 64;

        private static readonly HashSet<SlotController> visited = new HashSet<SlotController>();
        private static readonly List<RaycastResult> hits = new List<RaycastResult>();
        private static bool pressed;
        private static bool? side;
        private static Vector2? lastPosition;

        /// <summary>Called every frame from the plugin.</summary>
        internal static void Update()
        {
            // A stroke starts only on a key press seen while focused: after Alt+Tab the key may still read as held.
            if (Application.isFocused && Input.GetKeyDown(Plugin.PaintKey.Value))
                pressed = true;
            if (!pressed || !Plugin.PaintEnabled.Value || !Input.GetKey(Plugin.PaintKey.Value) || !Application.isFocused || !Allowed())
            {
                pressed &= Application.isFocused && Input.GetKey(Plugin.PaintKey.Value);
                EndStroke();
                return;
            }
            // A mouse button down belongs to vanilla drag, double-click and hold-move; resume after it, without sweeping the path in between.
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) || ItemData.globaldrag != null)
            {
                lastPosition = null;
                return;
            }

            Vector2 position = Input.mousePosition;
            Vector2 from = lastPosition ?? position;
            lastPosition = position;
            int samples = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, position) / SampleStep), 1, MaxSamples);
            Item moved = null;
            for (int i = 1; i <= samples; i++)
            {
                SlotController slot = SlotAt(Vector2.Lerp(from, position, (float)i / samples));
                if (slot != null && visited.Add(slot))
                    moved = Move(slot) ?? moved;
            }
            moved?.PlaySound();
        }

        private static bool Allowed()
        {
            if (GameUIController.instance == null || !GameUIController.instance.IsMenuVisible || EventSystem.current == null ||
                Inventory.instance == null || InputManager.instance == null || InputManager.instance.inputType != InputManager.InputType.Keyboard)
                return false;
            if (GameController.instance != null && GameController.instance.writeLock)
                return false;
            if (TradePanel.instance?.activeTrade != null || (BottleRecyclingPanel.instance != null && BottleRecyclingPanel.instance.open) ||
                LiquidStorage.currentStorage != null)
                return false;
            return Inventory.instance.ForeignSlots != null || BackpackInInventory;
        }

        private static bool BackpackInInventory => UnityChainloader.Instance.Plugins.ContainsKey(BackpackInInventoryGuid);

        private static SlotController SlotAt(Vector2 position)
        {
            var data = new PointerEventData(EventSystem.current) { position = position };
            hits.Clear();
            EventSystem.current.RaycastAll(data, hits);
            foreach (RaycastResult hit in hits)
            {
                Slot slot = hit.gameObject.GetComponentInParent<Slot>();
                if (slot != null)
                    return slot.slotcontrol;
            }
            return null;
        }

        /// <summary>Quick-moves the slot's stack; returns the moved item (for the sound), or null.</summary>
        private static Item Move(SlotController slot)
        {
            ItemStack stack = slot.itemStack;
            if (stack == null || stack.itemId < 0 || stack.itemAmount <= 0 || slot.slot == null ||
                Array.IndexOf(Inventory.instance.CharacterSlots, slot) >= 0)
                return null;
            ItemData data = slot.slot.GetItemItemData();
            Item item = data?.item;
            if (item == null)
                return null;
            bool plainInventory = Inventory.instance.ForeignSlots == null;
            if (plainInventory && item.Actions != null && item.Actions.Any(a => a is ItemEquipable))
                return null;

            // Player side vs container; with nothing open (Backpack In Inventory): main slots vs backpack slots.
            bool slotSide = plainInventory ? Array.IndexOf(Inventory.instance.Slots, slot) >= 0 : slot.slot.playerSlot;
            if (side != null && side != slotSide)
                return null;
            if (!data.QuickMove(data.amount))
                return null;
            side = slotSide;
            return item;
        }

        private static void EndStroke()
        {
            visited.Clear();
            side = null;
            lastPosition = null;
        }

        internal static void Unload()
        {
            pressed = false;
            EndStroke();
        }
    }
}
