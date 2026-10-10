using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace CRC3D3.NotEnoughTranslator.Plugin.Utils;

/// <summary>
///     BepInEx 日志系统的包装，提供分级日志，并按配置过滤。
///     同时负责把过长消息切成多段，避免刷爆控制台。
/// </summary>
public static class LogManager
{
    /// <summary>单条控制台消息的最大长度</summary>
    private const int MaxConsoleMessageLength = 512;

    private static ManualLogSource _logSource;

    /// <summary>上一条 Debug 消息，用于抑制重复刷屏</summary>
    private static string _lastDebugMessage = "";

    private static LogLevel RootLogLevel =>
        NotEnoughTranslator.LogLevelConfig?.Value ?? LogLevel.Info;

    /// <summary>
    /// 初始化插件使用的 BepInEx 日志源。
    /// Initialize the BepInEx log source used by the plugin.
    /// </summary>
    /// <param name="logSource">接收插件日志的日志源 / The log source receiving plugin messages.</param>
    public static void Init(ManualLogSource logSource)
    {
        _logSource = logSource;
    }

    /// <summary>
    /// 按配置输出调试日志，并抑制与上次相同的调试消息。
    /// Write debug logs according to configuration, suppressing messages identical to the previous debug message.
    /// </summary>
    /// <param name="message">需要安全转换为文字的日志对象 / The log object to convert safely to text.</param>
    public static void Debug(object message)
    {
        if (RootLogLevel < LogLevel.Debug) return;

        var safeMessage = SafeToString(message);

        // 同一条消息连续出现时只打一次，避免刷屏
        if (safeMessage == _lastDebugMessage) return;

        SafeLog(LogLevel.Debug, safeMessage);
        _lastDebugMessage = safeMessage;
    }

    /// <summary>
    /// 当前日志等级允许时，安全输出信息日志。
    /// Safely write an informational log when the configured log level allows it.
    /// </summary>
    /// <param name="message">需要输出的日志对象 / The log object to write.</param>
    public static void Info(object message)
    {
        if (RootLogLevel < LogLevel.Info) return;

        SafeLog(LogLevel.Info, SafeToString(message));
    }

    /// <summary>
    /// 当前日志等级允许时，安全输出警告日志。
    /// Safely write a warning log when the configured log level allows it.
    /// </summary>
    /// <param name="message">需要输出的警告对象 / The warning object to write.</param>
    public static void Warning(object message)
    {
        if (RootLogLevel < LogLevel.Warning) return;

        SafeLog(LogLevel.Warning, SafeToString(message));
    }

    /// <summary>
    /// 当前日志等级允许时，安全输出错误日志。
    /// Safely write an error log when the configured log level allows it.
    /// </summary>
    /// <param name="message">需要输出的错误对象 / The error object to write.</param>
    public static void Error(object message)
    {
        if (RootLogLevel < LogLevel.Error) return;

        SafeLog(LogLevel.Error, SafeToString(message));
    }

    /// <summary>
    /// 安全地将对象转换为字符串，转换失败时返回诊断文字而不抛出异常。
    /// Convert an object to a string safely, returning diagnostic text instead of throwing on conversion failure.
    /// </summary>
    /// <param name="message">待转换的日志对象 / The log object to convert.</param>
    /// <returns>对象文字或表示空值、转换失败的诊断文字 / The object text or diagnostic text for null values or conversion failures.</returns>
    private static string SafeToString(object message)
    {
        try
        {
            return message?.ToString() ?? "<error>";
        }
        catch (Exception e)
        {
            return "<log message threw: " + e.Message + ">";
        }
    }

    /// <summary>
    /// 分段输出超长日志，并吞掉日志系统自身的异常以保护插件运行。
    /// Split long log messages and suppress logging-system exceptions to protect plugin execution.
    /// </summary>
    /// <param name="level">日志消息等级 / The log message level.</param>
    /// <param name="message">已转换为字符串的完整消息 / The complete message already converted to a string.</param>
    private static void SafeLog(LogLevel level, string message)
    {
        try
        {
            if (_logSource == null) return;

            var parts = SplitForConsole(message);
            if (parts.Count == 1)
            {
                LogSingle(level, parts[0]);
                return;
            }

            for (var i = 0; i < parts.Count; i++)
            {
                var prefix = "[" + (i + 1) + "/" + parts.Count + "] ";
                var part = parts[i];
                var maxPartLength = MaxConsoleMessageLength - prefix.Length;
                if (maxPartLength < 1) maxPartLength = 1;
                if (part.Length > maxPartLength)
                    part = part.Substring(0, maxPartLength);

                LogSingle(level, prefix + part);
            }
        }
        catch
        {
            // 日志本身出错不应影响插件运行
        }
    }

    /// <summary>
    /// 根据日志等级将单段消息发送到对应的 BepInEx 日志接口。
    /// Send one message segment to the BepInEx logging method for the specified level.
    /// </summary>
    /// <param name="level">消息使用的日志等级 / The log level for the message.</param>
    /// <param name="message">无需再切分的单段消息 / A single message segment requiring no further splitting.</param>
    private static void LogSingle(LogLevel level, string message)
    {
        switch (level)
        {
            case LogLevel.Debug:
                _logSource!.LogDebug(message);
                break;
            case LogLevel.Info:
                _logSource!.LogInfo(message);
                break;
            case LogLevel.Warning:
                _logSource!.LogWarning(message);
                break;
            case LogLevel.Error:
                _logSource!.LogError(message);
                break;
        }
    }

    /// <summary>
    /// 将消息切成不超过控制台长度上限的片段，切分时避免截断 UTF-16 代理对。
    /// Split a message into console-sized segments without breaking UTF-16 surrogate pairs at split boundaries.
    /// </summary>
    /// <param name="message">待切分的完整日志消息 / The complete log message to split.</param>
    /// <returns>至少包含一个元素的有序消息片段列表 / An ordered list of message segments containing at least one element.</returns>
    private static List<string> SplitForConsole(string message)
    {
        var parts = new List<string>();

        if (string.IsNullOrEmpty(message))
        {
            parts.Add(message ?? string.Empty);
            return parts;
        }

        var i = 0;
        while (i < message.Length)
        {
            var remaining = message.Length - i;
            var max = remaining > MaxConsoleMessageLength ? MaxConsoleMessageLength : remaining;
            var end = i + max;

            // 不要在代理对中间切开
            if (end < message.Length &&
                char.IsHighSurrogate(message[end - 1]) &&
                char.IsLowSurrogate(message[end]))
                end--;

            if (end <= i) end = i + max;
            if (end > message.Length) end = message.Length;

            parts.Add(message.Substring(i, end - i));
            i = end;
        }

        return parts;
    }
}
