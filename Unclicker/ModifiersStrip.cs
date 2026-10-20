using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Unclicker
{
    /// <summary>
    /// Station panels show the station's modifier icons (quality, capacity, upgrades, conditions) in a strip under the slot grid, where
    /// the recipe info popup of a running craft covers it. This moves the strip above the grid and the grid down by the strip's height,
    /// so the strip stays in sight however many slots the station has. Our Take all / Harvest buttons then sit on the strip.
    /// The layout is prefab data, so it's done at runtime, once per panel, when the panel is first shown:
    /// - the strip and the grid area are siblings under a vertical layout group: the strip goes before the grid;
    /// - placed by hand: when the strip sits right under the grid, the two swap places (the gap between them is kept).
    /// Anything else is left alone (logged once).
    /// </summary>
    internal static class ModifiersStrip
    {
        private sealed class Moved
        {
            public RectTransform Grid, Strip;
            public Vector2 GridPosition, StripPosition;
            public int StripIndex = -1;
            public int GridIndex = -1;
            public RectTransform Icons;
            public Vector3 IconsScale;
            public Vector2 IconsPosition;
        }

        private static readonly Dictionary<CraftingPanelBaseUI, Moved> moved = new Dictionary<CraftingPanelBaseUI, Moved>();
        private static readonly HashSet<CraftingPanelBaseUI> done = new HashSet<CraftingPanelBaseUI>();
        private static readonly Vector3[] corners = new Vector3[4];

        /// <summary>The strip of a panel whose strip is above its grid now; null otherwise.</summary>
        public static RectTransform MovedStrip(CraftingPanelBaseUI panel) =>
            panel != null && moved.ContainsKey(panel) ? (RectTransform)panel.modifiersUI.transform : null;

        /// <summary>Called every frame by the panel's buttons; does the move the first time the panel is laid out.</summary>
        public static void Ensure(CraftingPanelBaseUI panel, RectTransform gridFrame)
        {
            if (panel == null || done.Contains(panel) || !Plugin.ModifiersOnTop.Value || panel.modifiersUI == null || gridFrame == null)
                return;
            var strip = (RectTransform)panel.modifiersUI.transform;
            if (!strip.gameObject.activeInHierarchy || !gridFrame.gameObject.activeInHierarchy)
                return;
            Canvas.ForceUpdateCanvases();
            if (strip.rect.height <= 1f || gridFrame.rect.height <= 1f)
                return; // not laid out yet
            done.Add(panel);
            try
            {
                Move(panel, strip, gridFrame);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Modifiers strip on {panel.name}: {e}");
            }
        }

        private static void Move(CraftingPanelBaseUI panel, RectTransform strip, RectTransform gridFrame)
        {
            // The children of the lowest common parent holding each side.
            Transform parent = CommonParent(strip, gridFrame);
            RectTransform stripSide = ChildOnPath(parent, strip);
            RectTransform gridSide = ChildOnPath(parent, gridFrame);
            if (parent == null || stripSide == null || gridSide == null || stripSide == gridSide)
            {
                Plugin.Log.LogInfo($"Modifiers strip on {panel.name}: no separate strip and grid under one parent ({Path(strip)} / {Path(gridFrame)}); left as is");
                return;
            }

            // The strip inside a bigger block (Controls: the strip on top, the progress list under it): move the strip itself up over
            // the grid, and the grid down onto the strip's old place. Only without layout groups, which would undo it.
            if (stripSide.rect.height > 2f * strip.rect.height + 10f)
            {
                MoveInner(panel, parent, strip, stripSide, gridSide);
                return;
            }

            var record = new Moved { Grid = gridSide, Strip = stripSide, GridPosition = gridSide.anchoredPosition, StripPosition = stripSide.anchoredPosition };
            VerticalLayoutGroup layout = parent.GetComponent<VerticalLayoutGroup>();
            if (layout != null && layout.enabled)
            {
                if (stripSide.GetSiblingIndex() < gridSide.GetSiblingIndex())
                {
                    Plugin.Log.LogInfo($"Modifiers strip on {panel.name}: already above the slots");
                    return;
                }
                record.StripIndex = stripSide.GetSiblingIndex();
                stripSide.SetSiblingIndex(gridSide.GetSiblingIndex());
                moved[panel] = record;
            ShrinkIcons(panel, record);
                Plugin.Log.LogInfo($"Modifiers strip on {panel.name}: moved above the slots (layout group {parent.name})");
                return;
            }

            // By hand, in the parent's space.
            (float gridTop, float gridBottom) = VerticalSpan(gridSide, parent);
            (float stripTop, float stripBottom) = VerticalSpan(stripSide, parent);
            float stripHeight = stripTop - stripBottom;
            float gap = gridBottom - stripTop;
            if (gap < -2f || gap > stripHeight)
            {
                Plugin.Log.LogInfo($"Modifiers strip on {panel.name}: not right under the slots (gap {gap:0}, strip {stripHeight:0}); left as is");
                return;
            }
            stripSide.anchoredPosition += new Vector2(0f, gridTop - stripTop);
            gridSide.anchoredPosition -= new Vector2(0f, stripHeight + gap);
            moved[panel] = record;
            ShrinkIcons(panel, record);
            Plugin.Log.LogInfo($"Modifiers strip on {panel.name}: moved above the slots ({stripSide.name} over {gridSide.name})");
        }

        private static void MoveInner(CraftingPanelBaseUI panel, Transform parent, RectTransform strip, RectTransform stripSide, RectTransform gridSide)
        {
            if (HasLayout(parent) || HasLayout(strip.parent))
            {
                Plugin.Log.LogInfo($"Modifiers strip on {panel.name}: inside {stripSide.name} under a layout group; left as is");
                return;
            }
            (float gridTop, float gridBottom) = VerticalSpan(gridSide, parent);
            (float stripTop, float stripBottom) = VerticalSpan(strip, parent);
            float stripHeight = stripTop - stripBottom;
            float gap = gridBottom - stripTop;
            if (gap < -2f || gap > stripHeight)
            {
                Plugin.Log.LogInfo($"Modifiers strip on {panel.name}: not right under the slots (gap {gap:0}, strip {stripHeight:0}); left as is");
                return;
            }
            var record = new Moved
            {
                Grid = gridSide, Strip = strip, GridPosition = gridSide.anchoredPosition, StripPosition = strip.anchoredPosition,
                GridIndex = gridSide.GetSiblingIndex()
            };
            strip.position += parent.TransformVector(new Vector3(0f, gridTop - stripTop, 0f));
            gridSide.anchoredPosition -= new Vector2(0f, stripHeight + gap);
            // The grid now covers the top of the strip's block: draw it after the block, so the block's background stays under it.
            if (gridSide.GetSiblingIndex() < stripSide.GetSiblingIndex())
                gridSide.SetSiblingIndex(stripSide.GetSiblingIndex());
            moved[panel] = record;
            ShrinkIcons(panel, record);
            Plugin.Log.LogInfo($"Modifiers strip on {panel.name}: moved above the slots ({strip.name} out of {stripSide.name}, {gridSide.name} down {stripHeight + gap:0})");
        }

        /// <summary>Icons at 1/1.25 of their size: the icon list is scaled, its left edge and vertical centre stay where they were.</summary>
        private static void ShrinkIcons(CraftingPanelBaseUI panel, Moved record)
        {
            var icons = panel.modifiersUI.modifierList != null ? panel.modifiersUI.modifierList.transform as RectTransform : null;
            if (icons == null)
                return;
            record.Icons = icons;
            record.IconsScale = icons.localScale;
            record.IconsPosition = icons.anchoredPosition;
            icons.GetWorldCorners(corners);
            Vector3 anchor = (corners[0] + corners[1]) / 2f; // middle of the left edge
            icons.localScale = record.IconsScale * IconScale;
            icons.GetWorldCorners(corners);
            icons.position += anchor - (corners[0] + corners[1]) / 2f;
        }

        private const float IconScale = 1f / 1.25f;

        private static bool HasLayout(Transform t)
        {
            LayoutGroup group = t != null ? t.GetComponent<LayoutGroup>() : null;
            return group != null && group.enabled;
        }

        private static (float Top, float Bottom) VerticalSpan(RectTransform rect, Transform space)
        {
            rect.GetWorldCorners(corners); // 0 bottom-left, 1 top-left
            return (space.InverseTransformPoint(corners[1]).y, space.InverseTransformPoint(corners[0]).y);
        }

        private static Transform CommonParent(Transform a, Transform b)
        {
            var above = new HashSet<Transform>();
            for (Transform t = a.parent; t != null; t = t.parent)
                above.Add(t);
            for (Transform t = b.parent; t != null; t = t.parent)
                if (above.Contains(t))
                    return t;
            return null;
        }

        private static RectTransform ChildOnPath(Transform parent, Transform leaf)
        {
            if (parent == null)
                return null;
            Transform t = leaf;
            while (t != null && t.parent != parent)
                t = t.parent;
            return t as RectTransform;
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        /// <summary>Puts every moved strip and grid back.</summary>
        public static void Unload()
        {
            foreach (Moved m in moved.Values)
            {
                if (m.Icons != null)
                {
                    m.Icons.localScale = m.IconsScale;
                    m.Icons.anchoredPosition = m.IconsPosition;
                }
                if (m.Grid == null || m.Strip == null)
                    continue;
                if (m.StripIndex >= 0)
                    m.Strip.SetSiblingIndex(m.StripIndex);
                else
                {
                    m.Strip.anchoredPosition = m.StripPosition;
                    m.Grid.anchoredPosition = m.GridPosition;
                    if (m.GridIndex >= 0)
                        m.Grid.SetSiblingIndex(m.GridIndex);
                }
            }
            moved.Clear();
            done.Clear();
        }
    }
}
