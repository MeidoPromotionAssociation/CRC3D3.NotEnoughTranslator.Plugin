using System.IO;
using System.Threading;

namespace CRC3D3.NotEnoughTranslator.Plugin.Loader;

public interface ITranslationFileProcessor
{
    string SupportedExtension { get; }

    /// <summary>
    /// 解析输入流并将翻译条目写入加载结果，保留调用方对流的所有权。
    /// Parse an input stream into the load result while leaving stream ownership with the caller.
    /// </summary>
    /// <param name="stream">由调用方管理生命周期的输入流 / The input stream whose lifetime is owned by the caller.</param>
    /// <param name="result">接收条目和统计信息的加载结果 / The load result receiving entries and statistics.</param>
    /// <param name="sourceName">用于诊断的可选来源名称 / An optional source name used in diagnostics.</param>
    /// <param name="cancellationToken">解析操作的取消令牌 / The cancellation token for parsing.</param>
    /// <returns>成功加载的翻译条目数 / The number of successfully loaded translation entries.</returns>
    int ProcessStream(Stream stream, TranslationLoadResult result, string sourceName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 打开并解析翻译文件，将条目写入指定加载结果。
    /// Open and parse a translation file into the supplied load result.
    /// </summary>
    /// <param name="filePath">翻译文件路径 / The translation file path.</param>
    /// <param name="result">接收条目和统计信息的加载结果 / The load result receiving entries and statistics.</param>
    /// <param name="cancellationToken">文件解析操作的取消令牌 / The cancellation token for file parsing.</param>
    /// <returns>成功加载的翻译条目数 / The number of successfully loaded translation entries.</returns>
    int ProcessFile(string filePath, TranslationLoadResult result,
        CancellationToken cancellationToken = default);
}
