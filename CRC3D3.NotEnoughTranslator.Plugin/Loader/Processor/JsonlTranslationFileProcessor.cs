using System;
using System.IO;
using System.Text;
using System.Threading;
using CRC3D3.NotEnoughTranslator.Plugin.Translation;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CRC3D3.NotEnoughTranslator.Plugin.Loader.Processor;

public sealed class JsonlTranslationFileProcessor : ITranslationFileProcessor
{
    private readonly TimeSpan _regexTimeout;

    public string SupportedExtension => ".jsonl";

    /// <summary>
    /// 创建 JSONL 处理器并设置正则规则的执行超时。
    /// Create a JSONL processor with a regex execution timeout.
    /// </summary>
    /// <param name="regexTimeout">正则超时时间，null 时使用 100 毫秒 / The regex timeout, defaulting to 100 milliseconds when null.</param>
    public JsonlTranslationFileProcessor(TimeSpan? regexTimeout = null)
    {
        _regexTimeout = regexTimeout ?? TimeSpan.FromMilliseconds(100);
    }

    /// <summary>
    /// 逐行解析 JSONL 精确与正则译文，记录并跳过无效条目，不关闭输入流。
    /// Parse exact and regex translations from JSONL, logging and skipping invalid entries without closing the input stream.
    /// </summary>
    /// <param name="stream">由调用方管理的 JSONL 输入流 / The caller-owned JSONL input stream.</param>
    /// <param name="result">接收通用文本条目和跳过计数的加载结果 / The load result receiving general-text entries and skipped-entry counts.</param>
    /// <param name="sourceName">用于诊断和规则来源记录的名称 / The source name used for diagnostics and rule provenance.</param>
    /// <param name="cancellationToken">逐行解析时检查的取消令牌 / The cancellation token checked while parsing lines.</param>
    /// <returns>成功加载的精确与正则条目总数 / The total number of successfully loaded exact and regex entries.</returns>
    public int ProcessStream(Stream stream, TranslationLoadResult result, string sourceName = null,
        CancellationToken cancellationToken = default)
    {
        var entriesCount = 0;
        var lineNumber = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true);

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var location = $"{sourceName ?? "JSONL"}:{lineNumber}";

            try
            {
                var record = JObject.Parse(line);
                var originalToken = record["original"];
                var textToken = record["text"];
                var regexToken = record["regex"];
                if (originalToken?.Type != JTokenType.String || textToken?.Type != JTokenType.String ||
                    (regexToken != null && regexToken.Type != JTokenType.Boolean))
                    throw new JsonException("Expected string 'original' and 'text', and optional boolean 'regex'.");

                var original = originalToken.Value<string>();
                var text = textToken.Value<string>();
                if (string.IsNullOrEmpty(original))
                    throw new JsonException("'original' must not be empty.");

                if (regexToken?.Value<bool>() == true)
                    result.TextRegexTranslations.Add(new RegexTranslationRule(original, text, _regexTimeout, location));
                else
                {
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    result.TextTranslations[original] = text;
                }

                entriesCount++;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                result.SkippedLines++;
                LogManager.Warning($"[Text] Invalid entry/无效翻译条目 {location}: {exception.Message}");
            }
        }

        return entriesCount;
    }

    /// <summary>
    /// 打开并处理 JSONL 翻译文件，结束后关闭文件流。
    /// Open and process a JSONL translation file, closing its stream afterward.
    /// </summary>
    /// <param name="filePath">JSONL 翻译文件路径 / The JSONL translation file path.</param>
    /// <param name="result">接收通用文本翻译条目的加载结果 / The load result receiving general-text translation entries.</param>
    /// <param name="cancellationToken">文件处理操作的取消令牌 / The cancellation token for file processing.</param>
    /// <returns>成功加载的精确与正则条目总数 / The total number of successfully loaded exact and regex entries.</returns>
    public int ProcessFile(string filePath, TranslationLoadResult result,
        CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(filePath);
        return ProcessStream(stream, result, filePath, cancellationToken);
    }
}
