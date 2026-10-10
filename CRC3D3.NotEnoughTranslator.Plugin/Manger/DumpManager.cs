using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CRC3D3.NotEnoughTranslator.Plugin.Translation;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CRC3D3.NotEnoughTranslator.Plugin.Manger;

/// <summary>
/// 管理三个模块的独立导出目录，对未翻译文字去重并缓冲写入，按调用导出原始 PNG。
/// Manage isolated dump directories, deduplicate and buffer untranslated text, and export original PNG data on demand.
/// </summary>
public static class DumpManager
{
    private static readonly object SyncRoot = new();
    private static readonly HashSet<string> DumpedTerms = new(StringComparer.Ordinal);
    private static readonly HashSet<string> DumpedTexts = new(StringComparer.Ordinal);
    private static readonly List<string> TermDumpBuffer = new();
    private static readonly List<string> TextDumpBuffer = new();
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    private static string _termDumpFilePath;
    private static string _textDumpFilePath;
    private static bool _initialized;

    /// <summary>
    /// 创建 JAT 式导出目录和本次运行的文件名，保留并优先重试上次未写入的缓冲。
    /// Create JAT-style dump directories and session filenames, retrying any previously unwritten buffers first.
    /// </summary>
    public static void Init()
    {
        lock (SyncRoot)
        {
            if (_initialized || !Flush()) return;
            DumpedTerms.Clear();
            DumpedTexts.Clear();
            var sessionName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff", CultureInfo.InvariantCulture) +
                "_" + Guid.NewGuid().ToString("N");
            _termDumpFilePath = Path.Combine(NotEnoughTranslator.TermDumpPath, sessionName + "_untranslated_terms.csv");
            _textDumpFilePath = Path.Combine(NotEnoughTranslator.TextDumpPath, sessionName + "_untranslated_text.jsonl");

            foreach (var directory in new[]
                     {
                         NotEnoughTranslator.TermDumpPath, NotEnoughTranslator.TextDumpPath,
                         NotEnoughTranslator.TextureDumpPath
                     })
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception exception)
                {
                    LogManager.Error($"[Dump] Cannot create directory/无法创建导出目录 {directory}: {exception.Message}");
                }
            }

