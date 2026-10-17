using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unclicker
{
    /// <summary>
    /// A "Take all" button on the station panels (workbench, stove, brewing barrel, bed, ...): moves everything from the station's
    /// inventory to the player's, like the button of a plain storage. It is a copy of the storage panel's own button,
    /// placed just above the station's slot grid. Generators are left out: taking all would take their fuel.
    /// The growing panel also gets a "Harvest all" button, a copy of the same button, to the left of Take all.
    /// </summary>
    [HarmonyPatch]
    internal static class TakeAllButton
    {
        private const string TakeAllName = "Unclicker Take all";
        private const string HarvestAllName = "Unclicker Harvest all";

        /// <summary>
        /// For stations that started before the mod loaded (a reload of the mod mid-game): their Start has passed, so add the
        /// buttons now. On a normal start there are no stations yet and this does nothing.
        /// </summary>
        public static void AddToExisting()
        {
            foreach (CraftingBase station in Object.FindObjectsByType<CraftingBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                StartPostfix(station);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CraftingBase), "Start")]
        private static void StartPostfix(CraftingBase __instance)
        {
            if (__instance is Generator)
                return;
            CraftingPanelBaseUI panel = __instance.craftingPanelBaseUI;
            if (panel == null || panel.inventorySlotPanel == null)
                return;
            try
            {
                RectTransform takeAll = Find(panel, TakeAllName);
                if (Plugin.TakeAll.Value && takeAll == null)
                    takeAll = Create(panel, TakeAllName, null, TakeAll, CanTakeAll, null);
                if (Plugin.HarvestAllButton.Value && __instance is Growing && Find(panel, HarvestAllName) == null)
                    Create(panel, HarvestAllName, "Harvest all", Farming.HarvestAllOpen, Farming.CanHarvestAllOpen, takeAll);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Panel buttons: {e}");
            }
        }

        private static RectTransform Find(CraftingPanelBaseUI panel, string name)
        {
            foreach (Placer placer in panel.GetComponentsInChildren<Placer>(true))
                if (placer.name == name)
                    return (RectTransform)placer.transform;
            return null;
        }

        /// <param name="label">New button text; null keeps the copied "Take all".</param>
        /// <param name="canUse">The button is greyed out while this is false.</param>
        /// <param name="leftOf">Placed to the left of this button; null = at the slot grid's top-right corner.</param>
        private static RectTransform Create(CraftingPanelBaseUI panel, string name, string label, UnityEngine.Events.UnityAction action,
            System.Func<bool> canUse, RectTransform leftOf)
        {
            GameObject source = Inventory.instance?.storagePanelUI?.takeAllButton;
            if (source == null)
                return null;
            Transform slots = panel.inventorySlotPanel.transform;
            // Under the panel root, not next to the slot grid: on some panels (the growing one) the grid sits in a masked
            // scroll area, which would clip a button placed above it.
            GameObject copy = Object.Instantiate(source, panel.transform, worldPositionStays: false);
            copy.transform.SetAsLastSibling();
            copy.name = name;
            copy.SetActive(true);
            // The game has its own global Button type, hence the full name.
            UnityEngine.UI.Button button = copy.GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (button == null)
            {
                Object.Destroy(copy);
                return null;
            }
            // The copy still calls the storage panel; point it at the station instead.
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
            if (label != null)
                foreach (TMP_Text text in copy.GetComponentsInChildren<TMP_Text>(true))
                    text.text = label;
            LayoutElement layout = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            copy.AddComponent<Placer>().Init((RectTransform)slots, leftOf, button, canUse);
            Plugin.Log.LogInfo($"{name} button added to {panel.name}");
            return (RectTransform)copy.transform;
        }

        /// <summary>Removes our buttons from every panel.</summary>
        public static void Unload()
        {
            foreach (Placer placer in Object.FindObjectsByType<Placer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(placer.gameObject);
        }

        private static bool CanTakeAll()
        {
            CraftingBase station = CraftingBase.currentCraftingBase;
            return station != null && (HasItems(station.InputOutputStorage) || HasItems(station.OutputStorage));
        }

        private static bool HasItems(Storage storage) =>
            storage?.storageSlots != null && storage.storageSlots.Any(s => s != null && s.itemId >= 0 && s.itemAmount > 0);

        private static void TakeAll()
        {
            CraftingBase station = CraftingBase.currentCraftingBase;
            if (station == null || station is Generator)
                return;
            station.InputOutputStorage?.TakeAll();
            if (station.OutputStorage != null && station.OutputStorage != station.InputOutputStorage)
                station.OutputStorage.TakeAll();
            if (Inventory.instance?.storagePanelUI != null)
                MiscAudioPlayer.PlaySound(Inventory.instance.storagePanelUI.takeAllSound);
        }

        /// <summary>
        /// Keeps the button just above the top-right corner of the station's inventory, whatever the panel's layout: of the scroll
        /// area around the slot grid when there is one, so a long grid doesn't push it out of sight. Greys it out while there's
        /// nothing to do.
        /// </summary>
        private sealed class Placer : MonoBehaviour
        {
            private RectTransform slots;
            private RectTransform leftOf;
            private RectTransform self;
            private readonly Vector3[] corners = new Vector3[4];

            private CanvasGroup group;
            private UnityEngine.UI.Button button;
            private System.Func<bool> canUse;
            private RectTransform frame;
            private float nextCheck;

            public void Init(RectTransform slotPanel, RectTransform left, UnityEngine.UI.Button target, System.Func<bool> usable)
            {
                slots = slotPanel;
                leftOf = left;
                button = target;
                canUse = usable;
                ScrollRect scroll = slotPanel.GetComponentInParent<ScrollRect>(true);
                frame = scroll != null ? (RectTransform)scroll.transform : slotPanel;
                self = (RectTransform)transform;
                self.anchorMin = self.anchorMax = new Vector2(0.5f, 0.5f);
                self.pivot = new Vector2(1f, 0f);
                group = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            }

            private void LateUpdate()
            {
                if (slots == null)
                    return;
                // Not a child of the grid any more: hide along with it (SetActive would stop this LateUpdate).
                bool shown = slots.gameObject.activeInHierarchy;
                group.alpha = shown ? 1f : 0f;
                group.interactable = group.blocksRaycasts = shown;
                if (shown && Time.unscaledTime >= nextCheck)
                {
                    nextCheck = Time.unscaledTime + 0.25f;
                    bool usable;
                    try
                    {
                        usable = canUse == null || canUse();
                    }
                    catch (System.Exception e)
                    {
                        usable = true;
                        canUse = null;
                        Plugin.Log.LogError($"{name}: {e}");
                    }
                    button.interactable = usable;
                }
                if (leftOf != null && leftOf.gameObject.activeInHierarchy)
                {
                    // Same pivot (bottom-right): step left by the other button's width and a small gap.
                    self.position = leftOf.position - new Vector3((leftOf.rect.width + 6f) * leftOf.lossyScale.x, 0f, 0f);
                    return;
                }
                frame.GetWorldCorners(corners); // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
                self.position = corners[2] + new Vector3(0f, 4f * self.lossyScale.y, 0f);
            }
        }
    }
}
