using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

public sealed class CharmApplier
{
    public string? ActiveCharmId => _activeId;
    public CustomCharm? ActiveCharm => _activeCharm;

    private string? _activeId;
    private CustomCharm? _activeCharm;
    private readonly List<(object target, string field, object? value)> _originals = new();

    public void ApplyOverrides(CustomCharm charm, object hero)
    {
        // ResetAllCrestState can run more than once while the same crest remains
        // equipped. Always rebuild the composition after vanilla resets it.
        RestoreOverrides();

        var active = AccessTools.Property(hero.GetType(), "CurrentConfigGroup")?.GetValue(hero);
        if (active == null)
        {
            Plugin.Log.LogWarning("CurrentConfigGroup not found; cannot apply overrides.");
            return;
        }
        var activeConfig = GetMember(active, "Config");

        var groups = new List<object>();
        foreach (var fname in new[] { "configs", "specialConfigs" })
            if (AccessTools.Field(hero.GetType(), fname)?.GetValue(hero) is Array arr)
                foreach (var g in arr) groups.Add(g);

        int applied = 0;
        foreach (var part in CharmPartNames.NonSlotParts)
        {
            if (!charm.PartCrestIds.TryGetValue(part.ToString(), out var crestId))
                continue;
            var srcCfg = ResolveHeroConfig(crestId);
            if (srcCfg == null)
            {
                Plugin.Log.LogWarning($"part {part}: source '{crestId}' HeroConfig not found.");
                continue;
            }

            var srcGroup = groups.FirstOrDefault(g => ReferenceEquals(GetMember(g, "Config"), srcCfg));
            if (srcGroup != null)
            {
                applied += CopyFields(active, srcGroup, PartGroupFields.For(part));
            }

            if (activeConfig != null)
                applied += CopyFields(activeConfig, srcCfg, PartFields.For(part));
        }

        _activeId = charm.Id;
        _activeCharm = charm;
        RefreshConfigGroup(hero, active);
        Plugin.Log.LogInfo($"applied custom charm overrides '{charm.Name}' ({applied} fields).");
    }

    public void ReapplyNow(CustomCharm charm)
    {
        if (ActiveCharmId != charm.Id) return;
        _activeId = null;
        var hero = ResolveHero();
        if (hero != null) ApplyOverrides(charm, hero);
    }

    public void RestoreOverrides()
    {
        if (_activeId == null && _originals.Count == 0) return;
        foreach (var (target, field, value) in _originals)
        {
            try
            {
                var fi = AccessTools.Field(target.GetType(), field);
                if (fi != null) fi.SetValue(target, value);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"restore {field}: {e.Message}"); }
        }
        _originals.Clear();
        _activeId = null;
        _activeCharm = null;
    }

    private int CopyFields(object target, object source, IReadOnlyList<string> names)
    {
        int n = 0;
        foreach (var fn in names)
        {
            var fi = AccessTools.Field(target.GetType(), fn);
            if (fi == null) continue;
            if (!_originals.Exists(o => ReferenceEquals(o.target, target) && o.field == fn))
                _originals.Add((target, fn, fi.GetValue(target)));
            try { fi.SetValue(target, fi.GetValue(source)); n++; }
            catch (Exception e) { Plugin.Log.LogWarning($"override {fn}: {e.Message}"); }
        }
        return n;
    }

    public string? SelectedCrestId(CharmPart part)
    {
        if (_activeCharm == null) return null;
        return part == CharmPart.Slot
            ? _activeCharm.SlotCrestId
            : _activeCharm.PartCrestIds.TryGetValue(part.ToString(), out var id) ? id : null;
    }

    public bool UsesCrestFor(CharmPart part, string? crestId)
    {
        if (string.IsNullOrEmpty(crestId)) return false;
        return string.Equals(SelectedCrestId(part), crestId, StringComparison.OrdinalIgnoreCase);
    }

    private static void RefreshConfigGroup(object hero, object activeGroup)
    {
        try
        {
            // SetConfigGroup also refreshes HeroController's cached slash,
            // downspike and damager references. Merely changing ConfigGroup
            // fields leaves those caches pointing at the original crest.
            var groupType = activeGroup.GetType();
            var mi = AccessTools.Method(hero.GetType(), "SetConfigGroup",
                new[] { groupType, groupType });
            mi?.Invoke(hero, new object?[] { activeGroup, null });
        }
        catch (Exception e) { Plugin.Log.LogWarning($"refresh config group: {e.Message}"); }
    }

    private static object? ResolveHero()
    {
        var t = AccessTools.TypeByName("HeroController");
        if (t == null) return null;
        foreach (var n in new[] { "instance", "Instance" })
        {
            try
            {
                var p = AccessTools.Property(t, n);
                if (p != null && p.GetGetMethod(true) != null)
                {
                    var v = p.GetValue(null, null);
                    if (v is UnityEngine.Object u && u == null) continue;
                    if (v != null) return v;
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
                    if (v != null) return v;
                }
            }
            catch { }
        }
        return null;
    }

    private static object? ResolveHeroConfig(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try
        {
            var mi = AccessTools.Method(typeof(ToolItemManager), nameof(ToolItemManager.GetCrestByName));
            if (mi?.Invoke(null, new object?[] { id }) is ToolCrest c)
            {
                var hc = GetMember(c, "HeroConfig");
                if (hc != null) return hc;
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"GetCrestByName '{id}': {e.Message}"); }
        return CrestCatalog.ById(id)?.HeroConfig;
    }

    private static object? GetMember(object obj, string name)
    {
        var t = obj.GetType();
        var p = AccessTools.Property(t, name);
        if (p != null && p.CanRead) return p.GetValue(obj, null);
        var f = AccessTools.Field(t, name);
        return f?.GetValue(obj);
    }
}
