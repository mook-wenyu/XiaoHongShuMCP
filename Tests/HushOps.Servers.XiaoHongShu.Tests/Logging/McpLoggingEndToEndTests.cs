using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Tests.Logging;

public sealed class McpLoggingEndToEndTests
{
    [Fact]
    public async Task LoggingBridge_EmitsSanitizedNotification_AfterSetLevel()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var clientToServer = Channel.CreateUnbounded<JsonRpcMessage>();
        var serverToClient = Channel.CreateUnbounded<JsonRpcMessage>();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders());
        services.AddMcpLoggingBridge();

        var notificationSender = new TestLoggingNotificationSender(serverToClient.Writer);
        services.AddSingleton<IMcpLoggingNotificationSender>(notificationSender);

        await using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetServices<IHostedService>().OfType<McpLoggingDispatcher>().Single();
        await dispatcher.StartAsync(cts.Token);

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = "TestServer",
                Title = "Test Server",
                Version = "1.0.0"
            },
            Capabilities = new ServerCapabilities
            {
                Logging = new LoggingCapability()
            },
            Handlers = new McpServerHandlers()
        };
        options.Handlers.SetLoggingLevelHandler = McpLoggingHandlers.HandleSetLoggingLevelAsync;

        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var transport = new InMemoryTransport("session-1", clientToServer.Reader, serverToClient.Writer);
        await using var server = McpServer.Create(transport, options, loggerFactory, provider);

        var serverTask = RunServerAsync(server, cts.Token);

        await SendInitializeAsync(clientToServer.Writer, requestId: 1, cts.Token);
        var notifications = new List<JsonRpcNotification>();
        var initializeResponse = await WaitForResponseAsync(serverToClient.Reader, expectedRequestId: 1, notifications, cts.Token);
        var initializeResult = initializeResponse.Result.Deserialize<InitializeResult>();
        Assert.NotNull(initializeResult);
        Assert.NotNull(initializeResult!.Capabilities?.Logging);

        await SendSetLevelAsync(clientToServer.Writer, requestId: 2, LoggingLevel.Info, cts.Token);
        var setLevelResponse = await WaitForResponseAsync(serverToClient.Reader, expectedRequestId: 2, notifications, cts.Token);
        var responseId = Assert.IsType<long>(setLevelResponse.Id.Id);
        Assert.Equal(2L, responseId);

        var state = provider.GetRequiredService<IMcpLoggingState>();
        Assert.Equal(LoggingLevel.Info, state.CurrentLevel);

        var logger = provider.GetRequiredService<ILogger<McpLoggingEndToEndTests>>();
        logger.LogWarning("api_key=super-secret");

        var payload = await WaitForPayloadAsync(notificationSender.PayloadReader, typeof(McpLoggingEndToEndTests).FullName!, cts.Token);
        Assert.Equal(LoggingLevel.Warning, payload.Level);
        Assert.True(payload.Data.HasValue);
        Assert.Equal("api_key=[REDACTED]", payload.Data.Value.GetProperty("Message").GetString());

        var notification = await WaitForNotificationAsync(serverToClient.Reader, notifications, cts.Token);
        Assert.Equal(NotificationMethods.LoggingMessageNotification, notification.Method);

        cts.Cancel();

        clientToServer.Writer.TryComplete();
        serverToClient.Writer.TryComplete();

        await dispatcher.StopAsync(CancellationToken.None);

        try
        {
            await serverTask;
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
    }

    private static async Task RunServerAsync(McpServer server, CancellationToken cancellationToken)
    {
        try
        {
            await server.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // expected when test cancels token
        }
    }

    private static async Task SendInitializeAsync(ChannelWriter<JsonRpcMessage> writer, long requestId, CancellationToken cancellationToken)
    {
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = new RequestId(requestId),
            Method = RequestMethods.Initialize,
            Params = JsonSerializer.SerializeToNode(new InitializeRequestParams
            {
                ProtocolVersion = "2024-11-05",
                ClientInfo = new Implementation
                {
                    Name = "TestClient",
                    Version = "1.0.0"
                }
            })
        };

        await writer.WriteAsync(request, cancellationToken);
    }

    private static async Task SendSetLevelAsync(ChannelWriter<JsonRpcMessage> writer, long requestId, LoggingLevel level, CancellationToken cancellationToken)
    {
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = new RequestId(requestId),
            Method = RequestMethods.LoggingSetLevel,
            Params = JsonSerializer.SerializeToNode(new SetLevelRequestParams
            {
                Level = level
            })
        };

        await writer.WriteAsync(request, cancellationToken);
    }

    private static async Task<JsonRpcResponse> WaitForResponseAsync(
        ChannelReader<JsonRpcMessage> reader,
        long expectedRequestId,
        ICollection<JsonRpcNotification> notificationSink,
        CancellationToken cancellationToken)
    {
        while (await reader.WaitToReadAsync(cancellationToken))
        {
            var message = await reader.ReadAsync(cancellationToken);
            switch (message)
            {
                case JsonRpcResponse response when response.Id.Id is long id && id == expectedRequestId:
                    return response;
                case JsonRpcNotification notification:
                    notificationSink.Add(notification);
                    continue;
            }
        }

        throw new InvalidOperationException("未接收到匹配的 JsonRpcResponse。");
    }

    private static async Task<JsonRpcNotification> WaitForNotificationAsync(
        ChannelReader<JsonRpcMessage> reader,
        IList<JsonRpcNotification> bufferedNotifications,
        CancellationToken cancellationToken)
    {
        if (bufferedNotifications.Count > 0)
        {
            var buffered = bufferedNotifications[0];
            bufferedNotifications.RemoveAt(0);
            return buffered;
        }

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            var message = await reader.ReadAsync(cancellationToken);
            if (message is JsonRpcNotification notification)
            {
                return notification;
            }
        }

        throw new InvalidOperationException("未接收到 JsonRpcNotification。");
    }

    private static async Task<LoggingMessageNotificationParams> WaitForPayloadAsync(
        ChannelReader<LoggingMessageNotificationParams> reader,
        string expectedLogger,
        CancellationToken cancellationToken)
    {
        while (await reader.WaitToReadAsync(cancellationToken))
        {
            var payload = await reader.ReadAsync(cancellationToken);
            if (string.Equals(payload.Logger, expectedLogger, StringComparison.Ordinal))
            {
                return payload;
            }
        }

        throw new InvalidOperationException("未接收到匹配的日志通知负载。");
    }

    private sealed class InMemoryTransport : ITransport, IAsyncDisposable
    {
        private readonly ChannelReader<JsonRpcMessage> _incomingReader;
        private readonly ChannelWriter<JsonRpcMessage> _outgoingWriter;

        public InMemoryTransport(string sessionId, ChannelReader<JsonRpcMessage> incomingReader, ChannelWriter<JsonRpcMessage> outgoingWriter)
        {
            SessionId = sessionId;
            _incomingReader = incomingReader;
            _outgoingWriter = outgoingWriter;
        }

        public string SessionId { get; }

        public ChannelReader<JsonRpcMessage> MessageReader => _incomingReader;

        public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
        {
            return _outgoingWriter.WriteAsync(message, cancellationToken).AsTask();
        }

        public ValueTask DisposeAsync()
        {
            _outgoingWriter.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestLoggingNotificationSender : IMcpLoggingNotificationSender
    {
        private readonly ChannelWriter<JsonRpcMessage> _jsonWriter;
        private readonly Channel<LoggingMessageNotificationParams> _payloadChannel = Channel.CreateUnbounded<LoggingMessageNotificationParams>();

        public TestLoggingNotificationSender(ChannelWriter<JsonRpcMessage> jsonWriter)
        {
            _jsonWriter = jsonWriter;
        }

        public ChannelReader<LoggingMessageNotificationParams> PayloadReader => _payloadChannel.Reader;

        public async Task SendAsync(LoggingMessageNotificationParams payload, CancellationToken cancellationToken)
        {
            await _payloadChannel.Writer.WriteAsync(payload, cancellationToken);

            JsonNode? node = JsonSerializer.SerializeToNode(payload);
            var notification = new JsonRpcNotification
            {
                JsonRpc = "2.0",
                Method = NotificationMethods.LoggingMessageNotification,
                Params = node
            };

            await _jsonWriter.WriteAsync(notification, cancellationToken);
        }
    }
}
