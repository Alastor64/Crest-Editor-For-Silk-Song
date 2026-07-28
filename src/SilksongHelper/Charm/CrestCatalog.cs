using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

public sealed class CrestInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int SlotCount { get; set; }
    public object? HeroConfig { get; set; }
    public ToolCrest? Crest { get; set; }
}

public sealed class CrestPartOption
{
    public string CrestId { get; set; } = "";
    public string CrestName { get; set; } = "";
    public CharmPart Part { get; set; }
    public string Summary { get; set; } = "";
    public SpriteAnimation Preview { get; set; } = new(Array.Empty<Texture2D>());
}

public static class CrestCatalog
{
    private static List<CrestInfo>? _crests;
    private static Dictionary<string, CrestInfo>? _byId;
    private static Dictionary<CharmPart, List<CrestPartOption>>? _options;

    public static IReadOnlyList<CrestInfo> All => _crests ?? (IReadOnlyList<CrestInfo>)Array.Empty<CrestInfo>();

    public static CrestInfo? ById(string? id)
        => id != null && _byId != null && _byId.TryGetValue(id, out var c) ? c : null;

    public static IReadOnlyList<CrestPartOption> Options(CharmPart part)
        => _options != null && _options.TryGetValue(part, out var o) ? o : (IReadOnlyList<CrestPartOption>)Array.Empty<CrestPartOption>();

    public static void Init()
    {
        _crests = null;
        _byId = null;
        _options = null;
    }

    public static void EnsureLoaded()
    {
        if (_crests != null)
            return;
        var list = TryReadLiveCrests();
        if (list.Count == 0)
            list = BuildFallbackCrests();
        _crests = list;
        _byId = list.Where(c => !string.IsNullOrEmpty(c.Id)).ToDictionary(c => c.Id);
        _options = new Dictionary<CharmPart, List<CrestPartOption>>();
        foreach (var part in CharmPartNames.All)
        {
            var implementations = new List<CrestInfo>();
            foreach (var c in list)
            {
                if (part != CharmPart.Slot
                    && !PartBehaviour.UsesCrestIdentity(part)
                    && c.HeroConfig == null
                    && !UsesDefaultHeroConfig(c.Id))
                    continue;

                int duplicate = implementations.FindIndex(
                    existing => SameImplementation(part, existing, c));
                if (duplicate < 0)
                {
                    implementations.Add(c);
                }
                else if (RepresentativePreference(part, c)
                         > RepresentativePreference(part, implementations[duplicate]))
                {
                    implementations[duplicate] = c;
                }
            }

            _options[part] = implementations.Select(c => BuildOption(c, part)).ToList();
            Plugin.Log.LogInfo(
                $"crest options {part}: {list.Count} crests -> {_options[part].Count} distinct implementations.");
        }
    }

