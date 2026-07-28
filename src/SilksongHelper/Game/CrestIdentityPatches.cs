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
    internal enum IdentityScope
    {
        None,
        HealMethod,
        BindCompleted,
        Slot,
        CombatBehaviours,
    }

    [ThreadStatic]
    private static IdentityScope _identityScope;

    private static void Enter(IdentityScope scope, out IdentityScope __state)
    {
        __state = _identityScope;
        _identityScope = scope;
    }

    private static Exception? Exit(Exception? __exception, IdentityScope __state)
    {
        _identityScope = __state;
        return __exception;
    }

    private static bool SelectedMatches(string? crestId)
    {
        if (_identityScope == IdentityScope.None
            || Plugin.Applier?.ActiveCharm == null
            || string.IsNullOrEmpty(crestId))
            return false;

        bool Is(string id) => string.Equals(crestId, id, StringComparison.OrdinalIgnoreCase);

        switch (_identityScope)
        {
            case IdentityScope.HealMethod:
                if (Is("Warrior")) return false;
                return Plugin.Applier.UsesCrestFor(CharmPart.HealMethod, crestId);

            case IdentityScope.BindCompleted:
                // Never enter Warrior/Reaper's native state while a synthetic
                // crest ID is equipped. Their later native update paths assume
                // that the matching full crest/config is active and can crash
                // Unity. These behaviours are implemented independently.
                if (Is("Warrior") || Is("Reaper")) return false;
                return Plugin.Applier.UsesCrestFor(CharmPart.HealMethod, crestId);

            case IdentityScope.Slot:
                return Plugin.Applier.UsesCrestFor(CharmPart.Slot, crestId);

            case IdentityScope.CombatBehaviours:
                // Warrior/Reaper combat checks consume the state created after
                // binding. The remaining identity checks are crest special
                // skills (Architect, Hunter, Shaman, Wanderer, etc.).
                if (Is("Warrior") || Is("Reaper"))
                    return Plugin.Applier.UsesCrestFor(CharmPart.PostHealEffect, crestId);
                return Plugin.Applier.UsesCrestFor(CharmPart.SpecialSkill, crestId);

            default:
                return false;
        }
    }

    [HarmonyPatch(typeof(ToolCrest), nameof(ToolCrest.IsEquipped), MethodType.Getter)]
    internal static class ToolCrestIsEquippedPatch
    {
        internal static void Postfix(ToolCrest __instance, ref bool __result)
        {
            if (_identityScope == IdentityScope.None || Plugin.Applier?.ActiveCharm == null)
                return;
            __result = SelectedMatches(__instance.name);
        }
    }

    [HarmonyPatch]
    internal static class ChargedAttackIdentityPatch
    {
        internal static MethodBase? TargetMethod()
        {
            var actionType = AccessTools.TypeByName(
                "HutongGames.PlayMaker.Actions.CheckIfCrestEquipped");
            return actionType == null
                ? null
                : AccessTools.PropertyGetter(actionType, "IsTrue");
        }

        internal static void Postfix(object __instance, ref bool __result)
        {
            if (Plugin.Applier?.ActiveCharm == null || !IsCrestAttacksFsm(__instance))
                return;

            var crestMember = AccessTools.Field(__instance.GetType(), "Crest")?.GetValue(__instance);
            var value = crestMember == null
                ? null
                : AccessTools.Property(crestMember.GetType(), "Value")?.GetValue(crestMember);
            if (value is ToolCrest crest)
                __result = Plugin.Applier.UsesCrestFor(CharmPart.ChargedAttack, crest.name);
        }

        private static bool IsCrestAttacksFsm(object action)
        {
            try
            {
                var fsm = AccessTools.Property(action.GetType(), "Fsm")?.GetValue(action);
                if (fsm == null) return false;

                var name = AccessTools.Property(fsm.GetType(), "Name")?.GetValue(fsm) as string
                           ?? AccessTools.Field(fsm.GetType(), "name")?.GetValue(fsm) as string;
                return string.Equals(name, "Crest Attacks", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
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

        internal static void Prefix(out IdentityScope __state)
            => Enter(IdentityScope.HealMethod, out __state);

        internal static Exception? Finalizer(Exception? __exception, IdentityScope __state)
            => Exit(__exception, __state);
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.BindCompleted))]
    internal static class PostHealIdentityScope
    {
        internal static void Prefix(out IdentityScope __state)
            => Enter(IdentityScope.BindCompleted, out __state);

        internal static Exception? Finalizer(Exception? __exception, IdentityScope __state)
            => Exit(__exception, __state);
    }

    [HarmonyPatch(typeof(BindOrbHudFrame), "DoChangeFrame")]
    internal static class HudIdentityScope
    {
        internal static void Prefix(out IdentityScope __state)
            => Enter(IdentityScope.Slot, out __state);

        internal static Exception? Finalizer(Exception? __exception, IdentityScope __state)
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

            foreach (var method in NamedMethods(typeof(DamageEnemies), "DoDamage"))
                yield return method;
            foreach (var method in NamedMethods(typeof(HealthManager), "TakeDamage"))
                yield return method;
            foreach (var method in NamedMethods(typeof(ActiveCorpse), "DoQueuedBurnEffects"))
                yield return method;
            foreach (var method in NamedMethods(typeof(HeroShamanRuneEffect), "Refresh"))
                yield return method;
        }

        internal static void Prefix(out IdentityScope __state)
            => Enter(IdentityScope.CombatBehaviours, out __state);

        internal static Exception? Finalizer(Exception? __exception, IdentityScope __state)
            => Exit(__exception, __state);

        private static IEnumerable<MethodBase> NamedMethods(Type type, string name)
        {
            foreach (var method in AccessTools.GetDeclaredMethods(type))
                if (method.Name == name && !method.IsAbstract)
                    yield return method;
        }
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
