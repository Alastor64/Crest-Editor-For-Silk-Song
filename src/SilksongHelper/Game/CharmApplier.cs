using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

public sealed class CharmApplier
{
    public string? ActiveCharmId => _activeId;
    public CustomCharm? ActiveCharm => _activeCharm;

    private string? _activeId;
    private CustomCharm? _activeCharm;
    private bool _isApplying;
    private readonly List<(object target, string field, object? value)> _originals = new();
    private readonly Dictionary<GameObject, GameObject> _attackObjectClones = new();
    private readonly HashSet<string> _diagnosedCharmIds = new();

    public void ApplyOverrides(CustomCharm charm, object hero)
    {
        if (_isApplying) return;

        _isApplying = true;
        try { ApplyOverridesCore(charm, hero); }
        finally { _isApplying = false; }
    }

    private void ApplyOverridesCore(CustomCharm charm, object hero)
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

        DumpCompositionDiagnostics(charm, groups);

        int applied = 0;
        foreach (var part in CharmPartNames.NonSlotParts)
        {
            if (!charm.PartCrestIds.TryGetValue(part.ToString(), out var crestId))
                continue;
            var srcCfg = ResolveHeroConfig(crestId);
            if (srcCfg == null && CrestCatalog.UsesDefaultHeroConfig(crestId))
                srcCfg = groups.Count > 0 ? GetMember(groups[0], "Config") : null;
            if (srcCfg == null)
            {
                Plugin.Log.LogWarning($"part {part}: source '{crestId}' HeroConfig not found.");
                continue;
            }

            var srcGroup = groups.FirstOrDefault(g => ReferenceEquals(GetMember(g, "Config"), srcCfg));
            if (srcGroup != null)
            {
                applied += CopyFields(active, srcGroup, PartGroupFields.For(part));
                applied += CopyAttackObjects(hero, active, srcGroup, part, crestId);
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
        foreach (var clone in _attackObjectClones.Values)
        {
            try
            {
                if (clone != null)
                    UnityEngine.Object.Destroy(clone);
            }
            catch { }
        }
        _attackObjectClones.Clear();
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

    private int CopyAttackObjects(
        object hero,
        object targetGroup,
        object sourceGroup,
        CharmPart part,
        string sourceCrestId)
    {
        bool useDeathGodBlade = DeathGodModule.UsesBladeVisual(part, sourceCrestId);
        if (ReferenceEquals(targetGroup, sourceGroup) && !useDeathGodBlade)
            return 0;

        var targetRoot = GetMember(targetGroup, "ActiveRoot") as GameObject;
        var parent = targetRoot != null
            ? targetRoot.transform
            : (hero as Component)?.transform;
        if (parent == null)
        {
            Plugin.Log.LogWarning($"part {part}: active attack root not found.");
            return 0;
        }

        int copied = 0;
        foreach (var objectField in PartAttackObjectFields.For(part))
        {
            if (GetMember(sourceGroup, objectField) is not GameObject sourceObject
                || sourceObject == null)
                continue;

            if (!_attackObjectClones.TryGetValue(sourceObject, out var clone)
                || clone == null)
            {
                // Source attack objects can be nested below an inactive crest
                // root. Preserve their world transform while moving the clone
                // below the active crest root.
                clone = UnityEngine.Object.Instantiate(sourceObject, parent, true);
                clone.name = $"SilksongHelper_{part}_{sourceObject.name}";
                clone.SetActive(sourceObject.activeSelf);
                _attackObjectClones[sourceObject] = clone;
                Plugin.Log.LogDebug(
                    $"cloned attack object {sourceObject.name} for {CharmPartNames.Display(part)}.");
            }

            if (useDeathGodBlade)
                DeathGodModule.DecorateAttackClone(clone, objectField);

            copied += SetField(targetGroup, objectField, clone);
            copied += BindAttackComponents(targetGroup, objectField, clone);
        }
        return copied;
    }

    private int BindAttackComponents(object targetGroup, string objectField, GameObject clone)
    {
        int copied = 0;
        switch (objectField)
        {
            case "NormalSlashObject":
                copied += SetField(targetGroup, "<NormalSlash>k__BackingField",
                    clone.GetComponent<NailSlash>());
                copied += SetField(targetGroup, "<NormalSlashDamager>k__BackingField",
                    clone.GetComponent<DamageEnemies>());
                break;
            case "AlternateSlashObject":
                copied += SetField(targetGroup, "<AlternateSlash>k__BackingField",
                    clone.GetComponent<NailSlash>());
                copied += SetField(targetGroup, "<AlternateSlashDamager>k__BackingField",
                    clone.GetComponent<DamageEnemies>());
                break;
            case "WallSlashObject":
                copied += SetField(targetGroup, "<WallSlash>k__BackingField",
                    clone.GetComponent<NailSlash>());
                copied += SetField(targetGroup, "<WallSlashDamager>k__BackingField",
                    clone.GetComponent<DamageEnemies>());
                break;
            case "UpSlashObject":
                copied += SetField(targetGroup, "<UpSlash>k__BackingField",
                    clone.GetComponent<NailSlash>());
                copied += SetField(targetGroup, "<UpSlashDamager>k__BackingField",
                    clone.GetComponent<DamageEnemies>());
                break;
            case "AltUpSlashObject":
                copied += SetField(targetGroup, "<AltUpSlash>k__BackingField",
                    clone.GetComponent<NailSlash>());
                copied += SetField(targetGroup, "<AltUpSlashDamager>k__BackingField",
                    clone.GetComponent<DamageEnemies>());
                break;
            case "DownSlashObject":
                copied += SetField(targetGroup, "<DownSlash>k__BackingField",
                    clone.GetComponent<NailSlash>());
                copied += SetField(targetGroup, "<DownSlashDamager>k__BackingField",
                    clone.GetComponent<DamageEnemies>());
                copied += SetField(targetGroup, "<Downspike>k__BackingField",
                    clone.GetComponent<Downspike>());
                break;
            case "AltDownSlashObject":
                copied += SetField(targetGroup, "<AltDownSlash>k__BackingField",
                    clone.GetComponent<NailSlash>());
                copied += SetField(targetGroup, "<AltDownSlashDamager>k__BackingField",
                    clone.GetComponent<DamageEnemies>());
                copied += SetField(targetGroup, "<AltDownspike>k__BackingField",
                    clone.GetComponent<Downspike>());
                break;
        }
        return copied;
    }

    private int SetField(object target, string name, object? value)
    {
        var field = AccessTools.Field(target.GetType(), name);
        if (field == null)
            return 0;
        if (!_originals.Exists(o => ReferenceEquals(o.target, target) && o.field == name))
            _originals.Add((target, name, field.GetValue(target)));
        try
        {
            field.SetValue(target, value);
            return 1;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"override {name}: {e.Message}");
            return 0;
        }
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
        return DeathGodModule.MatchesRuntimeIdentity(SelectedCrestId(part), crestId);
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
        id = DeathGodModule.RuntimeSourceId(id);
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
        var f = AccessTools.Field(t, name);
        if (f != null) return f.GetValue(obj);
        var p = AccessTools.Property(t, name);
        return p != null && p.CanRead ? p.GetValue(obj, null) : null;
    }

    private void DumpCompositionDiagnostics(CustomCharm charm, IReadOnlyList<object> groups)
    {
        if (!_diagnosedCharmIds.Add(charm.Id)) return;

        try
        {
            Plugin.Log.LogInfo($"[CrestDiag] composition '{charm.Name}' id={charm.Id}");
            var selections = new List<(CharmPart part, string id)>();
            if (!string.IsNullOrEmpty(charm.SlotCrestId))
                selections.Add((CharmPart.Slot, charm.SlotCrestId!));
            foreach (var part in CharmPartNames.NonSlotParts)
                if (charm.PartCrestIds.TryGetValue(part.ToString(), out var id))
                    selections.Add((part, id));

            foreach (var crestGroup in selections.GroupBy(item => item.id))
            {
                var cfg = ResolveHeroConfig(crestGroup.Key);
                var partNames = string.Join("/", crestGroup.Select(item => CharmPartNames.Display(item.part)));
                Plugin.Log.LogInfo(
                    $"[CrestDiag] source={crestGroup.Key} parts={partNames} config={cfg?.GetType().Name ?? "null"}");
                if (cfg != null)
                    Plugin.Log.LogInfo($"[CrestDiag] clips {crestGroup.Key}: {ReadAnimationClips(cfg)}");

                foreach (var item in crestGroup.Where(item => item.part != CharmPart.Slot))
                {
                    var sourceGroup = cfg == null
                        ? null
                        : groups.FirstOrDefault(group => ReferenceEquals(GetMember(group, "Config"), cfg));
                    if (sourceGroup == null) continue;
                    var values = PartGroupFields.For(item.part)
                        .Select(field => $"{field}={DescribeMember(GetMember(sourceGroup, field))}");
                    Plugin.Log.LogInfo(
                        $"[CrestDiag] group {CharmPartNames.Display(item.part)}: {string.Join(", ", values)}");
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[CrestDiag] failed: {e}");
        }
    }

    private static string ReadAnimationClips(object config)
    {
        var lib = GetFieldValue(config, "heroAnimOverrideLib");
        if (lib == null) return "(none)";
        if (GetFieldValue(lib, "clips") is not Array clips) return "(clips unavailable)";

        var names = new List<string>(clips.Length);
        foreach (var clip in clips)
        {
            if (clip == null) continue;
            var name = GetFieldValue(clip, "name") as string ?? "?";
            var frames = GetFieldValue(clip, "frames") as Array;
            names.Add($"{name}[{frames?.Length ?? 0}]");
        }
        return string.Join(", ", names);
    }

    private static object? GetFieldValue(object target, string fieldName)
    {
        for (Type? type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field.GetValue(target);
        }
        return null;
    }

    private static string DescribeMember(object? value)
    {
        if (value == null) return "null";
        if (value is UnityEngine.Object unityObject)
            return unityObject == null ? "destroyed" : $"{value.GetType().Name}:{unityObject.name}";
        return value.ToString() ?? value.GetType().Name;
    }
}
