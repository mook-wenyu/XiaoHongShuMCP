using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace HushOps.Servers.XiaoHongShu.Tests.Logging;

public sealed class McpLoggingProviderTests
{
    [Fact]
    public async Task Log_WritesEntry_WhenLevelEnabled()
    {
        var channel = Channel.CreateUnbounded<McpLogEntry>();
        var state = new TestLoggingState();
        state.ForceEnabled(true);

        var provider = new McpLoggingProvider(
            channel.Writer,
            state,
            new DefaultMcpLogSanitizer(),
            Options.Create(new McpLoggingOptions { IncludeScopes = true }));

        var scopeProvider = new LoggerExternalScopeProvider();
        provider.SetScopeProvider(scopeProvider);

        var logger = provider.CreateLogger("Tests");

        using (logger.BeginScope(new Dictionary<string, object?> { ["traceId"] = "token=abc" }))
        {
            logger.LogWarning("token=abcd1234");
        }

        var entry = await channel.Reader.ReadAsync();

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Tests", entry.Category);
        Assert.Equal("token=[REDACTED]", entry.Message);

        var scope = Assert.Single(entry.ScopeValues!);
        Assert.Equal("traceId", scope.Key);
        Assert.Equal("token=[REDACTED]", scope.Value);
    }

    [Fact]
    public void Log_DoesNotWrite_WhenLevelDisabled()
    {
        var channel = Channel.CreateUnbounded<McpLogEntry>();
        var state = new TestLoggingState();
        state.ForceEnabled(false);

        var provider = new McpLoggingProvider(
            channel.Writer,
            state,
            new DefaultMcpLogSanitizer(),
            Options.Create(new McpLoggingOptions()));

        provider.SetScopeProvider(new LoggerExternalScopeProvider());

        var logger = provider.CreateLogger("Tests");
        logger.LogCritical("api_key=abc");

        Assert.False(channel.Reader.TryRead(out _));
    }

    private sealed class TestLoggingState : IMcpLoggingState
    {
        private bool _enabled;

        public LoggingLevel? CurrentLevel { get; private set; }

        public LogLevel EffectiveLogLevel => _enabled ? LogLevel.Information : LogLevel.None;

        public void Reset()
        {
            _enabled = false;
            CurrentLevel = null;
        }

        public void SetLevel(LoggingLevel level)
        {
            _enabled = true;
            CurrentLevel = level;
        }

        public bool ShouldEmit(LogLevel level) => _enabled;

        public void ForceEnabled(bool enabled)
        {
            _enabled = enabled;
            CurrentLevel = enabled ? LoggingLevel.Info : null;
        }
    }
}
