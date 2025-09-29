using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModelContextProtocol;

namespace HushOps.Servers.XiaoHongShu.Services.Logging;

internal static class McpLoggingHandlers
{
    public static ValueTask<EmptyResult> HandleSetLoggingLevelAsync(
        RequestContext<SetLevelRequestParams> context,
        CancellationToken cancellationToken)
    {
        if (context.Params is null)
        {
            throw new McpException("logging/setLevel 请求缺少参数。");
        }

        var services = context.Services ?? context.Server.Services
            ?? throw new InvalidOperationException("请求上下文未提供服务容器");

        var state = services.GetRequiredService<IMcpLoggingState>();
        state.SetLevel(context.Params.Level);

        var loggerFactory = services.GetService<ILoggerFactory>();
        loggerFactory?.CreateLogger("McpLogging").LogInformation("MCP 客户端设置日志级别为 {Level}", context.Params.Level);

        return ValueTask.FromResult(new EmptyResult());
    }
}
