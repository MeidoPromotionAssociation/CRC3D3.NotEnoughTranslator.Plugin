using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;

namespace CRC3D3.NotEnoughTranslator.Plugin.Loader;

public sealed class AsyncTranslationLoader : IAsyncTranslationLoader, IDisposable
{
    private readonly string _loaderName;
    private readonly string _translationPath;
    private readonly bool _orderZipEntries;
    private readonly IProgress<TranslationLoadProgress> _progress;
    private readonly Dictionary<string, ITranslationFileProcessor> _processors =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _extensions;
    private CancellationTokenSource _cancellation;
    private Task<TranslationLoadResult> _loadTask;
    private bool _disposed;

    /// <summary>
    /// 创建异步翻译加载器，并建立文件扩展名与处理器的对应关系。
    /// Create an asynchronous translation loader and map file extensions to their processors.
    /// </summary>
    /// <param name="loaderName">用于日志标识的加载器名称 / The loader name used in logs.</param>
    /// <param name="translationPath">翻译资源根目录 / The translation-resource root directory.</param>
    /// <param name="progress">可选的加载进度接收器 / An optional receiver for loading progress.</param>
    /// <param name="orderZipEntries">是否按 Ordinal 文件名顺序处理 ZIP 条目 / Whether to process ZIP entries in Ordinal filename order.</param>
    /// <param name="processors">各资源格式对应的文件处理器 / File processors for the supported resource formats.</param>
    public AsyncTranslationLoader(string loaderName, string translationPath,
        IProgress<TranslationLoadProgress> progress, bool orderZipEntries,
        params ITranslationFileProcessor[] processors)
    {
        if (processors == null || processors.Length == 0)
            throw new ArgumentException("At least one file processor is required.", nameof(processors));

        _loaderName = loaderName;
        _translationPath = translationPath;
        _progress = progress;
        _orderZipEntries = orderZipEntries;
        foreach (var processor in processors)
            _processors.Add(processor.SupportedExtension, processor);
        var extensions = new List<string>(_processors.Keys) { ".zip" };
        _extensions = extensions.ToArray();
    }

    /// <summary>
    /// 创建临时加载器，执行一次异步加载并在结束后释放加载器资源。
    /// Create a temporary loader, perform one asynchronous load, and dispose its resources afterward.
    /// </summary>
    /// <param name="loaderName">用于日志标识的加载器名称 / The loader name used in logs.</param>
    /// <param name="translationPath">翻译资源根目录 / The translation-resource root directory.</param>
    /// <param name="orderZipEntries">是否按 Ordinal 文件名顺序处理 ZIP 条目 / Whether to process ZIP entries in Ordinal filename order.</param>
    /// <param name="cancellationToken">本次加载的取消令牌 / The cancellation token for this load.</param>
    /// <param name="processors">各资源格式对应的文件处理器 / File processors for the supported resource formats.</param>
    /// <returns>成功时包含完整资源快照的任务 / A task containing the complete resource snapshot on success.</returns>
    public static async Task<TranslationLoadResult> LoadOnceAsync(string loaderName,
        string translationPath, bool orderZipEntries, CancellationToken cancellationToken,
        params ITranslationFileProcessor[] processors)
    {
        using var loader = new AsyncTranslationLoader(loaderName, translationPath, null,
            orderZipEntries, processors);
        return await loader.StartLoadingAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 在后台启动加载；已有未完成任务时直接返回该任务。
    /// Start loading in the background, or return the existing unfinished task.
    /// </summary>
    /// <param name="cancellationToken">仅用于新启动加载的取消令牌 / The cancellation token used when starting a new load.</param>
    /// <returns>正在执行或新启动的资源加载任务 / The existing or newly started resource-loading task.</returns>
    public Task<TranslationLoadResult> StartLoadingAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AsyncTranslationLoader));
        if (_loadTask is { IsCompleted: false }) return _loadTask;

        _cancellation?.Dispose();
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cancellation.Token;
        _loadTask = Task.Run(() => Load(token), token);
        return _loadTask;
    }

    /// <summary>
    /// 请求取消当前加载，不同步等待任务结束。
    /// Request cancellation of the current load without waiting for completion.
    /// </summary>
    public void Cancel()
    {
        _cancellation?.Cancel();
    }

    /// <summary>
    /// 停用加载器，请求取消加载并释放其持有的取消源。
    /// Dispose the loader, request cancellation, and release its cancellation source.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    /// <summary>
    /// 读取支持的资源文件并汇总结果，文件级失败时拒绝返回不完整快照。
    /// Load supported resource files into a result, rejecting incomplete snapshots on file-level failures.
    /// </summary>
    /// <param name="cancellationToken">文件枚举和解析期间使用的取消令牌 / The cancellation token used while enumerating and parsing files.</param>
    /// <returns>包含词条、统计信息和耗时的完整加载结果 / The complete result with entries, statistics, and elapsed time.</returns>
    private TranslationLoadResult Load(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TranslationLoadResult();
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_translationPath);
        var files = FileTool.GetAllTranslationFiles(_translationPath, _extensions);
        var failedFiles = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    result.TotalEntries += ProcessZip(file, result, cancellationToken);
                else
                    result.TotalEntries += _processors[Path.GetExtension(file)]
                        .ProcessFile(file, result, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failedFiles++;
                LogManager.Error($"[{_loaderName}] Cannot read/无法读取 {file}: {exception.Message}");
            }

            result.TotalFiles++;
            _progress?.Report(new TranslationLoadProgress((float)result.TotalFiles / files.Count,
                result.TotalFiles, files.Count));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (failedFiles != 0)
            throw new IOException($"{failedFiles} resource file(s) failed; incomplete snapshot discarded.");

        result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        return result;
    }

    /// <summary>
    /// 按配置的条目顺序处理 ZIP 中受支持的翻译文件。
    /// Process supported translation files in a ZIP using the configured entry order.
    /// </summary>
    /// <param name="path">ZIP 文件路径 / The ZIP file path.</param>
    /// <param name="result">接收翻译条目的共享加载结果 / The shared load result receiving translation entries.</param>
    /// <param name="cancellationToken">归档条目处理期间使用的取消令牌 / The cancellation token used while processing archive entries.</param>
    /// <returns>成功加载的翻译条目总数 / The total number of successfully loaded translation entries.</returns>
    private int ProcessZip(string path, TranslationLoadResult result,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(path);
        var entries = new List<ZipArchiveEntry>();
        foreach (var entry in archive.Entries)
        {
            if (!string.IsNullOrEmpty(entry.Name) &&
                _processors.ContainsKey(Path.GetExtension(entry.FullName)))
                entries.Add(entry);
        }

        if (_orderZipEntries)
            entries.Sort((left, right) => StringComparer.Ordinal.Compare(left.FullName, right.FullName));

        var loaded = 0;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = entry.Open();
            loaded += _processors[Path.GetExtension(entry.FullName)].ProcessStream(stream, result,
                $"{path}!/{entry.FullName}", cancellationToken);
        }

        return loaded;
    }
}
