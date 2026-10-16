using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Unclicker
{
    /// <summary>
    /// A "Take all" button on the station panels (workbench, stove, brewing barrel, bed, ...): moves everything from the station's
    /// inventory to the player's, like the button of a plain storage. It is a copy of the storage panel's own button,
    /// placed just above the station's slot grid. Generators are left out: taking all would take their fuel.
    /// </summary>
    [HarmonyPatch]
    internal static class TakeAllButton
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CraftingBase), "Start")]
        private static void StartPostfix(CraftingBase __instance)
        {
            if (!Plugin.TakeAll.Value || __instance is Generator)
                return;
            CraftingPanelBaseUI panel = __instance.craftingPanelBaseUI;
            if (panel == null || panel.inventorySlotPanel == null || panel.GetComponentInChildren<Placer>(true) != null)
                return;
            try
            {
                Create(panel);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Take all button: {e}");
            }
        }

        private static void Create(CraftingPanelBaseUI panel)
        {
            GameObject source = Inventory.instance?.storagePanelUI?.takeAllButton;
            if (source == null)
                return;
            Transform slots = panel.inventorySlotPanel.transform;
            GameObject copy = Object.Instantiate(source, slots.parent, worldPositionStays: false);
            copy.name = "Unclicker Take all";
            copy.SetActive(true);
            // The game has its own global Button type, hence the full name.
            UnityEngine.UI.Button button = copy.GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (button == null)
            {
                Object.Destroy(copy);
                return;
            }
            // The copy still calls the storage panel; point it at the station instead.
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.onClick.AddListener(TakeAll);
            LayoutElement layout = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            copy.AddComponent<Placer>().Init((RectTransform)slots);
            Plugin.Log.LogInfo($"Take all button added to {panel.name}");
        }

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

        /// <summary>Keeps the button at the top-right corner of the slot grid, just above it, whatever the panel's layout.</summary>
        private sealed class Placer : MonoBehaviour
        {
            private RectTransform slots;
            private RectTransform self;
            private readonly Vector3[] corners = new Vector3[4];

            public void Init(RectTransform slotPanel)
            {
                slots = slotPanel;
                self = (RectTransform)transform;
                self.anchorMin = self.anchorMax = new Vector2(0.5f, 0.5f);
                self.pivot = new Vector2(1f, 0f);
            }

            private void LateUpdate()
            {
                if (slots == null)
                    return;
                slots.GetWorldCorners(corners); // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
                self.position = corners[2] + new Vector3(0f, 4f * self.lossyScale.y, 0f);
            }
        }
    }
}
