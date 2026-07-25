using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

public static class CustomCrestRegistry
{
    public const string Prefix = "__silksong_custom__";

    private static readonly Dictionary<string, ToolCrest> _byName = new();
    private static bool _dirty = true;

    public static void MarkDirty() => _dirty = true;

    public static string SentinelFor(string charmId) => Prefix + charmId;

    public static bool IsSentinel(string? name) => name != null && name.StartsWith(Prefix, StringComparison.Ordinal);

    public static string? IdFromSentinel(string? name)
        => IsSentinel(name) ? name!.Substring(Prefix.Length) : null;

    public static void EnsureBuilt()
    {
        if (!_dirty) return;
        _dirty = false;
        foreach (var obj in _byName.Values)
        {
            try { if (obj != null) UnityEngine.Object.Destroy(obj); } catch { }
        }
        _byName.Clear();

        foreach (var charm in Plugin.SaveData.Charms)
        {
            var src = ResolveSlotCrest(charm.SlotCrestId);
            if (src is not ToolCrest crest) continue;
            try
            {
                var clone = UnityEngine.Object.Instantiate(crest);
                clone.name = SentinelFor(charm.Id);
                _byName[clone.name] = clone;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"build synthetic crest for '{charm.Name}': {e}"); }
        }
    }

    private static ToolCrest? ResolveSlotCrest(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try
        {
            var mi = AccessTools.Method(typeof(ToolItemManager), nameof(ToolItemManager.GetCrestByName));
            if (mi != null && mi.Invoke(null, new object?[] { id }) is ToolCrest live)
                return live;
        }
        catch (Exception e) { Plugin.Log.LogWarning($"GetCrestByName '{id}': {e.Message}"); }
        return CrestCatalog.ById(id)?.Crest;
    }

    public static ToolCrest? Get(string? name)
        => IsSentinel(name) && _byName.TryGetValue(name!, out var c) ? c : null;

    public static IReadOnlyCollection<ToolCrest> All => _byName.Values;

    public static string? CustomNameFor(ToolCrest? crestData)
    {
        if (crestData == null) return null;
        var id = IdFromSentinel(crestData.name);
        if (id == null) return null;
        return Plugin.SaveData.Charms.FirstOrDefault(c => c.Id == id)?.Name;
    }
}
