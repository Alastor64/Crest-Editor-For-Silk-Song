using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SilksongHelper;

/// <summary>
/// Some crest behaviours are hard-coded as "is crest X equipped" checks and
/// are not represented by HeroControllerConfig. Scope those checks to the
/// editor module that owns the behaviour instead of pretending every selected
/// source crest is equipped globally.
/// </summary>
internal static class CrestIdentityPatches
{
    [ThreadStatic]
    private static CharmPart? _identityPart;

    private static void Enter(CharmPart part, out CharmPart? __state)
    {
        __state = _identityPart;
        _identityPart = part;
    }

    private static Exception? Exit(Exception? __exception, CharmPart? __state)
    {
        _identityPart = __state;
        return __exception;
    }

    private static bool SelectedMatches(string? crestId)
    {
        if (_identityPart == null || Plugin.Applier?.ActiveCharm == null)
            return false;
        return Plugin.Applier.UsesCrestFor(_identityPart.Value, crestId);
    }

    [HarmonyPatch(typeof(ToolCrest), nameof(ToolCrest.IsEquipped), MethodType.Getter)]
    internal static class ToolCrestIsEquippedPatch
    {
        internal static void Postfix(ToolCrest __instance, ref bool __result)
        {
            if (_identityPart == null || Plugin.Applier?.ActiveCharm == null)
                return;
            __result = SelectedMatches(__instance.name);
        }
    }

    [HarmonyPatch]
    internal static class HealIdentityScope
    {
        internal static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(HeroController)))
            {
                if (method.Name == "AddSilk" || method.Name == "FallCheck")
                    yield return method;
            }

            var bindCost = AccessTools.PropertyGetter(typeof(SilkSpool), "BindCost");
            if (bindCost != null) yield return bindCost;
        }

        internal static void Prefix(out CharmPart? __state)
            => Enter(CharmPart.HealMethod, out __state);

        internal static Exception? Finalizer(Exception? __exception, CharmPart? __state)
            => Exit(__exception, __state);
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.BindCompleted))]
    internal static class PostHealIdentityScope
    {
        internal static void Prefix(out CharmPart? __state)
            => Enter(CharmPart.PostHealEffect, out __state);

        internal static Exception? Finalizer(Exception? __exception, CharmPart? __state)
            => Exit(__exception, __state);
    }

    [HarmonyPatch(typeof(BindOrbHudFrame), "DoChangeFrame")]
    internal static class HudIdentityScope
    {
        internal static void Prefix(out CharmPart? __state)
            => Enter(CharmPart.Slot, out __state);

        internal static Exception? Finalizer(Exception? __exception, CharmPart? __state)
            => Exit(__exception, __state);
    }

    [HarmonyPatch]
    internal static class SpecialSkillIdentityScope
    {
        internal static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(HeroController)))
            {
                switch (method.Name)
                {
                    case "Attack":
                    case "NailHitEnemy":
                    case "DoSpecialDamage":
                    case "get_IsWandererLucky":
                        yield return method;
                        break;
                }
            }

            AddIfFound(typeof(DamageEnemies), "DoDamage", out var damage);
            if (damage != null) yield return damage;
            AddIfFound(typeof(HealthManager), "TakeDamage", out var health);
            if (health != null) yield return health;
            AddIfFound(typeof(ActiveCorpse), "DoQueuedBurnEffects", out var corpse);
            if (corpse != null) yield return corpse;
            AddIfFound(typeof(HeroShamanRuneEffect), "Refresh", out var shaman);
            if (shaman != null) yield return shaman;
        }

        internal static void Prefix(out CharmPart? __state)
            => Enter(CharmPart.SpecialSkill, out __state);

        internal static Exception? Finalizer(Exception? __exception, CharmPart? __state)
            => Exit(__exception, __state);

        private static void AddIfFound(Type type, string name, out MethodBase? method)
            => method = AccessTools.Method(type, name);
    }

    [HarmonyPatch(typeof(HeroController), "IsHunterCrestEquipped")]
    internal static class HunterIdentityPatch
    {
        internal static void Postfix(ref bool __result)
        {
            if (Plugin.Applier?.ActiveCharm == null) return;
            var id = Plugin.Applier.SelectedCrestId(CharmPart.SpecialSkill);
            __result = id != null
                && (id.Equals("Hunter", StringComparison.OrdinalIgnoreCase)
                    || id.StartsWith("Hunter_", StringComparison.OrdinalIgnoreCase));
        }
    }

    [HarmonyPatch(typeof(HeroController), "IsArchitectCrestEquipped")]
    internal static class ArchitectIdentityPatch
    {
        internal static void Postfix(ref bool __result)
        {
            if (Plugin.Applier?.ActiveCharm == null) return;
            __result = Plugin.Applier.UsesCrestFor(CharmPart.SpecialSkill, "Toolmaster");
        }
    }

    [HarmonyPatch(typeof(HeroController), "IsShamanCrestEquipped")]
    internal static class ShamanIdentityPatch
    {
        internal static void Postfix(ref bool __result)
        {
            if (Plugin.Applier?.ActiveCharm == null) return;
            __result = Plugin.Applier.UsesCrestFor(CharmPart.SpecialSkill, "Spell");
        }
    }
}
