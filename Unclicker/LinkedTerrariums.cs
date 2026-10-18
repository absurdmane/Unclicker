using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Unity.Mono.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace Unclicker
{
    /// <summary>
    /// Soft support for the Linked Terrariums mod: touching terrariums share one panel, and its <c>StartGrow</c> prefix plants
    /// across the whole group from every member's storage. When the open terrarium is linked, our amount counts the group's seeds
    /// and free slots, not just the open one's. Found by reflection; without the mod everything here is a no-op.
    /// </summary>
    internal static class LinkedTerrariums
    {
        // Renamed to Linked Planters in its 1.1.0 (GUID and namespace); the old name is still found for older installs.
        private static readonly (string Guid, string Namespace, string Links)[] Variants =
        {
            ("LinkedPlanters", "LinkedPlanters", "PlanterLinks"),
            ("LinkedTerrariums", "LinkedTerrariums", "TerrariumLinks")
        };

        private static bool resolved;
        private static Func<Growing, bool> isActiveGroup;
        private static Func<Recipe, int> totalSeeds;
        private static Func<Recipe, int> totalFreeSlots;
        private static FieldInfo group;

        /// <summary>True when the growing device is the open terrarium of a linked group.</summary>
        public static bool IsLinked(Growing growing)
        {
            Resolve();
            if (isActiveGroup == null || growing == null)
                return false;
            try
            {
                return isActiveGroup(growing);
            }
            catch (Exception e)
            {
                Disable(e);
                return false;
            }
        }

        /// <summary>The open terrarium's linked group (itself first), or just the device when it isn't linked.</summary>
        public static IReadOnlyList<Growing> Group(Growing growing)
        {
            if (IsLinked(growing) && group != null)
            {
                try
                {
                    if (group.GetValue(null) is List<Growing> list && list.Count > 0)
                        return list.ToArray();
                }
                catch (Exception e)
                {
                    Disable(e);
                }
            }
            return new[] { growing };
        }

        /// <summary>
        /// The group a device would have if its panel were opened now: Linked Terrariums builds groups only on open, so for a device
        /// in the world (Shift + interact) we open and close its group the same way, with no panel. Only on that press: the flood
        /// fill scans every growing device.
        /// </summary>
        public static IReadOnlyList<Growing> WorldGroup(Growing growing)
        {
            Resolve();
            if (open == null || close == null || active == null || group == null || growing == null)
                return new[] { growing };
            try
            {
                if (active.GetValue(null) is UnityEngine.Object current && current != null)
                    return Group(growing); // a panel is open: its group is live, don't touch it
                open(growing);
                try
                {
                    return group.GetValue(null) is List<Growing> list && list.Count > 0 ? list.ToArray() : new[] { growing };
                }
                finally
                {
                    close(growing);
                }
            }
            catch (Exception e)
            {
                Disable(e);
                return new[] { growing };
            }
        }

        private static Action<Growing> open;
        private static Action<Growing> close;
        private static FieldInfo active;

        /// <summary>How many the group can plant now: seeds in all linked storages (and the player's, via our counting) vs. free slots.</summary>
        public static int Max(Recipe recipe)
        {
            try
            {
                return Math.Min(totalSeeds(recipe), totalFreeSlots(recipe));
            }
            catch (Exception e)
            {
                Disable(e);
                return 0;
            }
        }

        private static void Resolve()
        {
            if (resolved)
                return;
            resolved = true;
            BepInEx.PluginInfo info = null;
            (string Guid, string Namespace, string Links) variant = default;
            foreach (var v in Variants)
                if (UnityChainloader.Instance.Plugins.TryGetValue(v.Guid, out info) && info.Instance != null)
                {
                    variant = v;
                    break;
                }
            if (info?.Instance == null)
                return;
            try
            {
                var asm = info.Instance.GetType().Assembly;
                Type links = asm.GetType($"{variant.Namespace}.{variant.Links}");
                Type planting = asm.GetType($"{variant.Namespace}.Planting");
                isActiveGroup = AccessTools.MethodDelegate<Func<Growing, bool>>(AccessTools.Method(links, "IsActiveGroup"));
                totalSeeds = AccessTools.MethodDelegate<Func<Recipe, int>>(AccessTools.Method(planting, "TotalSeeds"));
                totalFreeSlots = AccessTools.MethodDelegate<Func<Recipe, int>>(AccessTools.Method(planting, "TotalFreeSlots"));
                group = AccessTools.Field(links, "Group");
                active = AccessTools.Field(links, "Active");
                open = AccessTools.MethodDelegate<Action<Growing>>(AccessTools.Method(links, "Open"));
                close = AccessTools.MethodDelegate<Action<Growing>>(AccessTools.Method(links, "Close"));
                Plugin.Log.LogInfo($"{info.Metadata.Name} found: amounts on linked devices count the whole group.");
            }
            catch (Exception e)
            {
                Disable(e);
            }
        }

        private static void Disable(Exception e)
        {
            isActiveGroup = null;
            open = null;
            Plugin.Log.LogWarning($"Linked Terrariums support turned off: {e.Message}");
        }
    }
}