    /// <summary>
    /// Returns true when two crest choices execute the same implementation for
    /// one editor module. This also lets old saves that reference a now-hidden
    /// duplicate remain visibly selected in both editor UIs.
    /// </summary>
    public static bool AreEquivalent(CharmPart part, string? firstId, string? secondId)
    {
        if (string.Equals(firstId, secondId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.IsNullOrEmpty(firstId) || string.IsNullOrEmpty(secondId))
            return false;

        var first = ById(firstId);
        var second = ById(secondId);
        return first != null && second != null && SameImplementation(part, first, second);
    }

    private static bool SameImplementation(CharmPart part, CrestInfo first, CrestInfo second)
    {
        if (part == CharmPart.Slot)
            return string.Equals(first.Id, second.Id, StringComparison.OrdinalIgnoreCase);

        if (PartBehaviour.UsesCrestIdentity(part))
        {
            return string.Equals(
                IdentityImplementationKey(part, first.Id),
                IdentityImplementationKey(part, second.Id),
                StringComparison.Ordinal);
        }

        // The three Hunter assets deliberately have no HeroConfig and all fall
        // back to HeroController's same default ConfigGroup.
        if (UsesDefaultHeroConfig(first.Id) && UsesDefaultHeroConfig(second.Id))
            return true;

        // Cursed and Witch share the exact Whip HeroConfig. More generally,
        // sharing the same live config also means sharing module fields,
        // animation overrides, and the ConfigGroup attack objects copied by
        // CharmApplier.
        return first.HeroConfig != null
               && second.HeroConfig != null
               && ReferenceEquals(first.HeroConfig, second.HeroConfig);
    }

    private static string IdentityImplementationKey(CharmPart part, string id)
    {
        string normalized = id.ToLowerInvariant();
        bool known = IsKnownCrest(normalized);

        switch (part)
        {
            case CharmPart.HealMethod:
                // Only these identities add behaviour beyond the config fields:
                // Witch changes bind cost/silk gain, Spell changes landing, and
                // Warrior uses the safe custom attack-heal implementation.
                if (normalized == "witch"
                    || normalized == "spell"
                    || normalized == "warrior"
                    || normalized == "cloakless")
                    return $"heal:{normalized}";
                return known ? "heal:standard" : $"heal:{normalized}";

            case CharmPart.PostHealEffect:
                if (normalized == "warrior" || normalized == "reaper")
                    return $"post-heal:{normalized}";
                return known ? "post-heal:none" : $"post-heal:{normalized}";

            case CharmPart.SpecialSkill:
                if (IsHunterId(normalized)
                    || normalized == "toolmaster"
                    || normalized == "spell"
                    || normalized == "wanderer")
                    return $"special:{normalized}";
                return known ? "special:none" : $"special:{normalized}";

            default:
                return $"{part}:{normalized}";
        }
    }

    private static bool IsKnownCrest(string normalized)
        => normalized == "cloakless"
           || normalized == "cursed"
           || normalized == "curse"
           || IsHunterId(normalized)
           || normalized == "reaper"
           || normalized == "spell"
           || normalized == "toolmaster"
           || normalized == "wanderer"
           || normalized == "warrior"
           || normalized == "witch";

    private static bool IsHunterId(string normalized)
        => normalized == "hunter" || normalized.StartsWith("hunter_", StringComparison.Ordinal);

    private static int RepresentativePreference(CharmPart part, CrestInfo crest)
    {
        string id = crest.Id.ToLowerInvariant();

        if (part == CharmPart.HealMethod && id == "wanderer")
            return 100;
        if (part == CharmPart.PostHealEffect && id == "wanderer")
            return 100;
        if (part == CharmPart.SpecialSkill && id == "witch")
            return 100;

        if (!PartBehaviour.UsesCrestIdentity(part))
        {
            if (id == "witch" || id == "hunter")
                return 100;
            if (id == "cursed" || id == "curse" || IsHunterId(id))
                return -100;
        }

        return 0;
    }

    private static CrestPartOption BuildOption(CrestInfo crest, CharmPart part)
    {
        float hue = HueForId(crest.Id);
        var preview = new SpriteAnimation(ProceduralTextures.Build(part, hue));
        string summary = part == CharmPart.Slot
            ? $"{crest.SlotCount} 个槽位"
            : Summarize(crest.HeroConfig, part);
        return new CrestPartOption
        {
            CrestId = crest.Id,
            CrestName = OptionDisplayName(crest),
            Part = part,
            Summary = summary,
            Preview = preview,
        };
    }

    private static string OptionDisplayName(CrestInfo crest)
    {
        if (crest.Id.Equals("Hunter_v2", StringComparison.OrdinalIgnoreCase))
            return $"{crest.Name}（二阶）";
        if (crest.Id.Equals("Hunter_v3", StringComparison.OrdinalIgnoreCase))
            return $"{crest.Name}（三阶）";
        return crest.Name;
    }

    private static string Summarize(object? config, CharmPart part)
    {
        if (config == null)
            return "（占位）";
        var parts = new List<string>();
        foreach (var fn in PartFields.For(part))
        {
            object? v = null;
            var fi = AccessTools.Field(config.GetType(), fn);
            if (fi != null) v = fi.GetValue(config);
            else
            {
                var pi = AccessTools.Property(config.GetType(), fn);
                if (pi != null && pi.CanRead) v = pi.GetValue(config);
            }
            parts.Add($"{Short(fn)}={SafeGet(v)}");
        }
        return parts.Count == 0 ? "（无字段）" : string.Join(" ", parts);
    }

    private static string Short(string fn) => fn.Length <= 14 ? fn : fn.Substring(0, 12) + "..";

    private static string SafeGet(object? v)
    {
        if (v == null) return "null";
        if (v is bool b) return b ? "是" : "否";
        if (v is float f) return f.ToString("0.##");
        if (v is Enum e) return e.ToString();
        return v.ToString() ?? "";
    }

    private static float HueForId(string id)
    {
        int h = 0;
        foreach (var ch in id ?? "") h = h * 31 + ch;
        return (Math.Abs(h) % 1000) / 1000f;
    }

    private static List<CrestInfo> TryReadLiveCrests()
    {
        var list = new List<CrestInfo>();
        try
        {
            var gpType = AccessTools.TypeByName("GlobalSettings.Gameplay") ?? AccessTools.TypeByName("Gameplay");
            if (gpType == null) return list;
            object? gp = TrySingleton(gpType);
            if (gp is UnityEngine.Object u && u == null) gp = null;
            if (gp == null) return list;

            foreach (var f in gpType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.FieldType.Name != "ToolCrest") continue;
                var crest = f.GetValue(gp);
                if (crest is UnityEngine.Object cu && cu == null) continue;
                if (crest == null) continue;
                var info = ReadCrest(crest);
                if (info != null) list.Add(info);
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"read live crests failed: {e}"); }
        return list;
    }

