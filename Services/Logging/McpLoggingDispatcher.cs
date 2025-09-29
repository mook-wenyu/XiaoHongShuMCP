using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace HushOps.Servers.XiaoHongShu.Services.Logging;

internal sealed class McpLoggingDispatcher : BackgroundService
{
    private readonly ChannelReader<McpLogEntry> _reader;
    private readonly IMcpLoggingNotificationSender _sender;
    private readonly IMcpLoggingState _state;
    private readonly ILogger<McpLoggingDispatcher> _logger;

    public McpLoggingDispatcher(
        ChannelReader<McpLogEntry> reader,
        IMcpLoggingNotificationSender sender,
        IMcpLoggingState state,
        ILogger<McpLoggingDispatcher> logger)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in _reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            if (!_state.ShouldEmit(entry.Level))
            {
                continue;
            }

            var payload = BuildPayload(entry);

            try
            {
                await _sender.SendAsync(payload, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "推送 MCP 日志通知失败，分类：{Category}", entry.Category);
            }
        }
    }

    private static LoggingMessageNotificationParams BuildPayload(McpLogEntry entry)
    {
        var eventInfo = new
        {
            entry.EventId.Id,
            entry.EventId.Name
        };

        var scope = NormalizePairs(entry.ScopeValues);
        var state = NormalizePairs(entry.StateValues);

        var data = JsonSerializer.SerializeToElement(new
        {
            entry.Message,
            entry.Category,
            Event = eventInfo,
            entry.Timestamp,
            Exception = entry.Exception?.ToString(),
            Scope = scope,
            State = state
        });

        return new LoggingMessageNotificationParams
        {
            Level = McpLoggingLevelConverter.ToProtocol(entry.Level),
            Logger = entry.Category,
            Data = data
        };
    }

    private static IReadOnlyList<Dictionary<string, string?>>? NormalizePairs(IReadOnlyList<KeyValuePair<string, string?>>? pairs)
    {
        if (pairs is null || pairs.Count == 0)
        {
            return null;
        }

        var result = new List<Dictionary<string, string?>>(pairs.Count);
        foreach (var pair in pairs)
        {
            result.Add(new Dictionary<string, string?>(2)
            {
                ["key"] = pair.Key,
                ["value"] = pair.Value
            });
        }

        return result;
    }
}
