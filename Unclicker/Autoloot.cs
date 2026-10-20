using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OS.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unclicker
{
    /// <summary>
    /// MarkKey (default: middle click) on an item in any open inventory panel adds its kind to the autoloot list (again: removes it); listed items
    /// carry a small star. Opening a
    /// container outside the tenement then moves those items into the player's inventory, except stacks taking which is theft.
    /// The list lives in the config, not in the save.
    /// </summary>
    [HarmonyPatch]
    internal static class Autoloot
    {
        private const string TenementScene = "Interior Player Tenement";

        private static HashSet<int> pool;

        private static HashSet<int> Pool
        {
            get
            {
                if (pool == null)
                {
                    pool = new HashSet<int>();
                    foreach (string part in Plugin.AutolootItems.Value.Split(','))
                        if (int.TryParse(part.Trim(), out int id))
                            pool.Add(id);
                }
                return pool;
            }
        }

        private static void SavePool() =>
            Plugin.AutolootItems.Value = string.Join(",", Pool.OrderBy(id => id));

        // ---- marking ----

        /// <summary>Called every frame from the plugin.</summary>
        internal static void Update()
        {
            if (!Plugin.AutolootEnabled.Value || !Input.GetKeyDown(Plugin.AutolootKey.Value) || GameUIController.instance == null ||
                !GameUIController.instance.IsMenuVisible || EventSystem.current == null)
                return;
            ItemStack stack = SlotUnderMouse()?.slotcontrol?.itemStack;
            Item item = stack?.itemReference?.Item;
            if (item == null || stack.itemId < 0 || stack.itemAmount <= 0)
                return;
            bool added = Pool.Add(item.ID);
            if (!added)
                Pool.Remove(item.ID);
            SavePool();
            item.PlaySound();
            foreach (ItemData shown in Object.FindObjectsByType<ItemData>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Mark(shown);
        }

        // ---- marker on items in the list ----

        private const string MarkerName = "UnclickerAutoloot";
        private static readonly Color MarkerColor = new Color(1f, 0.82f, 0.25f);
        private static Sprite star;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemData), nameof(ItemData.UpdateItem))]
        private static void UpdateItemPostfix(ItemData __instance) => Mark(__instance);

        /// <summary>A small star in the item's top right corner (5/24 of its size, 1/32 in from the edges), with the game's upgrade star sprite.</summary>
        private static void Mark(ItemData data)
        {
            if (data == null)
                return;
            bool on = Plugin.AutolootEnabled.Value && data.item != null && Pool.Contains(data.item.ID);
            Transform marker = data.transform.Find(MarkerName);
            if (marker == null)
            {
                if (!on)
                    return;
                marker = CreateMarker(data.transform);
            }
            marker.gameObject.SetActive(on);
            marker.SetAsLastSibling();
        }

        private static Transform CreateMarker(Transform parent)
        {
            var go = new GameObject(MarkerName, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            const float pad = 1f / 32f, size = 5f / 24f;
            rt.anchorMax = new Vector2(1f - pad, 1f - pad);
            rt.anchorMin = rt.anchorMax - new Vector2(size, size);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.sprite = Star();
            image.preserveAspect = true;
            image.color = MarkerColor;
            image.raycastTarget = false;
            return rt;
        }

        private static Sprite Star()
        {
            if (star == null)
                star = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == "button_round_001_upgrade_star");
            return star;
        }

        private static Slot SlotUnderMouse()
        {
            var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            return hits.Select(h => h.gameObject.GetComponent<Slot>()).FirstOrDefault(s => s != null);
        }

        // ---- looting ----

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Storage), nameof(Storage.EnterExternally))]
        private static void EnterPostfix(Storage __instance)
        {
            if (!Plugin.AutolootEnabled.Value || Pool.Count == 0 || SceneManager.GetActiveScene().name == TenementScene ||
                __instance.GetComponent<CraftingBase>() != null || __instance.Slots == null || __instance.Slots.Length == 0 ||
                IsBackpack(__instance) || __instance.GetComponent<TenementResourceStorage>() != null)
                return;
            // A frame later, when the panel shows the container's slots: moving items then updates them on screen.
            Plugin.Instance.StartCoroutine(LootNextFrame(__instance));
        }

        private static IEnumerator LootNextFrame(Storage storage)
        {
            yield return null;
            if (storage == null || Storage.active != storage || TradePanel.instance?.activeTrade != null)
                yield break;
            Loot(storage);
        }

        private static void Loot(Storage storage)
        {
            bool looted = false;
            foreach (SlotController slot in storage.Slots.Reverse())
            {
                ItemStack stack = slot?.itemStack;
                if (stack?.itemReference?.Item == null || stack.itemId < 0 || stack.itemAmount <= 0 ||
                    !Pool.Contains(stack.itemId) || IsTheft(stack))
                    continue;
                slot.MoveToInventory(out int moved, stack.itemAmount);
                looted |= moved > 0;
            }
            if (looted)
                storage.storageChanged = true;
        }

        /// <summary>The worn backpack, rucksack or cooler bag: the player's own storage, opened like a container.</summary>
        private static bool IsBackpack(Storage storage)
        {
            BackpackStorage backpacks = BackpackStorage.instance;
            return backpacks != null && (storage == backpacks.backpackStorage || storage == backpacks.rucksackStorage ||
                                         storage == backpacks.coolerBagStorage);
        }

        /// <summary>The game's own rule (<c>Inventory.CheckForAddedStolenItems</c>): an owned stack without the owner's permission.</summary>
        private static bool IsTheft(ItemStack stack) =>
            stack.ownerId >= 2 && stack.GetMetaOfType<ItemOwnerPermission>()?.ownerPermission != true;

        internal static void Unload()
        {
            pool = null;
            foreach (ItemData data in Object.FindObjectsByType<ItemData>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform marker = data.transform.Find(MarkerName);
                if (marker != null)
                    Object.Destroy(marker.gameObject);
            }
        }
    }
}
