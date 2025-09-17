using System.Threading;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace XiaoHongShuMCP.Tooling;

/// <summary>
/// 工具经纪接口：负责暴露可供 MCP 客户端发现的工具集合，并代理实际调用以确保拟人化自动化能力集中呈现。
/// </summary>
public interface IToolBroker
{
    /// <summary>
    /// 列出当前可用的 MCP 工具，支持根据请求游标与上限进行裁剪。
    /// </summary>
    ValueTask<ListToolsResult> ListToolsAsync(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken);

    /// <summary>
    /// 调度执行指定工具，并返回标准的工具调用结果。
    /// </summary>
    ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken);
}
