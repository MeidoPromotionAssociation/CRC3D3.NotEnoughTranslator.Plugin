using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CRC3D3.NotEnoughTranslator.Plugin.Utils;

public static class FileTool
{
    /// <summary>
    /// 根目录优先，再按 Ordinal 目录及文件顺序收集匹配扩展名的资源。
    /// Collect matching resources with the root directory first and subdirectories and files ordered ordinally.
    /// </summary>
    /// <param name="translationPath">资源搜索根目录 / The resource search root directory.</param>
    /// <param name="fileExtensions">含前导点的扩展名列表，匹配时忽略大小写 / Extensions including the leading dot, matched case-insensitively.</param>
    /// <returns>按加载顺序排列的资源文件路径 / Resource file paths arranged in loading order.</returns>
    public static List<string> GetAllTranslationFiles(string translationPath, string[] fileExtensions)
    {
        var extensions = new HashSet<string>(fileExtensions, StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();
        var directories = new[] { translationPath }.Concat(Directory
            .GetDirectories(translationPath, "*", SearchOption.AllDirectories)
            .OrderBy(directory => directory, StringComparer.Ordinal));

        foreach (var directory in directories)
            files.AddRange(Directory.GetFiles(directory)
                .Where(file => extensions.Contains(Path.GetExtension(file)))
                .OrderBy(file => file, StringComparer.Ordinal));

        return files;
    }
}