            _initialized = true;
        }
    }

    /// <summary>
    /// 导出启用时按 I2 词条去重并缓冲 CSV 记录，达到阈值后写入文件。
    /// Deduplicate I2 terms and buffer CSV records when dumping is enabled, flushing at the configured threshold.
    /// </summary>
    /// <param name="term">调用方已确认未翻译的 I2 词条键 / An I2 term key confirmed untranslated by the caller.</param>
    /// <param name="original">游戏返回的原始显示文字，允许为空 / The original display text returned by the game, which may be empty.</param>
    public static void DumpTerm(string term, string original)
    {
        lock (SyncRoot)
        {
            if (!_initialized || !NotEnoughTranslator.EnableTermDump.Value ||
                string.IsNullOrWhiteSpace(term) || term == "-" || !DumpedTerms.Add(term)) return;

            TermDumpBuffer.Add(EscapeCsvField(term) + "," + EscapeCsvField(original) + ",\"\"");
            if (TermDumpBuffer.Count >= NotEnoughTranslator.TermDumpThreshold.Value)
                FlushBuffer(TermDumpBuffer, _termDumpFilePath, "Term,Original,Translation");
        }
    }

    /// <summary>
    /// 导出启用时按原文去重并缓冲 JSONL 模板，保持原文内容且不做自定义转义。
    /// Deduplicate source text and buffer JSONL templates when enabled, preserving text without custom escaping.
    /// </summary>
    /// <param name="original">调用方已确认未翻译的通用文本 / General text confirmed untranslated by the caller.</param>
    public static void DumpText(string original)
    {
        lock (SyncRoot)
        {
            if (!_initialized || !NotEnoughTranslator.EnableTextDump.Value ||
                string.IsNullOrWhiteSpace(original) || !DumpedTexts.Add(original)) return;

            var record = new JObject { ["original"] = original, ["text"] = string.Empty };
            TextDumpBuffer.Add(record.ToString(Formatting.None));
            if (TextDumpBuffer.Count >= NotEnoughTranslator.TextDumpThreshold.Value)
                FlushBuffer(TextDumpBuffer, _textDumpFilePath, null);
        }
    }

    /// <summary>
    /// 将调用方已编码的原图 PNG 安全写入纹理导出目录，不覆盖同名文件或访问 Unity 对象。
    /// Safely write caller-encoded original PNG data to the texture dump directory without overwriting files or accessing Unity objects.
    /// </summary>
    /// <param name="textureName">纹理名称或路径，使用与替换索引相同的文件名规则 / A texture name or path, normalized with the replacement catalog's filename rules.</param>
    /// <param name="pngData">已在合适线程完成编码的原图 PNG 数据 / Original PNG data already encoded on an appropriate thread.</param>
    /// <returns>新文件写入成功时为 true；未启用、输入无效、文件已存在或失败时为 false / True for a newly written file; false if disabled, invalid, already present, or unsuccessful.</returns>
    public static bool DumpTexture(string textureName, byte[] pngData)
    {
        lock (SyncRoot)
        {
            if (!_initialized || !NotEnoughTranslator.EnableTexturesDump.Value ||
                string.IsNullOrWhiteSpace(textureName) || !HasPngSignature(pngData)) return false;

            try
            {
                var fileName = TextureReplacementCatalog.GetKey(textureName);
                if (string.IsNullOrWhiteSpace(fileName) || fileName == "." || fileName == ".." ||
                    fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;

                var filePath = Path.Combine(NotEnoughTranslator.TextureDumpPath, fileName + ".png");
                if (File.Exists(filePath)) return false;
                Directory.CreateDirectory(NotEnoughTranslator.TextureDumpPath);
                var temporaryPath = Path.Combine(NotEnoughTranslator.TextureDumpPath,
                    Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllBytes(temporaryPath, pngData);
                    File.Move(temporaryPath, filePath);
                    return true;
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
            catch (Exception exception)
            {
                LogManager.Error($"[Dump] Cannot write texture/无法导出纹理 {textureName}: {exception.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 显式写入两个文本缓冲；不受开关限制，失败的缓冲保留以供下次重试。
    /// Explicitly flush both text buffers regardless of enable settings, retaining failed buffers for a later retry.
    /// </summary>
    /// <returns>两个缓冲均成功写入或原本为空时为 true / True if both buffers were written successfully or already empty.</returns>
    public static bool Flush()
    {
        lock (SyncRoot)
        {
            var termsFlushed = FlushBuffer(TermDumpBuffer, _termDumpFilePath, "Term,Original,Translation");
            var textFlushed = FlushBuffer(TextDumpBuffer, _textDumpFilePath, null);
            return termsFlushed && textFlushed;
        }
    }

    /// <summary>
    /// 停止接受新导出并尝试写入剩余缓冲，不通过逐帧回调或定时器处理。
    /// Stop accepting new dumps and try to flush remaining buffers, without per-frame callbacks or timers.
    /// </summary>
    public static void Shutdown()
    {
        lock (SyncRoot)
        {
            _initialized = false;
            Flush();
        }
    }

    /// <summary>
    /// 在持有导出锁时追加完整批次，仅为新 CSV 写表头，失败时回退文件长度并保留缓冲。
    /// Append a batch while holding the dump lock, writing CSV headers only for empty files and rolling back length on failure.
    /// </summary>
    /// <param name="buffer">待写入的完整序列化记录 / Complete serialized records awaiting writing.</param>
    /// <param name="filePath">当前运行对应的导出文件路径 / The dump file path for the current session.</param>
    /// <param name="header">CSV 表头；JSONL 使用 null / The CSV header, or null for JSONL.</param>
    /// <returns>批次写入成功或缓冲为空时为 true / True if the batch was written successfully or the buffer was empty.</returns>
    private static bool FlushBuffer(List<string> buffer, string filePath, string header)
    {
        if (buffer.Count == 0) return true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            using (var stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
                var originalLength = stream.Length;
                stream.Position = originalLength;
                try
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                    {
                        if (originalLength == 0 && header != null) writer.WriteLine(header);
                        foreach (var record in buffer) writer.WriteLine(record);
                    }
                    stream.Flush();
                }
                catch
                {
                    stream.SetLength(originalLength);
                    throw;
                }
            }
            buffer.Clear();
            return true;
        }
        catch (Exception exception)
        {
            LogManager.Error($"[Dump] Cannot flush; buffer retained/无法写入导出文件，保留缓冲 {filePath}: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// 按标准 CSV 规则包围字段并转义双引号，保留实际换行。
    /// Quote a field and escape double quotes using standard CSV rules, preserving actual line breaks.
    /// </summary>
    /// <param name="value">待序列化的字段，null 按空字符串处理 / The field to serialize, treating null as an empty string.</param>
    /// <returns>带引号的 CSV 字段 / A quoted CSV field.</returns>
    private static string EscapeCsvField(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// 检查 PNG 文件签名以拒绝明显错误的数据，不替代完整图片解码验证。
    /// Check the PNG signature to reject obviously incorrect data, without claiming full image validation.
    /// </summary>
    /// <param name="data">待检查的已编码图片数据 / The encoded image data to check.</param>
    /// <returns>包含 PNG 签名时为 true / True if the data starts with a PNG signature.</returns>
    private static bool HasPngSignature(byte[] data)
    {
        if (data == null || data.Length < PngSignature.Length) return false;
        for (var index = 0; index < PngSignature.Length; index++)
            if (data[index] != PngSignature[index]) return false;
        return true;
    }
}
