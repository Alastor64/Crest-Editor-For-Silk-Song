using System;
using HarmonyLib;

namespace SilksongHelper;

/// <summary>
/// Routes each requested hero animation to the crest selected for that editor
/// module. This leaves the game's animation libraries untouched and lets a
/// missing source override fall back to the original shared hero animation.
/// </summary>
internal static class CrestAnimationPatches
{
    [ThreadStatic]
    private static bool _routing;

    [HarmonyPatch(typeof(HeroControllerConfig), nameof(HeroControllerConfig.GetAnimationClip))]
    internal static class GetAnimationClipPatch
    {
        internal static void Postfix(
            HeroControllerConfig __instance,
            object[] __args,
            ref tk2dSpriteAnimationClip __result)
        {
            if (_routing || Plugin.Applier?.ActiveCharm == null
                || __args.Length == 0 || __args[0] is not string clipName)
                return;

            var part = PartForClip(clipName);
            if (!part.HasValue) return;

            var crestId = Plugin.Applier.SelectedCrestId(part.Value);
            var source = ResolveConfig(crestId);
            if (source == null || ReferenceEquals(source, __instance)) return;

            _routing = true;
            try
            {
                // A null result is intentional: HeroAnimationController then
                // falls back to the game's shared animation. This is required
                // for Shaman up-slash, whose config does not define UpSlash.
                __result = source.GetAnimationClip(clipName);
            }
            finally
            {
                _routing = false;
            }
        }
    }

    private static HeroControllerConfig? ResolveConfig(string? crestId)
    {
        crestId = DeathGodModule.RuntimeSourceId(crestId);
        if (string.IsNullOrEmpty(crestId)) return null;
        try
        {
            var crest = ToolItemManager.GetCrestByName(crestId);
            if (crest?.HeroConfig != null) return crest.HeroConfig;
        }
        catch { }
        return CrestCatalog.ById(crestId)?.HeroConfig as HeroControllerConfig;
    }

    private static CharmPart? PartForClip(string name)
    {
        bool Has(string value)
            => name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        bool Starts(string value)
            => name.StartsWith(value, StringComparison.OrdinalIgnoreCase);

        if (Starts("UpSlash") || Starts("Up Slash"))
            return CharmPart.UpSlash;
        if (Starts("Down") || Has("SpinBall") || Has("Drill Grind"))
            return CharmPart.DownSlashJump;
        if (Starts("Slash_Charged") || Starts("NeedleArt"))
            return CharmPart.ChargedAttack;
        if ((Starts("Dash ") || Starts("DashStab") || Starts("Sprint Followup"))
            && !Has("SpinBall"))
            return CharmPart.DashAttack;
        if (name.Equals("Slash", StringComparison.OrdinalIgnoreCase)
            || name.Equals("SlashAlt", StringComparison.OrdinalIgnoreCase)
            || name.Equals("SlashEffect", StringComparison.OrdinalIgnoreCase)
            || name.Equals("SlashEffectAlt", StringComparison.OrdinalIgnoreCase)
            || Has("Wall Slash")
            || Has("RecoilStab"))
            return CharmPart.NormalAttack;
        return null;
    }
}
