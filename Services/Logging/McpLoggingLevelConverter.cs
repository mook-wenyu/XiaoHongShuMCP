using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace HushOps.Servers.XiaoHongShu.Services.Logging;

internal static class McpLoggingLevelConverter
{
    public static LoggingLevel ToProtocol(LogLevel level) => level switch
    {
        LogLevel.Trace => LoggingLevel.Debug,
        LogLevel.Debug => LoggingLevel.Debug,
        LogLevel.Information => LoggingLevel.Info,
        LogLevel.Warning => LoggingLevel.Warning,
        LogLevel.Error => LoggingLevel.Error,
        LogLevel.Critical => LoggingLevel.Critical,
        _ => LoggingLevel.Debug
    };

    public static LogLevel ToLogLevel(LoggingLevel level) => level switch
    {
        LoggingLevel.Debug => LogLevel.Debug,
        LoggingLevel.Info => LogLevel.Information,
        LoggingLevel.Notice => LogLevel.Information,
        LoggingLevel.Warning => LogLevel.Warning,
        LoggingLevel.Error => LogLevel.Error,
        LoggingLevel.Critical => LogLevel.Critical,
        LoggingLevel.Alert => LogLevel.Critical,
        LoggingLevel.Emergency => LogLevel.Critical,
        _ => LogLevel.Information
    };

    public static bool ShouldEmit(LogLevel level, LoggingLevel? threshold)
    {
        if (threshold is null)
        {
            return false;
        }

        var mapped = ToProtocol(level);
        return mapped >= threshold.Value;
    }
}
