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
        private static readonly Color TakeAllColor = new Color(0.20f, 0.56f, 0.25f);
        private static readonly Color HarvestColor = new Color(0.78f, 0.56f, 0.14f);
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
                    takeAll = Create(panel, TakeAllName, "Take\nAll", "take-all.png", "Take all",
                        "", TakeAllColor, TakeAll, CanTakeAll, null);
                if (Plugin.HarvestAllButton.Value && __instance is Growing && Find(panel, HarvestAllName) == null)
                    Create(panel, HarvestAllName, "Harvest", "harvest.png", "Harvest all",
                        "", HarvestColor, Farming.HarvestAllOpen, Farming.CanHarvestAllOpen, takeAll);
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

        /// <param name="label">Button text, used only if the icon can't be loaded; null keeps the copied one.</param>
        /// <param name="icon">Our embedded icon (Icons/*.png) shown instead of the text.</param>
        /// <param name="canUse">The button is greyed out while this is false.</param>
        /// <param name="leftOf">Placed to the left of this button; null = at the slot grid's top-right corner.</param>
        private static RectTransform Create(CraftingPanelBaseUI panel, string name, string label, string icon, string tipTitle, string tipDetails, Color color, UnityEngine.Events.UnityAction action,
            System.Func<bool> canUse, RectTransform leftOf)
        {
            // The storage panel's small round Sort button; its wide Take all button if that's missing.
            GameObject source = Navigation()?.storageSortButton;
            if (source == null)
                source = Inventory.instance?.storagePanelUI?.takeAllButton;
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
            if (!SetIcon(copy, icon) && label != null)
                SetLabel(copy, label);
            AddToolTip(panel, copy, tipTitle, tipDetails);
            Paint(copy, button, color);
            LayoutElement layout = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            copy.AddComponent<Placer>().Init(panel, (RectTransform)slots, leftOf, button, canUse, ((RectTransform)source.transform).rect.size);
            Plugin.Log.LogInfo($"{name} button added to {panel.name}");
            return (RectTransform)copy.transform;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> icons = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>Our icon instead of the text, at 65% of the button, on top of the round background. False if it can't be loaded.</summary>
        private static bool SetIcon(GameObject button, string file)
        {
            Sprite sprite = Icon(file);
            if (sprite == null)
                return false;
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
                text.gameObject.SetActive(false);
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(button.transform, false);
            // 65% of the button; the take-all arrow is 15% smaller, it looks bigger than the harvest hand at the same size.
            float side = 0.65f * (file == "take-all.png" ? 0.85f : 1f);
            rt.anchorMin = Vector2.one * (0.5f - side / 2f);
            rt.anchorMax = Vector2.one * (0.5f + side / 2f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return true;
        }

        private static Sprite Icon(string file)
        {
            if (file == null)
                return null;
            if (icons.TryGetValue(file, out Sprite cached) && cached != null)
                return cached;
            using (System.IO.Stream stream = typeof(TakeAllButton).Assembly.GetManifestResourceStream("Unclicker.Icons." + file))
            {
                if (stream == null)
                {
                    Plugin.Log.LogError($"Icon {file} is missing from the dll");
                    return null;
                }
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                if (!ImageConversion.LoadImage(tex, bytes))
                    return null;
                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = "Unclicker " + file;
                icons[file] = sprite;
                return sprite;
            }
        }

        /// <summary>The game's hover tooltip, as on the modifier icons (same hover sound).</summary>
        private static void AddToolTip(CraftingPanelBaseUI panel, GameObject button, string title, string details)
        {
            ToolTipInfo tip = button.GetComponent<ToolTipInfo>() ?? button.AddComponent<ToolTipInfo>();
            ToolTipInfo modifierTip = panel.modifiersUI?.modifierPrefab?.GetComponentInChildren<ToolTipInfo>(true);
            tip.onPointerEnterSound = modifierTip?.onPointerEnterSound ?? new SoundInfo();
            tip.UpdateToolTipInfo(title, details, update: false);
        }

        /// <summary>The round Sort button is 40 px wide: a longer label shrinks to fit instead of spilling out.</summary>
        private static void SetLabel(GameObject button, string label)
        {
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Overflow; // the copied text ends in "..." when it doesn't fit
                text.fontSizeMax = text.fontSize;
                text.fontSizeMin = text.fontSize * 0.6f;
                text.enableAutoSizing = true;
                text.text = label;
            }
        }

        private static Sprite circle;

        /// <summary>A white disc with a darker rim and a soft edge, drawn once; tinted per button through Image.color.</summary>
        private static Sprite Circle()
        {
            if (circle != null)
                return circle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f - 1f, rim = 3f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                    float alpha = Mathf.Clamp01(r - d + 0.5f);
                    float shade = d > r - rim ? 0.55f : 1f - 0.12f * (y / (float)size); // rim, then a slight top-to-bottom light
                    byte v = (byte)(255 * shade);
                    pixels[y * size + x] = new Color32(v, v, v, (byte)(255 * alpha));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            circle.name = "Unclicker round button";
            return circle;
        }

        /// <summary>Our own round look in the given color; the game's sprite swap on hover is off, the Button tints instead.</summary>
        private static void Paint(GameObject buttonObject, UnityEngine.UI.Button button, Color color)
        {
            Image image = buttonObject.GetComponent<Image>() ?? buttonObject.GetComponentInChildren<Image>(true);
            if (image == null)
                return;
            image.sprite = Circle();
            image.type = Image.Type.Simple;
            image.color = color;
            DefaultUIButton defaultButton = buttonObject.GetComponent<DefaultUIButton>();
            if (defaultButton != null)
                defaultButton.isSpritesDisabled = true;
            if (button != null)
                button.transition = Selectable.Transition.None;
            Tint tint = buttonObject.GetComponent<Tint>() ?? buttonObject.AddComponent<Tint>();
            tint.Init(image, button, color);
        }

        /// <summary>
        /// Colors the button itself: Selectable's tint fought the game's own button script and left it half transparent.
        /// Normal, lighter on hover, darker while pressed, grey (but solid) when it can't be used.
        /// </summary>
        internal sealed class Tint : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
            UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
        {
            private Image image;
            private UnityEngine.UI.Button button;
            private Color color;
            private bool hover, down;

            public void Init(Image target, UnityEngine.UI.Button source, Color baseColor)
            {
                image = target;
                button = source;
                color = baseColor;
            }

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) => hover = true;
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => hover = down = false;
            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => down = true;
            public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => down = false;
            private void OnDisable() => hover = down = false;

            private void LateUpdate()
            {
                if (image == null)
                    return;
                bool usable = button == null || button.IsInteractable();
                Color c = !usable ? Color.Lerp(color, Color.grey, 0.7f)
                    : down ? color * 0.75f
                    : hover ? Color.Lerp(color, Color.white, 0.3f)
                    : color;
                c.a = 1f;
                image.color = c;
                image.canvasRenderer.SetColor(Color.white); // undo any fade left by a transition or the game's script
            }
        }

        // Its singleton may not be set yet when stations start.
        private static InventoryNavigationMovement Navigation() =>
            InventoryNavigationMovement.Instance ??
            Object.FindObjectsByType<InventoryNavigationMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

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
        /// Keeps the button at the right end of the modifier strip once <see cref="ModifiersStrip"/> has moved it above the grid; otherwise
        /// just above the top-right corner of the station's inventory, whatever the panel's layout: of the scroll
        /// area around the slot grid when there is one, so a long grid doesn't push it out of sight. Greys it out while there's
        /// nothing to do.
        /// </summary>
        private sealed class Placer : MonoBehaviour
        {
            private CraftingPanelBaseUI panel;
            private RectTransform slots;
            private RectTransform leftOf;
            private RectTransform self;
            private readonly Vector3[] corners = new Vector3[4];

            private CanvasGroup group;
            private UnityEngine.UI.Button button;
            private System.Func<bool> canUse;
            private RectTransform frame;
            private float nextCheck;

            /// <param name="size">The original button's size: the copy's sizeDelta was relative to the original's stretched anchors.</param>
            public void Init(CraftingPanelBaseUI owner, RectTransform slotPanel, RectTransform left, UnityEngine.UI.Button target, System.Func<bool> usable, Vector2 size)
            {
                panel = owner;
                slots = slotPanel;
                leftOf = left;
                button = target;
                canUse = usable;
                ScrollRect scroll = slotPanel.GetComponentInParent<ScrollRect>(true);
                frame = scroll != null ? (RectTransform)scroll.transform : slotPanel;
                self = (RectTransform)transform;
                self.anchorMin = self.anchorMax = new Vector2(0.5f, 0.5f);
                self.pivot = new Vector2(1f, 0f);
                // Fixed anchors turn a stretched sizeDelta into a real size (it came out as 140 x -40: there, but not drawn).
                // Always a circle: while a save loads, the panels aren't laid out yet and Sort's rect can still be stretched.
                // Sort is 40 x 40 once laid out.
                float side = Mathf.Min(size.x, size.y);
                if (side <= 1f || side > 80f)
                    side = 40f;
                side *= 0.9f; // a bit smaller than Sort, to sit on the modifier strip
                self.sizeDelta = new Vector2(side, side);
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
                ModifiersStrip.Ensure(panel, frame);
                RectTransform strip = ModifiersStrip.MovedStrip(panel);
                if (strip != null && strip.gameObject.activeInHierarchy)
                {
                    // On the strip above the grid: at its right end, centred on its height.
                    strip.GetWorldCorners(corners);
                    float middle = (corners[2].y + corners[3].y) / 2f;
                    self.position = new Vector3(corners[2].x - 6f * self.lossyScale.x, middle - self.rect.height / 2f * self.lossyScale.y, corners[2].z);
                    return;
                }
                frame.GetWorldCorners(corners); // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
                // Over the grid's top-right corner: the bottom-right corner sits 5% of the button's width right of it and 70% of
                // its height below it.
                Vector2 size = self.rect.size;
                self.position = corners[2] + new Vector3(0.05f * size.x * self.lossyScale.x, -0.7f * size.y * self.lossyScale.y, 0f);
            }
        }
    }
}
