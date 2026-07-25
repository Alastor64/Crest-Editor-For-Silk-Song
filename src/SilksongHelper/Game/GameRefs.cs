using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

public sealed class GameRefs
{
    public static readonly GameRefs Instance = new GameRefs();

    private readonly Dictionary<string, (Type type, object inst)> _cache = new Dictionary<string, (Type, object)>();

    public void Refresh() => _cache.Clear();

    public bool HasField(string typeName, string field)
        => TryResolve(typeName, out var t, out _) && AccessTools.Field(t, field) != null;

    public object? Get(string typeName, string field)
    {
        if (!TryResolve(typeName, out var t, out var inst))
            return null;
        var fi = AccessTools.Field(t, field);
        if (fi == null)
            return null;
        try { return fi.GetValue(inst); }
        catch { return null; }
    }

    public bool Set(string typeName, string field, object value)
    {
        if (!TryResolve(typeName, out var t, out var inst))
            return false;
        var fi = AccessTools.Field(t, field);
        if (fi == null)
            return false;
        try { fi.SetValue(inst, value); return true; }
        catch (Exception e) { Plugin.Log.LogWarning($"Set {typeName}.{field} failed: {e.Message}"); return false; }
    }

    public bool Call(string typeName, string method, params object[] args)
    {
        if (!TryResolve(typeName, out var t, out var inst))
            return false;
        var types = new Type[args.Length];
        for (int i = 0; i < args.Length; i++)
            types[i] = args[i]?.GetType() ?? typeof(object);
        var mi = AccessTools.Method(t, method, types);
        if (mi == null)
            return false;
        try { mi.Invoke(inst, args); return true; }
        catch (Exception e) { Plugin.Log.LogWarning($"Call {typeName}.{method} failed: {e.Message}"); return false; }
    }

    private bool TryResolve(string typeName, out Type type, out object inst)
    {
        type = null!;
        inst = null!;
        if (_cache.TryGetValue(typeName, out var c) && IsAlive(c.inst))
        {
            type = c.type;
            inst = c.inst;
            return true;
        }

        var t = AccessTools.TypeByName(typeName);
        if (t == null)
            return false;

        object? instance = TrySingleton(t);
        if (instance is UnityEngine.Object uo && uo == null)
            instance = null;
        if (instance == null)
        {
            try { instance = UnityEngine.Object.FindObjectOfType(t); }
            catch { }
        }
        if (instance == null)
            return false;

        _cache[typeName] = (t, instance);
        type = t;
        inst = instance;
        return true;
    }

    private static bool IsAlive(object o)
    {
        if (o is UnityEngine.Object u)
            return u != null;
        return o != null;
    }

    private static object? TrySingleton(Type t)
    {
        foreach (var name in new[] { "Instance", "instance", "current", "Current" })
        {
            try
            {
                var prop = AccessTools.Property(t, name);
                if (prop != null && prop.GetGetMethod(nonPublic: true) != null)
                {
                    var v = prop.GetValue(null, null);
                    if (v is UnityEngine.Object u && u == null) continue;
                    if (v != null) return v;
                }
            }
            catch { }
            try
            {
                var field = AccessTools.Field(t, name);
                if (field != null && field.IsStatic)
                {
                    var v = field.GetValue(null);
                    if (v is UnityEngine.Object u2 && u2 == null) continue;
                    if (v != null) return v;
                }
            }
            catch { }
        }
        return null;
    }
}
