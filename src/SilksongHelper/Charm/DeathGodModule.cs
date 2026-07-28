using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// Built-in editor source requested by 死神模块选项.md. It is intentionally
/// available only for the down-slash module. The vanilla Wanderer supplies the
/// action/config/object baseline, while a separate renderer-only placeholder
/// doubles the visible effect without changing the attack collider.
/// </summary>
internal static class DeathGodModule
{
    public const string Id = "SilksongHelper_DeathGod";
    public const string FallbackCrestId = "Wanderer";
    public const string DisplayName = "死神·下劈跳";
    public const string Description = "动作与漫游者相同，攻击特效使用 2 倍纯色占位表现。";

    private const string EffectObjectName = "SilksongHelper_DeathGodDownSlashEffect";
    private static Sprite? _effectSprite;

    public static bool Is(string? crestId)
        => string.Equals(crestId, Id, StringComparison.OrdinalIgnoreCase);

    public static string? RuntimeSourceId(string? crestId)
        => Is(crestId) ? FallbackCrestId : crestId;

    public static bool IsSelectableFor(CharmPart part)
        => part == CharmPart.DownSlashJump;

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
        => part == CharmPart.DownSlashJump
            ? "漫游者下劈跳动作；攻击特效大小 2 倍"
            : "";

    public static bool UsesDoubleDownSlashEffect(CharmPart part, string? crestId)
        => part == CharmPart.DownSlashJump && Is(crestId);

    public static void DecorateDownSlashClone(GameObject clone)
    {
        if (clone == null || clone.transform.Find(EffectObjectName) != null)
            return;

        var sourceRenderers = clone.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer != null)
            .ToArray();
        Bounds bounds;
        if (sourceRenderers.Length > 0)
        {
            bounds = sourceRenderers[0].bounds;
            for (int i = 1; i < sourceRenderers.Length; i++)
                bounds.Encapsulate(sourceRenderers[i].bounds);
        }
        else
        {
            bounds = new Bounds(clone.transform.position, Vector3.one);
        }

        var effect = new GameObject(EffectObjectName, typeof(SpriteRenderer));
        effect.layer = clone.layer;
        effect.transform.SetParent(clone.transform, false);
        effect.transform.localPosition = clone.transform.InverseTransformPoint(bounds.center);
        effect.transform.localRotation = Quaternion.identity;

        var lossyScale = clone.transform.lossyScale;
        float localWidth = bounds.size.x / Mathf.Max(Mathf.Abs(lossyScale.x), 0.001f);
        float localHeight = bounds.size.y / Mathf.Max(Mathf.Abs(lossyScale.y), 0.001f);
        effect.transform.localScale = new Vector3(
            Mathf.Max(localWidth * 2f, 1f),
            Mathf.Max(localHeight * 2f, 1f),
            1f);

        var renderer = effect.GetComponent<SpriteRenderer>();
        renderer.sprite = EffectSprite;
        renderer.color = Color.white;

        var nearbyRenderer = sourceRenderers.FirstOrDefault();
        if (nearbyRenderer != null)
        {
            renderer.sortingLayerID = nearbyRenderer.sortingLayerID;
            renderer.sortingOrder = nearbyRenderer.sortingOrder + 1;
        }
        else
        {
            renderer.sortingOrder = 100;
        }
    }

    private static Sprite EffectSprite
    {
        get
        {
            if (_effectSprite != null)
                return _effectSprite;

            const int width = 4;
            const int height = 4;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "SilksongHelper_DeathGodDownSlashEffectTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = Enumerable.Repeat(new Color32(92, 12, 32, 150), width * height).ToArray();
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            _effectSprite = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                4f);
            _effectSprite.name = "SilksongHelper_DeathGodDownSlashEffectSprite";
            _effectSprite.hideFlags = HideFlags.HideAndDontSave;
            return _effectSprite;
        }
    }
}
