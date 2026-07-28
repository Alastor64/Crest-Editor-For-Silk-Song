using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

internal static class CrestInventoryPatches
{
    [HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.GetAllCrests))]
    internal static class GetAllCrestsPatch
    {
        internal static void Postfix(List<ToolCrest> __result)
        {
            try
            {
                if (__result == null) return;
                CustomCrestRegistry.EnsureBuilt();
                __result.RemoveAll(crest =>
                    crest == null
                    || (CustomCrestRegistry.IsSentinel(crest.name)
                        && !CustomCrestRegistry.All.Contains(crest)));
                foreach (var synth in CustomCrestRegistry.All)
                    if (!__result.Contains(synth))
                        __result.Add(synth);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"GetAllCrests postfix: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.GetCrestByName))]
    internal static class GetCrestByNamePatch
    {
        internal static void Postfix(ref ToolCrest? __result, object[] __args)
        {
            try
            {
                if (__result != null) return;
                if (__args == null || __args.Length == 0) return;
                var name = __args[0] as string;
                if (!CustomCrestRegistry.IsSentinel(name)) return;
                // ResetAllCrestState can ask for the equipped crest before the
                // inventory has ever called GetAllCrests in this process.
                CustomCrestRegistry.EnsureBuilt();
                __result = CustomCrestRegistry.Get(name);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"GetCrestByName postfix: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(InventoryToolCrest), nameof(InventoryToolCrest.DisplayName), MethodType.Getter)]
    internal static class DisplayNamePatch
    {
        internal static void Postfix(ref string __result, InventoryToolCrest __instance)
        {
            try
            {
                if (__instance == null) return;
                var custom = CustomCrestRegistry.CustomNameFor(__instance.CrestData);
                if (custom != null) __result = custom;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"DisplayName postfix: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(InventoryToolCrest), nameof(InventoryToolCrest.Description), MethodType.Getter)]
    internal static class DescriptionPatch
    {
        internal static void Postfix(ref string __result, InventoryToolCrest __instance)
        {
            try
            {
                if (__instance == null) return;
                var custom = CustomCrestRegistry.CustomDescriptionFor(__instance.CrestData);
                if (custom != null) __result = custom;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Description postfix: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(InventoryToolCrest), nameof(InventoryToolCrest.IsUnlocked), MethodType.Getter)]
    internal static class IsUnlockedPatch
    {
        internal static void Postfix(ref bool __result, InventoryToolCrest __instance)
        {
            try
            {
                var crestData = __instance?.CrestData;
                if (crestData == null) return;
                if (CustomCrestRegistry.IsSentinel(crestData.name)) __result = true;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"IsUnlocked postfix: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(ToolCrest), nameof(ToolCrest.IsUnlocked), MethodType.Getter)]
    internal static class ToolCrestIsUnlockedPatch
    {
        internal static void Postfix(ToolCrest __instance, ref bool __result)
        {
            if (__instance != null && CustomCrestRegistry.IsSentinel(__instance.name))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(ToolCrest), nameof(ToolCrest.SaveData), MethodType.Getter)]
    internal static class ToolCrestSaveDataPatch
    {
        internal static void Postfix(ToolCrest __instance, ref ToolCrestsData.Data __result)
        {
            if (__result.Slots != null || __instance == null) return;
            var fallback = CustomCrestRegistry.SaveDataFor(__instance.name);
            if (fallback.HasValue) __result = fallback.Value;
        }
    }

    [HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.IsToolEquipped), typeof(string))]
    internal static class IsCustomCrestToolEquippedPatch
    {
        internal static void Postfix(string name, ref bool __result)
        {
            if (__result) return;
            var currentCrest = PlayerData.instance?.CurrentCrestID;
            if (CustomCrestRegistry.IsToolEquipped(currentCrest, name))
                __result = true;
        }
    }

    [HarmonyPatch(
        typeof(ToolItemManager),
        nameof(ToolItemManager.IsToolEquipped),
        typeof(ToolItem),
        typeof(ToolEquippedReadSource))]
    internal static class IsCustomCrestToolEquippedObjectPatch
    {
        internal static void Postfix(ToolItem tool, ref bool __result)
        {
            if (__result || tool == null) return;
            var currentCrest = PlayerData.instance?.CurrentCrestID;
            if (CustomCrestRegistry.IsToolEquipped(currentCrest, tool.name))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(ToolItem), nameof(ToolItem.IsEquippedHud), MethodType.Getter)]
    internal static class IsCustomCrestToolEquippedHudPatch
    {
        internal static void Postfix(ToolItem __instance, ref bool __result)
        {
            if (__result || __instance == null) return;
            var currentCrest = PlayerData.instance?.CurrentCrestID;
            if (CustomCrestRegistry.IsToolEquipped(currentCrest, __instance.name))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(HeroController), "ResetAllCrestState", typeof(bool))]
    internal static class ResetCrestStatePatch
    {
        internal static void Postfix(HeroController __instance)
        {
            try
            {
                var id = CurrentCrestId();
                var customId = CustomCrestRegistry.IdFromSentinel(id);
                if (customId != null)
                {
                    var charm = Plugin.SaveData.Charms.FirstOrDefault(c => c.Id == customId)
                                ?? (Plugin.Applier.ActiveCharmId == customId
                                    ? Plugin.Applier.ActiveCharm
                                    : null);
                    if (charm != null) Plugin.Applier.ApplyOverrides(charm, __instance);
                }
                else
                {
                    bool hadCustomSnapshot = Plugin.Applier.ActiveCharm != null;
                    Plugin.Applier.RestoreOverrides(__instance);
                    if (hadCustomSnapshot)
                        CustomCrestRegistry.MarkDirty();
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"ResetAllCrestState postfix: {e.Message}"); }
        }

        private static string? CurrentCrestId()
        {
            var t = AccessTools.TypeByName("PlayerData");
            if (t == null) return null;
            object? inst = null;
            foreach (var n in new[] { "instance", "_instance", "Instance", "current", "Current" })
            {
                try
                {
                    var p = AccessTools.Property(t, n);
                    if (p != null && p.GetGetMethod(nonPublic: true) != null)
                    {
                        var v = p.GetValue(null, null);
                        if (v is UnityEngine.Object u && u == null) continue;
                        if (v != null) { inst = v; break; }
                    }
                }
                catch { }
                try
                {
                    var f = AccessTools.Field(t, n);
                    if (f != null && f.IsStatic)
                    {
                        var v = f.GetValue(null);
                        if (v is UnityEngine.Object u2 && u2 == null) continue;
                        if (v != null) { inst = v; break; }
                    }
                }
                catch { }
            }
            if (inst == null)
            {
                try { inst = UnityEngine.Object.FindObjectOfType(t); } catch { }
            }
            if (inst == null) return null;
            try { return AccessTools.Field(t, "CurrentCrestID")?.GetValue(inst) as string; }
            catch { return null; }
        }
    }

    [HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.SetEquippedCrest))]
    internal static class SetEquippedCrestPatch
    {
        internal static void Postfix()
        {
            try
            {
                var currentId = PlayerData.instance?.CurrentCrestID;
                if (!CustomCrestRegistry.IsSentinel(currentId)) return;

                var hero = HeroController.instance;
                if (hero == null) return;

                // SetEquippedCrest is the explicit bench/inventory boundary:
                // this is where a definition saved since the previous equip is
                // allowed to replace the active snapshot.
                Plugin.Applier.UseLatestDefinitionOnNextApply();
                CustomCrestRegistry.MarkDirty();
                CustomCrestRegistry.EnsureBuilt();

                // The inventory refresh is not guaranteed to call the same
                // ResetAllCrestState overload on every game version. Invoke the
                // known reset point immediately after committing the crest ID.
                var reset = AccessTools.Method(typeof(HeroController),
                    "ResetAllCrestState", new[] { typeof(bool) });
                reset?.Invoke(hero, new object[] { false });
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"apply equipped custom crest: {e.Message}");
            }
        }
    }
}
