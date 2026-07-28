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
    private static readonly Dictionary<string, ToolCrestsData.Data> _fallbackSaveData = new();
    private static bool _dirty = true;
    private static bool _building;

    public static void MarkDirty() => _dirty = true;

    public static string SentinelFor(string charmId) => Prefix + charmId;

    public static bool IsSentinel(string? name) => name != null && name.StartsWith(Prefix, StringComparison.Ordinal);

    public static string? IdFromSentinel(string? name)
        => IsSentinel(name) ? name!.Substring(Prefix.Length) : null;

    public static void EnsureBuilt()
    {
        if (!_dirty || _building) return;
        _building = true;
        try
        {
            var liveCrestList = ResolveLiveCrestList();
            foreach (var obj in _byName.Values)
            {
                try
                {
                    if (obj == null) continue;
                    liveCrestList?.Remove(obj);
                    UnityEngine.Object.Destroy(obj);
                }
                catch { }
            }
            _byName.Clear();
            _fallbackSaveData.Clear();

            var definitions = Plugin.SaveData.Charms
                .Select(charm => Plugin.Applier.DefinitionForRegistry(charm))
                .ToList();
            var active = Plugin.Applier.ActiveCharm;
            if (active != null && definitions.All(charm => charm.Id != active.Id))
                definitions.Add(active);

            foreach (var charm in definitions)
            {
                var src = ResolveSlotCrest(charm.SlotCrestId);
                if (src is not ToolCrest crest) continue;
                try
                {
                    var clone = UnityEngine.Object.Instantiate(crest);
                    clone.name = SentinelFor(charm.Id);
                    NormalizeSyntheticCrest(clone);
                    EnsureSaveData(clone, crest);
                    _byName[clone.name] = clone;
                    if (liveCrestList != null && !liveCrestList.Contains(clone))
                        liveCrestList.Add(clone);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"build synthetic crest for '{charm.Name}': {e}");
                }
            }

            _dirty = false;
            Plugin.Log.LogInfo($"custom crest registry rebuilt: {_byName.Count} crest(s).");
        }
        finally
        {
            _building = false;
        }
    }

    private static void NormalizeSyntheticCrest(ToolCrest clone)
    {
        var type = typeof(ToolCrest);
        AccessTools.Field(type, "isHidden")?.SetValue(clone, false);
        AccessTools.Field(type, "previousVersion")?.SetValue(clone, null);
        AccessTools.Field(type, "upgradedVersion")?.SetValue(clone, null);
        AccessTools.Field(type, "oldPreviousVersion")?.SetValue(clone, null);
        AccessTools.Field(type, "hasCustomAction")?.SetValue(clone, false);

        var customButton = AccessTools.Field(type, "customButtonCombo");
        if (customButton != null)
        {
            var empty = customButton.FieldType.IsValueType
                ? Activator.CreateInstance(customButton.FieldType)
                : null;
            customButton.SetValue(clone, empty);
        }
    }

    private static ToolCrestList? ResolveLiveCrestList()
    {
        try
        {
            var manager = ToolItemManager.Instance;
            if (manager == null) return null;
            return AccessTools.Field(typeof(ToolItemManager), "crestList")?.GetValue(manager)
                as ToolCrestList;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"resolve live crest list: {e.Message}");
            return null;
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

    public static ToolCrestsData.Data? SaveDataFor(string? name)
        => IsSentinel(name) && _fallbackSaveData.TryGetValue(name!, out var data) ? data : null;

    public static bool IsToolEquipped(string? crestName, string? toolName)
    {
        if (string.IsNullOrEmpty(toolName)) return false;
        if (!IsSentinel(crestName)) return false;

        ToolCrestsData.Data? data = null;
        try
        {
            var live = PlayerData.instance?.ToolEquips?.GetData(crestName);
            if (live?.Slots != null)
                data = live;
        }
        catch { }

        data ??= SaveDataFor(crestName);
        return data?.Slots != null
               && data.Value.Slots.Any(slot =>
                   string.Equals(slot.EquippedTool, toolName, StringComparison.Ordinal));
    }

    public static string? CustomNameFor(ToolCrest? crestData)
    {
        return CustomCharmFor(crestData)?.Name;
    }

    public static string? CustomDescriptionFor(ToolCrest? crestData)
        => CustomCharmFor(crestData)?.Description;

    private static CustomCharm? CustomCharmFor(ToolCrest? crestData)
    {
        if (crestData == null) return null;
        var id = IdFromSentinel(crestData.name);
        if (id == null) return null;
        return Plugin.SaveData.Charms.FirstOrDefault(c => c.Id == id);
    }

    private static void EnsureSaveData(ToolCrest clone, ToolCrest source)
    {
        var sentinel = clone.name;
        var data = default(ToolCrestsData.Data);
        var hasData = false;
        try
        {
            var equips = PlayerData.instance?.ToolEquips;
            if (equips != null)
            {
                data = equips.GetData(sentinel);
                hasData = data.Slots != null;
            }
        }
        catch { }

        if (!hasData)
        {
            data = new ToolCrestsData.Data
            {
                IsUnlocked = true,
                DisplayNewIndicator = false,
                Slots = CloneSlots(source),
            };
            try { PlayerData.instance?.ToolEquips?.SetData(sentinel, data); }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"create custom crest save data '{sentinel}': {e.Message}");
            }
        }
        else
        {
            data.IsUnlocked = true;
            data.DisplayNewIndicator = false;
            data.Slots ??= CloneSlots(source);
            ResizeSlots(data.Slots, source.Slots?.Length ?? 0);
            SeedMissingEquipsFromSource(data.Slots, source);
        }

        _fallbackSaveData[sentinel] = data;
        Plugin.Log.LogInfo(
            $"custom crest slots '{sentinel}': "
            + string.Join(", ", data.Slots.Select((slot, index) =>
                $"{index}={(string.IsNullOrEmpty(slot.EquippedTool) ? "(empty)" : slot.EquippedTool)}")));
    }

    private static List<ToolCrestsData.SlotData> CloneSlots(ToolCrest source)
    {
        var result = new List<ToolCrestsData.SlotData>();
        var sourceData = default(ToolCrestsData.Data);
        try { sourceData = source.SaveData; } catch { }
        int count = source.Slots?.Length ?? 0;
        for (int i = 0; i < count; i++)
        {
            var sourceSlot = sourceData.Slots != null && i < sourceData.Slots.Count
                ? sourceData.Slots[i]
                : default;
            result.Add(new ToolCrestsData.SlotData
            {
                EquippedTool = sourceData.Slots != null && i < sourceData.Slots.Count
                    ? sourceData.Slots[i].EquippedTool
                    : "",
                IsUnlocked = sourceData.Slots == null ? true : sourceSlot.IsUnlocked,
            });
        }
        return result;
    }

    private static void ResizeSlots(List<ToolCrestsData.SlotData> slots, int count)
    {
        while (slots.Count < count)
            slots.Add(new ToolCrestsData.SlotData { EquippedTool = "", IsUnlocked = true });
        if (slots.Count > count)
            slots.RemoveRange(count, slots.Count - count);
    }

    private static void SeedMissingEquipsFromSource(
        List<ToolCrestsData.SlotData> targetSlots,
        ToolCrest source)
    {
        if (targetSlots.Any(slot => !string.IsNullOrEmpty(slot.EquippedTool)))
            return;

        var sourceData = default(ToolCrestsData.Data);
        try { sourceData = source.SaveData; } catch { }
        if (sourceData.Slots == null) return;

        int count = Math.Min(targetSlots.Count, sourceData.Slots.Count);
        for (int i = 0; i < count; i++)
        {
            var target = targetSlots[i];
            target.EquippedTool = sourceData.Slots[i].EquippedTool;
            target.IsUnlocked = sourceData.Slots[i].IsUnlocked;
            targetSlots[i] = target;
        }
    }
}
