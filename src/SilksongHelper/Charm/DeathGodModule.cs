using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// Built-in editor source requested by 死神模块选项.md. It is intentionally
/// available only for the down-slash module. The vanilla Wanderer supplies the
/// action/config/object baseline. Uniformly scaling the cloned attack object
/// doubles both its renderers and inherited 2D colliders while retaining their
/// exact shape and the original DamageEnemies values.
/// </summary>
internal static class DeathGodModule
{
    public const string Id = "SilksongHelper_DeathGod";
    public const string FallbackCrestId = "Wanderer";
    public const string DisplayName = "死神";
    public const string Description = "动作与漫游者相同；攻击特效和判定范围保持原形并放大至 2 倍，伤害不变。";

    private const string ScaleMarkerName = "SilksongHelper_DeathGodDownSlashScaled";
    private const float DownSlashScale = 2f;

    public static bool Is(string? crestId)
        => string.Equals(crestId, Id, StringComparison.OrdinalIgnoreCase);

    public static string? RuntimeSourceId(string? crestId)
        => Is(crestId) ? FallbackCrestId : crestId;

    public static bool IsSelectableFor(CharmPart part)
        => part == CharmPart.DownSlashJump;

    public static bool MatchesRuntimeIdentity(string? selectedId, string? queriedId)
    {
        if (string.IsNullOrEmpty(selectedId) || string.IsNullOrEmpty(queriedId))
            return false;
        if (string.Equals(selectedId, queriedId, StringComparison.OrdinalIgnoreCase))
            return true;
        return Is(selectedId)
               && string.Equals(queriedId, FallbackCrestId, StringComparison.OrdinalIgnoreCase);
    }

    public static void AddCatalogEntry(List<CrestInfo> crests)
    {
        if (crests.Any(c => Is(c.Id)))
            return;

        var wanderer = crests.FirstOrDefault(c =>
            string.Equals(c.Id, FallbackCrestId, StringComparison.OrdinalIgnoreCase));
        if (wanderer == null)
        {
            Plugin.Log.LogWarning("death-god module unavailable: Wanderer crest was not found.");
            return;
        }

        crests.Add(new CrestInfo
        {
            Id = Id,
            Name = DisplayName,
            Description = Description,
            SlotCount = wanderer.SlotCount,
            HeroConfig = wanderer.HeroConfig,
            Crest = wanderer.Crest,
        });
    }

    public static string Summary(CharmPart part)
        => part == CharmPart.DownSlashJump
            ? "漫游者下劈跳动作；攻击特效与判定范围同形放大 2 倍，伤害不变"
            : "";

    public static bool UsesDoubleDownSlash(CharmPart part, string? crestId)
        => part == CharmPart.DownSlashJump && Is(crestId);

    public static void ScaleDownSlashClone(GameObject clone)
    {
        if (clone == null || clone.transform.Find(ScaleMarkerName) != null)
            return;

        var scale = clone.transform.localScale;
        clone.transform.localScale = new Vector3(
            scale.x * DownSlashScale,
            scale.y * DownSlashScale,
            scale.z);

        // A child marker makes the operation idempotent even when the same
        // source object backs multiple ConfigGroup fields.
        var marker = new GameObject(ScaleMarkerName);
        marker.hideFlags = HideFlags.HideAndDontSave;
        marker.transform.SetParent(clone.transform, false);
    }
}
