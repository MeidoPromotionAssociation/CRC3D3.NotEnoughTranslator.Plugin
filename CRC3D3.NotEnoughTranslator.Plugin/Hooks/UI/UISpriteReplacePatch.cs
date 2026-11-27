using System;
using System.Collections.Generic;
using CRC3D3.NotEnoughTranslator.Plugin.Hooks.Texture;
using CRC3D3.NotEnoughTranslator.Plugin.Manger;
using CRC3D3.NotEnoughTranslator.Plugin.Translation;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CRC3D3.NotEnoughTranslator.Plugin.Hooks.UI;

/// <summary>
/// 在 Unity 主线程为 uGUI Image 替换或导出单张精灵图。
/// Replace or dump individual sprites for uGUI Images on Unity's main thread.
/// </summary>
internal static class UISpriteReplacePatch
{
    /// <summary>
    /// UI 精灵图补丁的独立 Harmony ID。
    /// The dedicated Harmony ID for UI sprite patches.
    /// </summary>
    public const string HarmonyId =
        "github.meidopromotionassociation.crc3d3.notenoughtranslator.plugin.hooks.ui.uispritereplacepatch";

    private static readonly Dictionary<Sprite, Sprite> Replacements = new();
    private static readonly Dictionary<Sprite, (Sprite Original, Texture2D Texture)> Originals = new();
    private static readonly HashSet<string> ReportedNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> DumpAttempts = new(StringComparer.OrdinalIgnoreCase);
    private static TextureReplacementCatalog _catalog = TextureReplacementCatalog.Empty;
    private static bool _dumpEnabled;
    private static AccessTools.FieldRef<Image, Sprite> _overrideSprite;
    private static Harmony _harmony;

    /// <summary>
    /// 判断精灵是否由 UI 模块生成，供整图纹理补丁识别。
    /// Identify a sprite owned by the UI module for the whole-texture patch.
    /// </summary>
    internal static bool IsReplacement(Sprite sprite) => !ReferenceEquals(sprite, null) && Originals.ContainsKey(sprite);

