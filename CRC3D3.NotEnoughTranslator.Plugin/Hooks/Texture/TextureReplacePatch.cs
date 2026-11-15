using System;
using System.Collections.Generic;
using System.Reflection;
using CRC3D3.NotEnoughTranslator.Plugin.Manger;
using CRC3D3.NotEnoughTranslator.Plugin.Translation;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CRC3D3.NotEnoughTranslator.Plugin.Hooks.Texture;

/// <summary>
/// 纹理替换补丁：在精灵资源加载入口和 uGUI 图片赋值处原地替换纹理像素，未命中时按配置导出原图。
/// 由 <see cref="TextureReplaceManager"/> 通过 PatchAll 一次性安装。
/// Texture-replacement patches that swap texture pixels in place at the sprite load entry and on uGUI image assignment, dumping originals on a miss.
/// Installed in one go by <see cref="TextureReplaceManager"/> through PatchAll.
/// </summary>
[HarmonyPatch]
internal static class TextureReplacePatch
{
    /// <summary>
    /// 纹理替换补丁的独立 Harmony ID。
    /// The dedicated Harmony ID for texture-replacement patches.
    /// </summary>
    public const string HarmonyId =
        "github.meidopromotionassociation.crc3d3.notenoughtranslator.plugin.hooks.texture.texturereplacepatch";