    private static CrestInfo? ReadCrest(object crest)
    {
        var t = crest.GetType();
        string id = (string?)AccessTools.Property(t, "name")?.GetValue(crest, null) ?? "";
        if (string.IsNullOrEmpty(id)) id = (string?)AccessTools.Field(t, "nameCache")?.GetValue(crest) ?? "";
        string? name = ResolveLocalised(AccessTools.Property(t, "DisplayName")?.GetValue(crest, null));
        name = CorrectKnownDisplayName(id, name);
        if (string.IsNullOrEmpty(name)) name = id;
        string? desc = ResolveLocalised(AccessTools.Property(t, "Description")?.GetValue(crest, null));
        var slots = AccessTools.Property(t, "Slots")?.GetValue(crest, null) as Array;
        int slotCount = slots?.Length ?? 0;
        var heroConfig = AccessTools.Property(t, "HeroConfig")?.GetValue(crest, null);
        if (heroConfig is UnityEngine.Object ho && ho == null) heroConfig = null;
        return new CrestInfo
        {
            Id = id,
            Name = name ?? id,
            Description = desc ?? "",
            SlotCount = slotCount,
            HeroConfig = heroConfig,
            Crest = crest as ToolCrest,
        };
    }

    internal static bool UsesDefaultHeroConfig(string? id)
        => id != null
           && (id.Equals("Hunter", StringComparison.OrdinalIgnoreCase)
               || id.StartsWith("Hunter_", StringComparison.OrdinalIgnoreCase));

    private static string? CorrectKnownDisplayName(string id, string? resolved)
    {
        // These two internal crests intentionally use incomplete localisation
        // keys in the current game data. Do not expose the localisation
        // system's !!/...!! missing-key markers in the Chinese editor.
        if (id.Equals("Cursed", StringComparison.OrdinalIgnoreCase)
            || id.Equals("Curse", StringComparison.OrdinalIgnoreCase)
            || string.Equals(resolved, "!!/Tools/CREST_CURSE_NAME!!", StringComparison.Ordinal))
            return "诅咒";

        if (id.Equals("Cloakless", StringComparison.OrdinalIgnoreCase)
            || string.Equals(resolved, "!!/!!", StringComparison.Ordinal))
            return "无装";

        return resolved;
    }

