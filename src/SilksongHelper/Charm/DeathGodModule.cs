using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// Built-in editor source requested by 死神模块.md. The vanilla Wanderer crest
/// supplies every unspecified module. Normal attacks additionally receive a
/// long, dark placeholder blade and a modest reach increase.
/// </summary>
internal static class DeathGodModule
{
    public const string Id = "SilksongHelper_DeathGod";
    public const string FallbackCrestId = "Wanderer";
    public const string DisplayName = "死神模块";
    public const string Description =
        "参考 BVN 的一护·虚化，以天锁斩月般的长刃姿态挥舞织针；未说明的能力继承漫游者。";

    private const string BladeObjectName = "SilksongHelper_DeathGodBlade";
    private static Sprite? _bladeSprite;

    public static bool Is(string? crestId)
        => string.Equals(crestId, Id, StringComparison.OrdinalIgnoreCase);

    public static string? RuntimeSourceId(string? crestId)
        => Is(crestId) ? FallbackCrestId : crestId;

    public static bool MatchesRuntimeIdentity(string? selectedId, string? queriedId)
    {
        if (string.IsNullOrEmpty(selectedId) || string.IsNullOrEmpty(queriedId))
            return false;
        if (string.Equals(selectedId, queriedId, StringComparison.OrdinalIgnoreCase))
            return true;
        return Is(selectedId)
               && string.Equals(queriedId, FallbackCrestId, StringComparison.OrdinalIgnoreCase);
    }

    public static void AddCatalogEntry(List<CrestInfo> crests)
    {
        if (crests.Any(c => Is(c.Id)))
            return;

        var wanderer = crests.FirstOrDefault(c =>
            string.Equals(c.Id, FallbackCrestId, StringComparison.OrdinalIgnoreCase));
        if (wanderer == null)
        {
            Plugin.Log.LogWarning("death-god module unavailable: Wanderer crest was not found.");
            return;
        }

        crests.Add(new CrestInfo
        {
            Id = Id,
            Name = DisplayName,
            Description = Description,
            SlotCount = wanderer.SlotCount,
            HeroConfig = wanderer.HeroConfig,
            Crest = wanderer.Crest,
        });
    }

    public static string Summary(CharmPart part)
        => part == CharmPart.NormalAttack
            ? "漫游者基底；黑色长刃占位表现与加长攻击范围"
            : "继承漫游者对应模块";

    public static bool UsesBladeVisual(CharmPart part, string? crestId)
        => part == CharmPart.NormalAttack && Is(crestId);

    public static void DecorateAttackClone(GameObject clone, string objectField)
    {
        if (clone == null || clone.transform.Find(BladeObjectName) != null)
            return;

        var scale = clone.transform.localScale;
        float reachScale = objectField == "WallSlashObject" ? 1.15f : 1.35f;
        clone.transform.localScale = new Vector3(
            scale.x * reachScale,
            scale.y * 0.9f,
            scale.z);

        var blade = new GameObject(BladeObjectName, typeof(SpriteRenderer));
        blade.layer = clone.layer;
        blade.transform.SetParent(clone.transform, false);
        blade.transform.localPosition = new Vector3(0.35f, 0f, -0.02f);
        blade.transform.localRotation = Quaternion.identity;
        blade.transform.localScale = new Vector3(1.7f, 0.55f, 1f);

        var renderer = blade.GetComponent<SpriteRenderer>();
        renderer.sprite = BladeSprite;
        renderer.color = Color.white;

        var nearbyRenderer = clone.GetComponentInChildren<SpriteRenderer>(true);
        if (nearbyRenderer != null && !ReferenceEquals(nearbyRenderer, renderer))
        {
            renderer.sortingLayerID = nearbyRenderer.sortingLayerID;
            renderer.sortingOrder = nearbyRenderer.sortingOrder + 1;
        }
        else
        {
            renderer.sortingOrder = 100;
        }
    }

    private static Sprite BladeSprite
    {
        get
        {
            if (_bladeSprite != null)
                return _bladeSprite;

            const int width = 32;
            const int height = 6;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "SilksongHelper_DeathGodBladeTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = Enumerable.Repeat(new Color32(48, 8, 18, 230), width * height).ToArray();
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            _bladeSprite = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.05f, 0.5f),
                16f);
            _bladeSprite.name = "SilksongHelper_DeathGodBladeSprite";
            _bladeSprite.hideFlags = HideFlags.HideAndDontSave;
            return _bladeSprite;
        }
    }
}
