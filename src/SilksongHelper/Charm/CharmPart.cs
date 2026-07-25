using System;
using System.Collections.Generic;

namespace SilksongHelper;

public enum CharmPart
{
    Slot,
    NormalAttack,
    HealMethod,
    ChargedAttack,
    DashAttack,
    DownSlashJump,
    PostHealEffect,
    UpSlash,
    SpecialSkill,
}

public static class CharmPartNames
{
    public static string Display(CharmPart p) => p switch
    {
        CharmPart.Slot => "插槽",
        CharmPart.NormalAttack => "普通攻击",
        CharmPart.HealMethod => "回血（缚丝）方式",
        CharmPart.ChargedAttack => "蓄力攻击",
        CharmPart.DashAttack => "冲刺攻击",
        CharmPart.DownSlashJump => "下劈跳",
        CharmPart.PostHealEffect => "回血（缚丝）后特效",
        CharmPart.UpSlash => "上劈",
        CharmPart.SpecialSkill => "特殊技能",
        _ => p.ToString(),
    };

    public static readonly IReadOnlyList<CharmPart> NonSlotParts = new[]
    {
        CharmPart.NormalAttack,
        CharmPart.HealMethod,
        CharmPart.ChargedAttack,
        CharmPart.DashAttack,
        CharmPart.DownSlashJump,
        CharmPart.PostHealEffect,
        CharmPart.UpSlash,
        CharmPart.SpecialSkill,
    };

    public static readonly IReadOnlyList<CharmPart> All = new[]
    {
        CharmPart.Slot,
        CharmPart.NormalAttack,
        CharmPart.HealMethod,
        CharmPart.ChargedAttack,
        CharmPart.DashAttack,
        CharmPart.DownSlashJump,
        CharmPart.PostHealEffect,
        CharmPart.UpSlash,
        CharmPart.SpecialSkill,
    };
}

internal static class PartFields
{
    public static IReadOnlyList<string> For(CharmPart p) => p switch
    {
        CharmPart.NormalAttack => new[]
        {
            "attackCooldownTime", "attackDuration", "attackRecoveryTime",
            "quickAttackCooldownTime", "quickAttackSpeedMult",
            "canTurnWhileSlashing", "wallSlashSlowdown",
        },
        CharmPart.HealMethod => new[] { "canBind", "canBrolly" },
        CharmPart.ChargedAttack => new[]
        {
            "canNailCharge", "chargeSlashChain", "chargeSlashLungeSpeed",
            "chargeSlashLungeDeceleration", "chargeSlashRecoils",
        },
        CharmPart.DashAttack => new[]
        {
            "dashStabSpeed", "dashStabTime", "dashStabSteps",
            "dashStabBounceJumpSpeed", "canHarpoonDash", "forceShortDashStabBounce",
        },
        CharmPart.DownSlashJump => new[]
        {
            "downSlashType", "downSlashEvent", "downspikeAnticTime", "downspikeTime",
            "downspikeSpeed", "downspikeRecoveryTime", "downspikeThrusts", "downspikeBurstEffect",
        },
        // These behaviours are selected by crest identity in the game's
        // HeroController/FSM code rather than by HeroControllerConfig fields.
        CharmPart.PostHealEffect => Array.Empty<string>(),
        CharmPart.UpSlash => Array.Empty<string>(),
        CharmPart.SpecialSkill => Array.Empty<string>(),
        _ => Array.Empty<string>(),
    };
}

internal static class PartGroupFields
{
    public static IReadOnlyList<string> For(CharmPart p) => p switch
    {
        CharmPart.NormalAttack => new[]
        {
            "<NormalSlash>k__BackingField", "<NormalSlashDamager>k__BackingField", "NormalSlashObject",
            "<AlternateSlash>k__BackingField", "<AlternateSlashDamager>k__BackingField", "AlternateSlashObject",
        },
        CharmPart.DownSlashJump => new[]
        {
            "<DownSlash>k__BackingField", "<DownSlashDamager>k__BackingField", "DownSlashObject",
            "<Downspike>k__BackingField",
            "<AltDownSlash>k__BackingField", "<AltDownSlashDamager>k__BackingField", "AltDownSlashObject",
            "<AltDownspike>k__BackingField",
        },
        CharmPart.ChargedAttack => new[] { "ChargeSlash" },
        CharmPart.DashAttack => new[] { "DashStab", "DashStabAlt" },
        CharmPart.UpSlash => new[]
        {
            "<UpSlash>k__BackingField", "<UpSlashDamager>k__BackingField", "UpSlashObject",
            "<AltUpSlash>k__BackingField", "<AltUpSlashDamager>k__BackingField", "AltUpSlashObject",
        },
        _ => Array.Empty<string>(),
    };
}

internal static class PartBehaviour
{
    public static bool UsesCrestIdentity(CharmPart part)
        => part == CharmPart.HealMethod
           || part == CharmPart.PostHealEffect
           || part == CharmPart.SpecialSkill;
}
