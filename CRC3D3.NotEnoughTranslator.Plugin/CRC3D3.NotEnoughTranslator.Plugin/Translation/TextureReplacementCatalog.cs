using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;

namespace CRC3D3.NotEnoughTranslator.Plugin.Translation;

/// <summary>
/// 维护纹理名称到替换图片路径的索引，按需读取图片编码数据，不缓存图片内容或创建 Unity 纹理对象。
/// Maintains an index from texture names to replacement-image paths and reads encoded image data on demand without caching image contents or creating Unity textures.
/// </summary>
internal sealed class TextureReplacementCatalog
{
    public static readonly TextureReplacementCatalog Empty = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private readonly Dictionary<string, string> _paths;

    public int Count => _paths.Count;

    /// <summary>
    /// 使用已构建的路径字典创建纹理替换索引。
    /// Create a texture-replacement catalog from an existing path dictionary.
    /// </summary>
    /// <param name="paths">由索引持有的纹理键到图片路径的映射 / The texture-key-to-image-path mapping retained by the catalog.</param>
    private TextureReplacementCatalog(Dictionary<string, string> paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// 扫描支持的替换图片并建立路径索引，同名纹理由后加载的文件覆盖。
    /// Scan supported replacement images into a path index, letting later files override duplicate texture names.
    /// </summary>
    /// <param name="directory">替换图片根目录 / The replacement-image root directory.</param>
    /// <param name="cancellationToken">路径扫描期间使用的取消令牌 / The cancellation token used while scanning paths.</param>
    /// <returns>不缓存图片内容的纹理路径索引 / A texture-path catalog that does not cache image contents.</returns>
    public static TextureReplacementCatalog Load(string directory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in FileTool.GetAllTranslationFiles(directory, new[] { ".png", ".jpg", ".jpeg" }))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = GetKey(path);
            if (paths.ContainsKey(key))
                LogManager.Warning($"[Texture] Duplicate name, later file wins/纹理重名，后加载文件覆盖: {key}: {path}");
            paths[key] = path;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new TextureReplacementCatalog(paths);
    }

    /// <summary>
    /// 按规范化纹理键查找并读取图片字节，读取失败时不返回替换数据。
    /// Resolve a normalized texture key and read image bytes, returning no replacement data on failure.
    /// </summary>
    /// <param name="textureName">纹理名称或包含文件扩展名的路径 / The texture name or path, optionally including a file extension.</param>
    /// <param name="data">非空图片编码数据，失败时为 null / Non-empty encoded image data, or null on failure.</param>
    /// <returns>找到并成功读取非空图片时为 true / True if a non-empty replacement image is found and read successfully.</returns>
    public bool TryGetBytes(string textureName, out byte[] data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(textureName)) return false;

        try
        {
            var key = GetKey(textureName);
            if (!_paths.TryGetValue(key, out var path)) return false;

            try
            {
                data = File.ReadAllBytes(path);
                if (data.Length == 0) throw new InvalidDataException("Empty texture file.");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                data = null;
                LogManager.Warning($"[Texture] Cannot read replacement/无法读取替换纹理 {path}: {exception.Message}");
            }

            return data != null;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 统一路径分隔符并提取文件名，去掉支持的图片或纹理扩展名。
    /// Normalize path separators and extract the filename, stripping supported image or texture extensions.
    /// </summary>
    /// <param name="name">纹理名称或文件路径 / The texture name or file path.</param>
    /// <returns>用于索引查询的纹理键 / The texture key used for catalog lookup.</returns>
    private static string GetKey(string name)
    {
        var fileName = Path.GetFileName(name.Replace('\\', '/'));
        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tex", StringComparison.OrdinalIgnoreCase))
            return Path.GetFileNameWithoutExtension(fileName);
        return fileName;
    }
}
