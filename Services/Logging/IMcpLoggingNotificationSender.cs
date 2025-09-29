using System;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Services.Logging;

internal interface IMcpLoggingNotificationSender
{
    Task SendAsync(LoggingMessageNotificationParams payload, CancellationToken cancellationToken);
}

internal sealed class McpServerLoggingNotificationSender : IMcpLoggingNotificationSender
{
    private readonly McpServer _server;

    public McpServerLoggingNotificationSender(McpServer server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    public Task SendAsync(LoggingMessageNotificationParams payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return _server.SendNotificationAsync(
            NotificationMethods.LoggingMessageNotification,
            payload,
            cancellationToken: cancellationToken);
    }
}
