using ModelContextProtocol.Protocol;

namespace HushOps.Servers.XiaoHongShu.Tests.Logging;

public sealed class McpLoggingStateTests
{
    [Fact]
    public void ShouldEmit_RespectsConfiguredLevel()
    {
        var state = new McpLoggingState();
        state.SetLevel(LoggingLevel.Warning);

        Assert.False(state.ShouldEmit(LogLevel.Information));
        Assert.True(state.ShouldEmit(LogLevel.Warning));
        Assert.True(state.ShouldEmit(LogLevel.Error));
    }

    [Fact]
    public void Reset_DisablesEmission()
    {
        var state = new McpLoggingState();
        state.SetLevel(LoggingLevel.Info);
        Assert.True(state.ShouldEmit(LogLevel.Warning));

        state.Reset();

        Assert.False(state.ShouldEmit(LogLevel.Warning));
        Assert.Null(state.CurrentLevel);
    }
}
