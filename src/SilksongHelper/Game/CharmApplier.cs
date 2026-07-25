using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

public sealed class CharmApplier
{
    public string? ActiveCharmId => _activeId;

    private string? _activeId;
    private readonly List<(object target, string field, object? value)> _originals = new();
    private readonly List<GameObject> _activatedRoots = new();

    public void ApplyOverrides(CustomCharm charm, object hero)
    {
        if (_activeId == charm.Id) return;
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
                ActivateRoot(srcGroup);
            }

            if (activeConfig != null)
            {
                applied += CopyFields(activeConfig, srcCfg, PartFields.For(part));
                applied += CopyAnimLib(activeConfig, srcCfg);
            }
        }

        _activeId = charm.Id;
        RefreshAnimation(hero, activeConfig);
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
        if (_activeId == null && _originals.Count == 0 && _activatedRoots.Count == 0) return;
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
        foreach (var go in _activatedRoots)
        {
            try { if (go != null) go.SetActive(false); } catch { }
        }
        _activatedRoots.Clear();
        _activeId = null;
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

    private int CopyAnimLib(object targetConfig, object sourceConfig)
    {
        const string field = "heroAnimOverrideLib";
        var fi = AccessTools.Field(targetConfig.GetType(), field);
        if (fi == null) return 0;
        var srcLib = fi.GetValue(sourceConfig);
        if (srcLib == null) return 0;
        if (!_originals.Exists(o => ReferenceEquals(o.target, targetConfig) && o.field == field))
            _originals.Add((targetConfig, field, fi.GetValue(targetConfig)));
        try { fi.SetValue(targetConfig, srcLib); return 1; }
        catch (Exception e) { Plugin.Log.LogWarning($"override anim lib: {e.Message}"); return 0; }
    }

    private void ActivateRoot(object srcGroup)
    {
        try
        {
            var go = AccessTools.Field(srcGroup.GetType(), "ActiveRoot")?.GetValue(srcGroup) as GameObject;
            if (go == null) return;
            if (_activatedRoots.Contains(go)) return;
            go.SetActive(true);
            _activatedRoots.Add(go);
        }
        catch (Exception e) { Plugin.Log.LogWarning($"activate root: {e.Message}"); }
    }

    private static void RefreshAnimation(object hero, object? activeConfig)
    {
        if (activeConfig == null) return;
        try
        {
            var animCtrl = AccessTools.Field(hero.GetType(), "animCtrl")?.GetValue(hero);
            if (animCtrl == null) return;
            var mi = AccessTools.Method(animCtrl.GetType(), "SetHeroControllerConfig", new[] { activeConfig.GetType() });
            mi?.Invoke(animCtrl, new object?[] { activeConfig });
        }
        catch (Exception e) { Plugin.Log.LogWarning($"refresh animation: {e.Message}"); }
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