    /// <summary>
    /// 发布图片索引和导出状态，先还原旧精灵，再处理当前活动的 Image。
    /// Publish the image index and dump state, restoring previous sprites before processing active Images.
    /// </summary>
    internal static void ApplyCatalog(TextureReplacementCatalog catalog)
    {
        var dumpEnabled = NotEnoughTranslator.EnableSpriteDump.Value;
        if (catalog.Count == 0 && !dumpEnabled)
        {
            Unload();
            return;
        }
        if (!Install()) return;

        RestoreOriginals();
        _catalog = catalog;
        _dumpEnabled = dumpEnabled;
        foreach (var image in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
            if (image.isActiveAndEnabled) RefreshImage(image);

        if (catalog.Count != 0)
            LogManager.Info($"[UI/Sprite] Indexed/已索引 {catalog.Count} replacement sprites/替换精灵图");
    }

    /// <summary>
    /// 还原已替换的精灵并卸载本功能的 Hook。
    /// Restore replaced sprites and remove this feature's hooks.
    /// </summary>
    internal static void Unload()
    {
        RestoreOriginals();
        _harmony?.UnpatchSelf();
        _harmony = null;
    }

    /// <summary>
    /// 安装普通与覆盖精灵的写入补丁，以及处理预制体初值的启用补丁。
    /// Hook base and override sprite writes, and Image activation for prefab values.
    /// </summary>
    private static bool Install()
    {
        if (_harmony != null) return true;

        var harmony = new Harmony(HarmonyId);
        try
        {
            // overrideSprite 的 getter 会回退到 sprite，这里需要字段的实际值。
            // The overrideSprite getter falls back to sprite, so use the actual field value.
            _overrideSprite = AccessTools.FieldRefAccess<Image, Sprite>("m_OverrideSprite");
            var prefix = new HarmonyMethod(typeof(UISpriteReplacePatch), nameof(SpritePrefix))
            {
                before = new[] { TextureReplacePatch.HarmonyId }
            };
            harmony.Patch(AccessTools.PropertySetter(typeof(Image), nameof(Image.sprite)), prefix: prefix);
            harmony.Patch(AccessTools.PropertySetter(typeof(Image), nameof(Image.overrideSprite)), prefix: prefix);
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Image), "OnEnable"),
                postfix: new HarmonyMethod(typeof(UISpriteReplacePatch), nameof(RefreshImage)));
            _harmony = harmony;
            return true;
        }
        catch (Exception exception)
        {
            harmony.UnpatchSelf();
            LogManager.Warning($"[UI/Sprite] Cannot install hooks/无法安装精灵图补丁: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// 在 uGUI 保存引用前处理单张精灵图的替换与导出。
    /// Process sprite replacement and dumping before uGUI stores the reference.
    /// </summary>
    private static void SpritePrefix(ref Sprite __0) => __0 = Resolve(__0);

    /// <summary>
    /// 处理普通和覆盖槽位里的精灵，保留空的覆盖槽位。
    /// Process sprites in the base and override slots, preserving an unset override.
    /// </summary>
    private static void RefreshImage(Image __instance)
    {
        try
        {
            var sprite = __instance.sprite;
            var replacement = Resolve(sprite);
            if (!ReferenceEquals(sprite, replacement)) __instance.sprite = replacement;

            var currentOverride = _overrideSprite(__instance);
            replacement = Resolve(currentOverride);
            if (!ReferenceEquals(currentOverride, replacement)) __instance.overrideSprite = replacement;
        }
        catch (Exception exception)
        {
            Report(__instance.name, $"cannot update Image/无法更新 Image: {exception.Message}");
        }
    }

    /// <summary>
    /// 返回已生成或新解码的精灵，未替换时按配置尝试导出。
    /// Return a cached or newly decoded sprite, or try to dump the original on a miss.
    /// </summary>
    private static Sprite Resolve(Sprite original)
    {
        if ((_catalog.Count == 0 && !_dumpEnabled) || original == null || IsReplacement(original)) return original;
        if (Replacements.TryGetValue(original, out var replacement)) return replacement;

        var name = original.name;
        try
        {
            if (_catalog.TryGetBytes(name, out var bytes)) return Build(original, bytes);
        }
        catch (Exception exception)
        {
            Report(name, $"cannot create replacement/无法生成替换精灵: {exception.Message}");
        }

        if (_dumpEnabled && original.texture != null && !string.IsNullOrWhiteSpace(name) &&
            DumpAttempts.Add(name))
            DumpManager.DumpSprite(name, () => SpritePngEncoder.Encode(original));

        return original;
    }

    /// <summary>
    /// 为原精灵生成独立纹理和替换对象，按图片尺寸换算轴心和九宫格边框。
    /// Create a standalone texture and sprite with the original pivot and scaled nine-slice borders.
    /// </summary>
    private static Sprite Build(Sprite original, byte[] bytes)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Sprite replacement = null;
        try
        {
            if (!ImageConversion.LoadImage(texture, bytes, false))
                throw new InvalidOperationException("Image decode failed/图片解码失败");

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.name = original.name + "_NET_UI";

            var rect = original.rect;
            var scaleX = texture.width / rect.width;
            var scaleY = texture.height / rect.height;
            var pivot = new Vector2(original.pivot.x / rect.width, original.pivot.y / rect.height);
            var border = original.border;
            border = new Vector4(border.x * scaleX, border.y * scaleY, border.z * scaleX, border.w * scaleY);
            replacement = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot,
                original.pixelsPerUnit * scaleX, 0, SpriteMeshType.FullRect, border);
            if (replacement == null) throw new InvalidOperationException("Sprite.Create failed/精灵创建失败");

            replacement.name = original.name;
            if (!Mathf.Approximately(scaleX, scaleY))
                Report(original.name, $"aspect ratio changed/宽高比变更: {rect.width}x{rect.height} -> {texture.width}x{texture.height}");

            Originals.Add(replacement, (original, texture));
            Replacements.Add(original, replacement);
            return replacement;
        }
        catch
        {
            if (replacement != null) Object.Destroy(replacement);
            Object.Destroy(texture);
            throw;
        }
    }

    /// <summary>
    /// 还原持有旧精灵的 Image，包括隐藏实例，之后释放本功能创建的资源。
    /// Restore Images holding previous sprites, including hidden instances, then release owned resources.
    /// </summary>
    private static void RestoreOriginals()
    {
        _catalog = TextureReplacementCatalog.Empty;
        _dumpEnabled = false;
        ReportedNames.Clear();
        DumpAttempts.Clear();
        Replacements.Clear();
        if (Originals.Count == 0) return;

        var restored = true;
        foreach (var image in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (image == null) continue;
            try
            {
                var sprite = image.sprite;
                if (!ReferenceEquals(sprite, null) && Originals.TryGetValue(sprite, out var entry))
                    image.sprite = entry.Original;

                var currentOverride = _overrideSprite(image);
                if (!ReferenceEquals(currentOverride, null) && Originals.TryGetValue(currentOverride, out entry))
                    image.overrideSprite = entry.Original;
            }
            catch (Exception exception)
            {
                restored = false;
                Report(image.name, $"cannot restore original sprite/无法还原原始精灵: {exception.Message}");
            }
        }

        // 写回失败时保留生成的对象，以便仍在使用的 Image 正常显示并在下一次重载时重试。
        // Retain owned objects after a failed write so Images using them can render until the next retry.
        if (!restored) return;

        foreach (var entry in Originals)
        {
            Object.Destroy(entry.Key);
            Object.Destroy(entry.Value.Texture);
        }
        Originals.Clear();
    }

    /// <summary>
    /// 每轮按名称限流精灵替换告警。
    /// Report a sprite replacement warning once per name in a resource cycle.
    /// </summary>
    private static void Report(string name, string message)
    {
        if (ReportedNames.Add(name ?? string.Empty))
            LogManager.Warning($"[UI/Sprite] {name}: {message}");
    }
}
