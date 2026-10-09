namespace CRC3D3.NotEnoughTranslator.Plugin.Loader;

/// <summary>
///     加载进度快照。
/// </summary>
public readonly struct TranslationLoadProgress
{
    /// <summary>总体进度，0.0 ~ 1.0</summary>
    public float Progress { get; }

    /// <summary>已处理文件数</summary>
    public int FilesProcessed { get; }

    /// <summary>总文件数</summary>
    public int TotalFiles { get; }

    /// <summary>
    /// 创建一次不可变的翻译资源加载进度快照。
    /// Create an immutable snapshot of translation-resource loading progress.
    /// </summary>
    /// <param name="progress">总体进度，范围为 0 到 1 / Overall progress in the range from 0 to 1.</param>
    /// <param name="filesProcessed">已处理文件数 / The number of files processed.</param>
    /// <param name="totalFiles">待处理文件总数 / The total number of files to process.</param>
    public TranslationLoadProgress(float progress, int filesProcessed, int totalFiles)
    {
        Progress = progress;
        FilesProcessed = filesProcessed;
        TotalFiles = totalFiles;
    }

    /// <summary>
    /// 将加载进度格式化为包含百分比和文件计数的日志文字。
    /// Format loading progress as log text containing a percentage and file counts.
    /// </summary>
    /// <returns>百分比及已处理文件数与总文件数 / The percentage followed by processed and total file counts.</returns>
    public override string ToString()
    {
        return $"{Progress:P0} ({FilesProcessed}/{TotalFiles})";
    }
}