    private static readonly object SyncRoot = new();
    private static readonly HashSet<int> AppliedTextures = new();
    private static readonly HashSet<string> ReportedKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> DumpedKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 定位 <c>GameResource.LoadAsset&lt;Sprite&gt;(string)</c>；泛型参数无法写进特性构造参数，只能运行时构造。
    /// Locate <c>GameResource.LoadAsset&lt;Sprite&gt;(string)</c>; generic arguments cannot appear in an attribute argument, so it is constructed at runtime.
    /// </summary>
    /// <returns>已构造的泛型方法 / The constructed generic method.</returns>
    /// <exception cref="MissingMethodException">签名不匹配时抛出，由管理器回滚 / Thrown on a signature mismatch so the manager can roll back.</exception>
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        foreach (var method in typeof(GameResource).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "LoadAsset" || !method.IsGenericMethodDefinition) continue;
            var parameters = method.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string)) continue;
            return method.MakeGenericMethod(typeof(Sprite));
        }

        throw new MissingMethodException(
            "[Texture] GameResource.LoadAsset<Sprite> unavailable; texture hooks skipped/未找到精灵资源加载入口，跳过纹理补丁");
    }

    /// <summary>
    /// 在资源返回后替换纹理像素，未命中替换图时按配置导出原图。
    /// Replace texture pixels after the asset returns, dumping the original on a miss when enabled.
    /// </summary>
    /// <param name="__0">按位置绑定的资源名 / The asset name, bound by position.</param>
    /// <param name="__result">加载到的精灵，可能为 null / The loaded sprite, which may be null.</param>
    [HarmonyPostfix]
    private static void LoadAssetPostfix(string __0, Sprite __result)
    {
        if (__result == null) return;
        try
        {
            Apply(__result, __0, true);
        }
        catch (Exception exception)
        {
            Report(__0, $"replacement failed/替换失败: {exception.Message}");
        }
    }

    /// <summary>
    /// uGUI 图片赋值入口：覆盖不经过 GameResource 的精灵，例如随预制体序列化的图片。
    /// uGUI image assignment entry, covering sprites that never pass through GameResource, such as prefab-serialized images.
    /// </summary>
    /// <param name="__0">按位置绑定的 sprite 参数，原地修改其纹理 / The sprite argument bound by position, whose texture is modified in place.</param>
    [HarmonyPatch(typeof(Image), nameof(Image.sprite), MethodType.Setter)]
    [HarmonyPrefix]
    private static void ImageSpritePrefix(ref Sprite __0)
    {
        if (__0 == null) return;
        try
        {
            Apply(__0, null, false);
        }
        catch (Exception exception)
        {
            Report(__0.name, $"UI replacement failed/UI 替换失败: {exception.Message}");
        }
    }

    /// <summary>
    /// 界面激活时重新赋一次 sprite，让预制体反序列化写入的字段也经过 <see cref="Image.sprite"/> setter。
    /// Reassign the sprite on activation so prefab-deserialized fields also pass through the <see cref="Image.sprite"/> setter.
    /// </summary>
    /// <param name="__instance">被激活的图形组件 / The graphic component being enabled.</param>
    // OnEnable 是 protected，不能写 nameof，只能用字符串 / OnEnable is protected, so the name is a literal
    [HarmonyPatch(typeof(MaskableGraphic), "OnEnable")]
    [HarmonyPrefix]
    private static void GraphicOnEnablePrefix(MaskableGraphic __instance)
    {
        if (!(__instance is Image image)) return;
        var sprite = image.sprite;
        if (sprite == null) return;

        // 直接读字段后重赋值，确保 setter 补丁一定会执行 / Reassign after reading the field so the setter patch always runs
        image.sprite = sprite;
    }

    /// <summary>
    /// 清空本轮已应用与已报告的纹理记录，供卸载或重载时复用。
    /// Clear this session's applied and reported texture records for unload or reload.
    /// </summary>
    public static void Reset()
    {
        lock (SyncRoot)
        {
            AppliedTextures.Clear();
            ReportedKeys.Clear();
            DumpedKeys.Clear();
        }
    }

    /// <summary>
    /// 按资源名、精灵名或纹理名查找替换图并原地替换；不可原地替换的纹理只记录一次原因。
    /// Resolve a replacement by asset name, sprite name, or texture name and swap in place; textures that cannot be swapped report the reason once.
    /// </summary>
    /// <param name="sprite">加载或赋值得到的精灵 / The sprite that was loaded or assigned.</param>
    /// <param name="nameHint">调用方已知的资源名，可为 null / The asset name known by the caller, which may be null.</param>
    /// <param name="reportSkip">跳过原因是否写入日志；UI 赋值路径会大量命中图集子图，不重复告警 / Whether to log skip reasons; the UI path hits atlas sub-rects often and stays quiet.</param>
    private static void Apply(Sprite sprite, string nameHint, bool reportSkip)
    {
        var texture = sprite.texture;
        if (texture == null) return;
        var wholeTexture = IsWholeTexture(sprite, texture);

        var key = FindReplacementKey(nameHint, sprite.name, texture.name);
        if (key == null)
        {
            if (wholeTexture) DumpOriginal(texture, nameHint ?? sprite.name ?? texture.name);
            return;
        }

        var instanceId = texture.GetInstanceID();
        lock (SyncRoot)
            if (AppliedTextures.Contains(instanceId)) return;

        if (!wholeTexture)
        {
            if (reportSkip)
                Report(key, "sprite is an atlas sub-rect; in-place replacement skipped/精灵为图集子图，跳过原地替换");
            return;
        }

        if (!(texture is Texture2D texture2D))
        {
            Report(key, $"texture type {texture.GetType().Name} is unsupported/纹理类型不受支持");
            return;
        }

        if (!TextureReplaceManager.TryGetReplacement(key, out var data)) return;

        var width = texture2D.width;
        var height = texture2D.height;
        if (!ImageConversion.LoadImage(texture2D, data, false))
        {
            Report(key, "cannot decode replacement image/无法解码替换图片");
            return;
        }

        lock (SyncRoot) AppliedTextures.Add(instanceId);
        if (texture2D.width != width || texture2D.height != height)
            Report(key, $"size changed {width}x{height} -> {texture2D.width}x{texture2D.height}; sprites may look off/尺寸变化，显示可能错位");
    }

    /// <summary>
    /// 依次用候选名称查询索引，返回第一个存在替换图的名称。
    /// Probe the index with each candidate name in order, returning the first one that has a replacement.
    /// </summary>
    /// <param name="candidates">按优先级排列的资源名、精灵名与纹理名 / Asset, sprite, and texture names in priority order.</param>
    /// <returns>存在替换图的名称；都不存在时为 null / The matching name, or null when none has a replacement.</returns>
    private static string FindReplacementKey(params string[] candidates)
    {
        foreach (var candidate in candidates)
            if (TextureReplaceManager.HasReplacement(candidate)) return candidate;
        return null;
    }

    /// <summary>
    /// 判断精灵是否使用整张纹理，避免把图集中的其他图片一起覆盖。
    /// Check whether the sprite covers the whole texture, avoiding overwriting other atlas entries.
    /// </summary>
    /// <param name="sprite">待检查的精灵 / The sprite to check.</param>
    /// <param name="texture">精灵引用的纹理 / The texture referenced by the sprite.</param>
    /// <returns>精灵矩形覆盖整张纹理时为 true / True when the sprite rectangle covers the whole texture.</returns>
    private static bool IsWholeTexture(Sprite sprite, UnityEngine.Texture texture)
    {
        var rect = sprite.rect;
        return Mathf.Approximately(rect.width, texture.width) && Mathf.Approximately(rect.height, texture.height);
    }

    /// <summary>
    /// 导出启用时把未命中的原图编码为 PNG 交给导出设施，同一名称只尝试一次。
    /// When dumping is enabled, encode and hand the missing original to the dump infrastructure once per name.
    /// </summary>
    /// <param name="texture">未找到替换图的原始纹理 / The original texture with no replacement.</param>
    /// <param name="textureName">用于导出文件名的纹理名称 / The texture name used for the dump filename.</param>
    private static void DumpOriginal(UnityEngine.Texture texture, string textureName)
    {
        if (!NotEnoughTranslator.EnableTexturesDump.Value || string.IsNullOrWhiteSpace(textureName)) return;

        var key = TextureReplacementCatalog.GetKey(textureName);
        lock (SyncRoot)
            if (!DumpedKeys.Add(key)) return;

        var png = EncodePng(texture);
        if (png != null) DumpManager.DumpTexture(key, png);
    }

    /// <summary>
    /// 编码 PNG；纹理不可读时先复制到临时渲染纹理再读回。
    /// Encode PNG, copying unreadable textures into a temporary render texture first.
    /// </summary>
    /// <param name="texture">待编码的纹理 / The texture to encode.</param>
    /// <returns>PNG 数据；失败时为 null / PNG bytes, or null on failure.</returns>
    private static byte[] EncodePng(UnityEngine.Texture texture)
    {
        if (texture is Texture2D readable)
        {
            try
            {
                return readable.EncodeToPNG();
            }
            catch (Exception)
            {
                // 纹理不可读时退回渲染纹理拷贝 / Fall back to a render-texture copy when unreadable
            }
        }

        RenderTexture temporary = null;
        Texture2D copy = null;
        var previous = RenderTexture.active;
        try
        {
            temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, temporary);
            RenderTexture.active = temporary;
            copy = new Texture2D(texture.width, texture.height);
            copy.ReadPixels(new Rect(0, 0, temporary.width, temporary.height), 0, 0);
            copy.Apply();
            return copy.EncodeToPNG();
        }
        catch (Exception exception)
        {
            Report(texture.name, $"cannot encode original texture/无法编码原始纹理: {exception.Message}");
            return null;
        }
        finally
        {
            RenderTexture.active = previous;
            if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            if (copy != null) UnityEngine.Object.Destroy(copy);
        }
    }

    /// <summary>
    /// 对同一个键只记录一次告警，避免每次加载都刷屏。
    /// Report a warning once per key so repeated loads do not flood the log.
    /// </summary>
    /// <param name="key">纹理键 / The texture key.</param>
    /// <param name="message">告警内容 / The warning message.</param>
    private static void Report(string key, string message)
    {
        lock (SyncRoot)
            if (!ReportedKeys.Add(key ?? string.Empty)) return;
        LogManager.Warning($"[Texture] {key}: {message}");
    }
}
