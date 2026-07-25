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
        CharmPart.PostHealEffect => new[] { "canPlayNeedolin" },
        CharmPart.UpSlash => new[]
        {
            "upSlashType", "upSlashEvent", "upSlashAnticTime", "upSlashTime",
            "upSlashSpeed", "upSlashRecoveryTime",
        },
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

