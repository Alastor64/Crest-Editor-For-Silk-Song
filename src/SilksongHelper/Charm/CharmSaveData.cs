using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace SilksongHelper;

public sealed class CharmSaveData
{
    public List<CustomCharm> Charms { get; set; } = new List<CustomCharm>();

    private static string Dir => Directory.CreateDirectory(Path.Combine(Paths.ConfigPath, "SilksongHelper")).FullName;
    private static string FilePath => Path.Combine(Dir, "charms.json");

    public void Upsert(CustomCharm charm)
    {
        // The editor keeps mutating its working copy after Save is pressed.
        // Persist a snapshot so unsaved edits cannot leak into the definition
        // that the inventory will equip later.
        var snapshot = charm.Clone();
        int i = Charms.FindIndex(c => c.Id == charm.Id);
        if (i >= 0) Charms[i] = snapshot;
        else Charms.Add(snapshot);
    }

    public void Delete(string id) => Charms.RemoveAll(c => c.Id == id);

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented)); }
        catch (Exception e) { Plugin.Log.LogWarning($"save failed: {e.Message}"); }
    }

    public static CharmSaveData Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new CharmSaveData();
            return JsonConvert.DeserializeObject<CharmSaveData>(File.ReadAllText(FilePath)) ?? new CharmSaveData();
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"load failed: {e.Message}");
            return new CharmSaveData();
        }
    }
}
