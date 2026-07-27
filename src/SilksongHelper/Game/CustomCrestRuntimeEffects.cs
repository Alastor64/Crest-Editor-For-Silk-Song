using System;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// Safe, isolated implementations for crest behaviours whose vanilla state
/// machines require PlayerData.CurrentCrestID to be the real source crest.
/// </summary>
internal static class CustomCrestRuntimeEffects
{
    private const float WarriorHealWindow = 8f;
    private const int WarriorHealCap = 3;

    private static float _warriorHealUntil;
    private static int _warriorHeals;
    private static int _lastAttackCount = int.MinValue;
    private static float _lastHealAt = float.MinValue;

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.BindCompleted))]
    internal static class BindCompletedPatch
    {
        internal static void Postfix()
        {
            if (Plugin.Applier?.ActiveCharm == null
                || !Plugin.Applier.UsesCrestFor(CharmPart.HealMethod, "Warrior"))
            {
                ResetWarriorHeal();
                return;
            }

            _warriorHealUntil = Time.time + WarriorHealWindow;
            _warriorHeals = 0;
            _lastAttackCount = int.MinValue;
            _lastHealAt = float.MinValue;
            Plugin.Log.LogInfo("custom Warrior heal window started.");
        }
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.NailHitEnemy))]
    internal static class NailHitEnemyPatch
    {
        internal static void Postfix(HeroController __instance)
        {
            if (Plugin.Applier?.ActiveCharm == null
                || !Plugin.Applier.UsesCrestFor(CharmPart.HealMethod, "Warrior")
                || Time.time > _warriorHealUntil
                || _warriorHeals >= WarriorHealCap)
                return;

            var playerData = PlayerData.instance;
            if (playerData == null || playerData.health >= playerData.CurrentMaxHealth)
                return;

            int attackCount = ReadAttackCount(__instance);
            if (attackCount != int.MinValue)
            {
                if (attackCount == _lastAttackCount) return;
                _lastAttackCount = attackCount;
            }
            else if (Time.time - _lastHealAt < 0.12f)
            {
                return;
            }

            var addHealth = AccessTools.Method(
                typeof(HeroController), "AddHealth", new[] { typeof(int) });
            if (addHealth == null) return;

            try
            {
                addHealth.Invoke(__instance, new object[] { 1 });
                _warriorHeals++;
                _lastHealAt = Time.time;
                Plugin.Log.LogInfo(
                    $"custom Warrior attack heal {_warriorHeals}/{WarriorHealCap}.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"custom Warrior heal failed: {e.Message}");
                ResetWarriorHeal();
            }
        }
    }

    private static int ReadAttackCount(HeroController hero)
    {
        try
        {
            var states = AccessTools.Field(typeof(HeroController), "cState")?.GetValue(hero);
            if (states == null) return int.MinValue;
            return AccessTools.Field(states.GetType(), "attackCount")?.GetValue(states) is int value
                ? value
                : int.MinValue;
        }
        catch
        {
            return int.MinValue;
        }
    }

    private static void ResetWarriorHeal()
    {
        _warriorHealUntil = 0f;
        _warriorHeals = 0;
        _lastAttackCount = int.MinValue;
        _lastHealAt = float.MinValue;
    }
}
