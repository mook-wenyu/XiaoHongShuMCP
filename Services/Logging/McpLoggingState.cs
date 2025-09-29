using System;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace HushOps.Servers.XiaoHongShu.Services.Logging;

internal interface IMcpLoggingState
{
    LoggingLevel? CurrentLevel { get; }

    LogLevel EffectiveLogLevel { get; }

    void SetLevel(LoggingLevel level);

    void Reset();

    bool ShouldEmit(LogLevel level);
}

internal sealed class McpLoggingState : IMcpLoggingState
{
    private readonly object _gate = new();
    private LoggingLevel? _currentLevel;

    public LoggingLevel? CurrentLevel
    {
        get
        {
            lock (_gate)
            {
                return _currentLevel;
            }
        }
    }

    public LogLevel EffectiveLogLevel
    {
        get
        {
            var snapshot = CurrentLevel;
            return snapshot is null ? LogLevel.None : McpLoggingLevelConverter.ToLogLevel(snapshot.Value);
        }
    }

    public void SetLevel(LoggingLevel level)
    {
        lock (_gate)
        {
            _currentLevel = level;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _currentLevel = null;
        }
    }

    public bool ShouldEmit(LogLevel level)
    {
        LoggingLevel? snapshot;
        lock (_gate)
        {
            snapshot = _currentLevel;
        }

        return McpLoggingLevelConverter.ShouldEmit(level, snapshot);
    }
}
