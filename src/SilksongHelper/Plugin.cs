using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using UnityEngine;

namespace SilksongHelper;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.silksong.helper";
    public const string PluginName = "Silksong Helper";
    public const string PluginVersion = "0.3.0";

    internal static ManualLogSource Log = null!;
    internal static CharmApplier Applier = null!;
    internal static CharmSaveData SaveData = null!;
    internal static ConfigEntry<KeyCode> ToggleKey = null!;

    private Harmony? _harmony;

    private void Awake()
    {
        Log = Logger;
        ToggleKey = Config.Bind("Editor", "ToggleKey", KeyCode.F2, "打开/关闭自定义纹章编辑器的按键。");

        CrestCatalog.Init();
        SaveData = CharmSaveData.Load();
        Applier = new CharmApplier();

        gameObject.AddComponent<CharmEditor>();
        gameObject.AddComponent<CharmWorkshopUI>();

        _harmony = new Harmony(PluginGuid);
        try { _harmony.PatchAll(); }
        catch (Exception e) { Log.LogError($"Harmony PatchAll failed: {e}"); }

        Log.LogInfo($"{PluginName} {PluginVersion} 已就绪。按 {ToggleKey.Value} 打开编辑器。");
    }

    private void OnDestroy()
    {
        Applier?.RestoreOverrides();
        _harmony?.UnpatchSelf();
    }
}