    private static string? ResolveLocalised(object? ls)
    {
        if (ls == null) return "";
        try
        {
            var t = ls.GetType();
            var implicitOp = t.GetMethod("op_Implicit", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (implicitOp != null && implicitOp.ReturnType == typeof(string))
            {
                var s = implicitOp.Invoke(null, new[] { ls }) as string;
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            var ts = ls.ToString();
            if (!string.IsNullOrWhiteSpace(ts) && !ts.Contains(":")) return ts;
        }
        catch { }
        return "";
    }

    private static object? TrySingleton(Type t)
    {
        var names = new[] { "Instance", "instance", "_instance" };
        for (Type? cur = t; cur != null; cur = cur.BaseType)
        {
            foreach (var n in names)
            {
                try
                {
                    var p = cur.GetProperty(n, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
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
                    var f = cur.GetField(n, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null && f.IsStatic)
                    {
                        var v = f.GetValue(null);
                        if (v is UnityEngine.Object u2 && u2 == null) continue;
                        if (v != null) return v;
                    }
                }
                catch { }
            }
        }
        return null;
    }

    private static List<CrestInfo> BuildFallbackCrests()
    {
        Plugin.Log.LogInfo("crests not loaded yet; using placeholder data.");
        var cfgType = AccessTools.TypeByName("HeroControllerConfig");
        var data = new[]
        {
            ("wanderer", "漫游者纹章", 3, new (string, object)[] { ("canBind", true), ("canNailCharge", false), ("downSlashType", 1), ("upSlashType", 1) }),
            ("warrior", "野兽纹章", 4, new (string, object)[] { ("canBind", true), ("canNailCharge", true), ("downSlashType", 2), ("upSlashType", 2) }),
            ("reaper", "收割者纹章", 3, new (string, object)[] { ("canBrolly", true), ("canHarpoonDash", true), ("downSlashType", 1), ("upSlashType", 1) }),
            ("hunter", "猎手纹章", 3, new (string, object)[] { ("canPlayNeedolin", true), ("canNailCharge", false), ("downSlashType", 0), ("upSlashType", 0) }),
            ("witch", "巫女纹章", 3, new (string, object)[] { ("canBind", false), ("canBrolly", true), ("canHarpoonDash", false), ("upSlashType", 1) }),
            ("toolmaster", "工匠纹章", 4, new (string, object)[] { ("canNailCharge", true), ("canPlayNeedolin", true), ("downSlashType", 2), ("upSlashType", 2) }),
        };
        var list = new List<CrestInfo>();
        foreach (var (id, name, slots, overrides) in data)
        {
            list.Add(new CrestInfo
            {
                Id = id,
                Name = name,
                Description = "（占位纹章，游戏数据未加载时显示）",
                SlotCount = slots,
                HeroConfig = CreateFallbackConfig(cfgType, overrides),
            });
        }
        return list;
    }

    private static object? CreateFallbackConfig(Type? cfgType, params (string field, object value)[] overrides)
    {
        if (cfgType == null) return null;
        try
        {
            var so = AccessTools.TypeByName("UnityEngine.ScriptableObject");
            var inst = so?.GetMethod("CreateInstance", new[] { typeof(Type) })?.Invoke(null, new object[] { cfgType });
            if (inst == null) return null;
            foreach (var (fn, v) in overrides)
            {
                var fi = AccessTools.Field(cfgType, fn);
                if (fi != null) fi.SetValue(inst, v);
            }
            return inst;
        }
        catch { return null; }
    }
}
