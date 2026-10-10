using System.Threading.Tasks;
using CRC3D3.NotEnoughTranslator.Plugin.Loader;
using CRC3D3.NotEnoughTranslator.Plugin.Translation;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;

namespace CRC3D3.NotEnoughTranslator.Plugin.Manger;

public static class TextureReplaceManager
{
    private static ResourceModule<TextureReplacementCatalog> _module;
    private static volatile TextureReplacementCatalog _catalog = TextureReplacementCatalog.Empty;

    public static bool IsLoaded => _module?.IsLoaded == true;
    public static bool IsLoading => _module?.IsLoading == true;
    public static int EntryCount => _catalog.Count;

    /// <summary>
    /// 在 Unity 主线程初始化纹理模块，异步索引图片路径而不创建 Unity 纹理对象。
    /// Initialize the texture module on Unity's main thread and index image paths asynchronously without creating Unity textures.
    /// </summary>
    public static void Init()
    {
        if (_module != null || !NotEnoughTranslator.EnableTextureReplacement.Value) return;
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
    /// 取消索引加载并清空替换路径，不修改或销毁游戏纹理。
    /// Cancel index loading and clear replacement paths without modifying or destroying game textures.
    /// </summary>
    public static void Unload()
    {
        _module?.Dispose();
        _module = null;
        _catalog = TextureReplacementCatalog.Empty;
    }
}
