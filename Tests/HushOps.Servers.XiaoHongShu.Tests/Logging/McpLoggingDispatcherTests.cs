using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace HushOps.Servers.XiaoHongShu.Tests.Logging;

public sealed class McpLoggingDispatcherTests
{
    [Fact]
    public async Task ExecuteAsync_PublishesNotification_WhenThresholdAllows()
    {
        var channel = Channel.CreateUnbounded<McpLogEntry>();
        var sender = new TestNotificationSender();
        var state = new McpLoggingState();
        state.SetLevel(LoggingLevel.Debug);

        using var dispatcher = new McpLoggingDispatcher(
            channel.Reader,
            sender,
            state,
            NullLogger<McpLoggingDispatcher>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);

        var entry = new McpLogEntry(
            LogLevel.Warning,
            "Tests",
            new EventId(42, "sample"),
            "token=abcd123",
            null,
            DateTimeOffset.UtcNow,
            new List<KeyValuePair<string, string?>>()
            {
                new("traceId", "token=abcd123")
            },
            null);

        await channel.Writer.WriteAsync(entry);
        channel.Writer.TryComplete();

        var payload = await sender.WaitAsync();

        Assert.Equal(LoggingLevel.Warning, payload.Level);
        Assert.Equal("Tests", payload.Logger);

        Assert.True(payload.Data.HasValue);
        var data = payload.Data.Value;
        Assert.Equal("token=abcd123", data.GetProperty("Message").GetString());

        await dispatcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsNotification_WhenThresholdBlocks()
    {
        var channel = Channel.CreateUnbounded<McpLogEntry>();
        var sender = new TestNotificationSender();
        var state = new McpLoggingState();

        using var dispatcher = new McpLoggingDispatcher(
            channel.Reader,
            sender,
            state,
            NullLogger<McpLoggingDispatcher>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);

        var entry = new McpLogEntry(
            LogLevel.Information,
            "Tests",
            default,
            "skipped",
            null,
            DateTimeOffset.UtcNow,
            null,
            null);

        await channel.Writer.WriteAsync(entry);
        channel.Writer.TryComplete();

        var hasNotification = await sender.WaitAsync(TimeSpan.FromMilliseconds(200));

        Assert.False(hasNotification);

        await dispatcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Integration_ILoggerPipeline_ForwardsSanitizedPayload()
    {
        var sender = new TestNotificationSender();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders());
        services.AddMcpLoggingBridge(options => options.QueueCapacity = 8);
        services.AddSingleton<IMcpLoggingNotificationSender>(sender);

        await using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetServices<IHostedService>()
            .OfType<McpLoggingDispatcher>()
            .Single();

        await dispatcher.StartAsync(CancellationToken.None);

        var state = provider.GetRequiredService<IMcpLoggingState>();
        state.SetLevel(LoggingLevel.Info);

        var logger = provider.GetRequiredService<ILogger<McpLoggingDispatcherTests>>();
        using (logger.BeginScope(new Dictionary<string, object?> { ["secret"] = "token=abcd" }))
        {
            logger.LogWarning("token=abcd");
        }

        var payload = await sender.WaitAsync();

        Assert.True(payload.Data.HasValue);
        var data = payload.Data.Value;
        Assert.Equal("token=[REDACTED]", data.GetProperty("Message").GetString());

        await dispatcher.StopAsync(CancellationToken.None);
    }

    private sealed class TestNotificationSender : IMcpLoggingNotificationSender
    {
        private readonly TaskCompletionSource<LoggingMessageNotificationParams> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendAsync(LoggingMessageNotificationParams payload, CancellationToken cancellationToken)
        {
            _tcs.TrySetResult(payload);
            return Task.CompletedTask;
        }

        public async Task<LoggingMessageNotificationParams> WaitAsync()
        {
            return await _tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }

        public async Task<bool> WaitAsync(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);

            try
            {
                await _tcs.Task.WaitAsync(cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
