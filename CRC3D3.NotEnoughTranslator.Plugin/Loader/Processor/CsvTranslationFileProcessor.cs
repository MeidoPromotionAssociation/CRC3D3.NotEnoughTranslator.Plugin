using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;

namespace CRC3D3.NotEnoughTranslator.Plugin.Loader.Processor;

public sealed class CsvTranslationFileProcessor : ITranslationFileProcessor
{
    public string SupportedExtension => ".csv";

    /// <summary>
    /// 解析 CSV 表头与记录，将有效 UI 词条写入结果，并保持输入流打开。
    /// Parse CSV headers and records into valid UI terms while leaving the input stream open.
    /// </summary>
    /// <param name="stream">由调用方管理的 CSV 输入流 / The caller-owned CSV input stream.</param>
    /// <param name="result">接收 I2 词条和跳过计数的加载结果 / The load result receiving I2 terms and skipped-entry counts.</param>
    /// <param name="sourceName">用于日志定位的文件或归档条目名称 / The file or archive-entry name used in diagnostics.</param>
    /// <param name="cancellationToken">读取 CSV 记录时使用的取消令牌 / The cancellation token used while reading CSV records.</param>
    /// <returns>成功加载的 UI 翻译条目数 / The number of successfully loaded UI translation entries.</returns>
    public int ProcessStream(Stream stream, TranslationLoadResult result, string sourceName = null,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true);
        var entriesLoaded = 0;
        var recordNumber = 0;
        var termIndex = 0;
        var translationIndex = 2;
        var headerParsed = false;
        List<string> fields;

        while ((fields = ReadRecord(reader, cancellationToken)) != null)
        {
            recordNumber++;
            if (fields.All(string.IsNullOrWhiteSpace)) continue;

            if (!headerParsed)
            {
                headerParsed = true;
                var foundTerm = fields.FindIndex(field =>
                    field.Trim().Equals("Term", StringComparison.OrdinalIgnoreCase) ||
                    field.Trim().Equals("Key", StringComparison.OrdinalIgnoreCase));
                if (foundTerm >= 0)
                {
                    termIndex = foundTerm;
                    translationIndex = fields.FindIndex(field =>
                        field.Trim().Equals("Translation", StringComparison.OrdinalIgnoreCase));
                    if (translationIndex < 0)
                        throw new InvalidDataException("CSV header must contain 'Translation'.");
                    continue;
                }
            }

            if (fields.Count <= Math.Max(termIndex, translationIndex))
            {
                result.SkippedLines++;
                LogManager.Warning($"[UI] Missing CSV columns/CSV 缺少列: {sourceName}, record {recordNumber}");
                continue;
            }

            var term = fields[termIndex].Trim();
            var translation = fields[translationIndex];
            if (string.IsNullOrEmpty(term) || string.IsNullOrWhiteSpace(translation)) continue;
            result.I2Terms[term] = translation;
            entriesLoaded++;
        }

        return entriesLoaded;
    }

    /// <summary>
    /// 打开并处理 CSV 翻译文件，结束后关闭文件流。
    /// Open and process a CSV translation file, closing its stream afterward.
    /// </summary>
    /// <param name="filePath">CSV 翻译文件路径 / The CSV translation file path.</param>
    /// <param name="result">接收 UI 翻译条目的加载结果 / The load result receiving UI translation entries.</param>
    /// <param name="cancellationToken">文件处理操作的取消令牌 / The cancellation token for file processing.</param>
    /// <returns>成功加载的 UI 翻译条目数 / The number of successfully loaded UI translation entries.</returns>
    public int ProcessFile(string filePath, TranslationLoadResult result,
        CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(filePath);
        return ProcessStream(stream, result, filePath, cancellationToken);
    }

    /// <summary>
    /// 读取一条 CSV 记录，支持引号转义、多行字段和整行注释。
    /// Read a CSV record with escaped quotes, multiline fields, and full-line comments.
    /// </summary>
    /// <param name="reader">提供 CSV 字符的读取器 / The reader supplying CSV characters.</param>
    /// <param name="cancellationToken">逐字符读取时检查的取消令牌 / The cancellation token checked while reading characters.</param>
    /// <returns>字段列表；注释行为为空列表，输入结束时为 null / Field values; an empty list for a comment line, or null at end of input.</returns>
    private static List<string> ReadRecord(TextReader reader, CancellationToken cancellationToken)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var closedQuote = false;
        var hasInput = false;
        int characterCode;

        while ((characterCode = reader.Read()) >= 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hasInput = true;
            var character = (char)characterCode;

            if (inQuotes)
            {
                if (character == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                        closedQuote = true;
                    }
                }
                else field.Append(character);
                continue;
            }

            if (fields.Count == 0 && !closedQuote &&
                (character == '#' || (character == '/' && reader.Peek() == '/')) &&
                string.IsNullOrWhiteSpace(field.ToString()))
            {
                reader.ReadLine();
                return fields;
            }

            if (character == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                closedQuote = false;
            }
            else if (character == '\r' || character == '\n')
            {
                if (character == '\r' && reader.Peek() == '\n') reader.Read();
                fields.Add(field.ToString());
                return fields;
            }
            else if (character == '"')
            {
                if (closedQuote || !string.IsNullOrWhiteSpace(field.ToString()))
                    throw new InvalidDataException("Unexpected quote in CSV field.");
                field.Clear();
                inQuotes = true;
            }
            else if (closedQuote)
            {
                if (!char.IsWhiteSpace(character))
                    throw new InvalidDataException("Unexpected text after quoted CSV field.");
            }
            else field.Append(character);
        }

        if (inQuotes) throw new InvalidDataException("Unclosed quoted CSV field.");
        if (!hasInput) return null;
        fields.Add(field.ToString());
        return fields;
    }
}
