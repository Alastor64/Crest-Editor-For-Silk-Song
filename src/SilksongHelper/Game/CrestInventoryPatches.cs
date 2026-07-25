using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

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
                __result ??= CustomCrestRegistry.Get(__args[0] as string);
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

    [HarmonyPatch(typeof(HeroController), "ResetAllCrestState", typeof(bool))]
    internal static class ResetCrestStatePatch
    {
        internal static void Postfix(HeroController __instance)
        {
            try
            {
                var id = GameRefs.Instance.Get("PlayerData", "CurrentCrestID") as string;
                var customId = CustomCrestRegistry.IdFromSentinel(id);
                if (customId != null)
                {
                    var charm = Plugin.SaveData.Charms.FirstOrDefault(c => c.Id == customId);
                    if (charm != null) Plugin.Applier.ApplyOverrides(charm, __instance);
                }
                else
                {
                    Plugin.Applier.RestoreOverrides();
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"ResetAllCrestState postfix: {e.Message}"); }
        }
    }
}
