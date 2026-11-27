using System;
using System.Threading.Tasks;
using CRC3D3.NotEnoughTranslator.Plugin.Hooks.Texture;
using CRC3D3.NotEnoughTranslator.Plugin.Loader;
using CRC3D3.NotEnoughTranslator.Plugin.Translation;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;
using HarmonyLib;

namespace CRC3D3.NotEnoughTranslator.Plugin.Manger;

public static class TextureReplaceManager
{
    private static ResourceModule<TextureReplacementCatalog> _module;
    private static volatile TextureReplacementCatalog _catalog = TextureReplacementCatalog.Empty;
    private static Harmony _textureHarmony;

    public static bool IsLoaded => _module?.IsLoaded == true;
    public static bool IsLoading => _module?.IsLoading == true;
    public static bool IsHookInstalled => _textureHarmony != null;
    public static int EntryCount => _catalog.Count;

    /// <summary>
    /// 在 Unity 主线程初始化纹理模块，安装精灵加载补丁并异步索引图片路径而不创建 Unity 纹理对象。
    /// Initialize the texture module on Unity's main thread, install the sprite load patch, and index image paths asynchronously without creating Unity textures.
    /// </summary>
    public static void Init()
    {
        if (_module != null || !NotEnoughTranslator.EnableTextureReplacement.Value) return;
        try
        {
            _textureHarmony = TextureReplacePatch.Install();
        }
        catch (Exception exception)
        {
            new Harmony(TextureReplacePatch.HarmonyId).UnpatchSelf();
            _textureHarmony = null;
            LogManager.Warning($"[Texture] Cannot install hooks; textures stay query-only/无法安装纹理补丁，纹理仍仅提供查询: {exception.Message}");
        }

        _module = new ResourceModule<TextureReplacementCatalog>("Texture", token =>
        {
            var directory = NotEnoughTranslator.TextureReplacePath;
            return Task.Run(() => TextureReplacementCatalog.Load(directory, token), token);
        }, catalog =>
        {
            _catalog = catalog;
            LogManager.Info($"[Texture] Indexed/已索引 {EntryCount} replacement textures/替换纹理");
        });
        _module.Reload();
    }

    /// <summary>
    /// 检查是否存在该名称的替换图片，只查询索引而不读取文件。
    /// Check whether a replacement exists for the name, querying the index without reading the file.
    /// </summary>
    /// <param name="textureName">纹理名称或包含扩展名的路径 / The texture name or a path including its extension.</param>
    /// <returns>索引中存在该名称时为 true / True when the name exists in the index.</returns>
    public static bool HasReplacement(string textureName)
    {
        return _catalog.Contains(textureName);
    }

    /// <summary>
    /// 按纹理名称读取替换图片的编码数据，不缓存图片内容。
    /// Read encoded replacement-image data by texture name without caching image contents.
    /// </summary>
    /// <param name="textureName">待替换的纹理名称 / The name of the texture to replace.</param>
    /// <param name="data">成功读取的图片编码字节，失败时为 null / Encoded image bytes on success, or null on failure.</param>
    /// <returns>找到并成功读取替换图片时为 true / True if a replacement image is found and read successfully.</returns>
    public static bool TryGetReplacement(string textureName, out byte[] data)
    {
        return _catalog.TryGetBytes(textureName, out data);
    }

    /// <summary>
    /// 在 Unity 主线程按启用配置初始化、重载或卸载纹理路径索引。
    /// Initialize, reload, or unload the texture-path index on Unity's main thread according to its enable setting.
    /// </summary>
    public static void Reload()
    {
        if (!NotEnoughTranslator.EnableTextureReplacement.Value) Unload();
        else if (_module == null) Init();
        else _module.Reload();
    }

    /// <summary>
    /// 移除纹理补丁、取消索引加载并清空替换路径，不修改或销毁游戏纹理。
    /// Remove the texture patch, cancel index loading, and clear replacement paths without modifying or destroying game textures.
    /// </summary>
    public static void Unload()
    {
        _textureHarmony?.UnpatchSelf();
        _textureHarmony = null;
        TextureReplacePatch.Reset();
        _module?.Dispose();
        _module = null;
        _catalog = TextureReplacementCatalog.Empty;
    }
}
