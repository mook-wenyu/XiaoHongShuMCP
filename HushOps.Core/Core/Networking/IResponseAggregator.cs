using HushOps.Core.Automation.Abstractions;

namespace HushOps.Core.Networking;

/// <summary>
/// 事件聚合器：对被动监听到的统一网络事件执行“分类→去重→窗口化聚合”。
/// - 分类：由 <see cref="IEndpointClassifier"/> 提供低基数端点键；
/// - 去重：对同一端点内使用可配置的去重键（默认 url+payload 哈希）；
/// - 窗口：仅保留最近窗口内的数据（默认 2 分钟）。
/// </summary>
public interface IResponseAggregator
{
    /// <summary>处理一个统一网络事件。</summary>
    void OnEvent(INetworkEvent ev, IEndpointClassifier classifier);

    /// <summary>按端点键获取当前窗口内的响应快照。</summary>
    IReadOnlyList<AggregatedResponse> GetResponses(string endpointKey);

    /// <summary>清理指定端点的当前窗口内聚合数据。</summary>
    void Clear(string? endpointKey = null);
}

/// <summary>
/// 聚合后的事件快照（低基数字段）。
/// </summary>
public sealed class AggregatedResponse
{
    /// <summary>事件类型（HTTP/WS/Worker）。</summary>
    public NetworkEventKind Kind { get; init; }
    /// <summary>端点键（低基数）。</summary>
    public string Endpoint { get; init; } = string.Empty;
    /// <summary>关联 URL（HTTP 请求 URL / WS 握手 URL）。</summary>
    public string Url { get; init; } = string.Empty;
    /// <summary>HTTP 状态码（仅 HTTP）。</summary>
    public int? Status { get; init; }
    /// <summary>方向（仅 WS/Worker）。</summary>
    public NetworkDirection? Direction { get; init; }
    /// <summary>小型载荷（HTTP 可为空；WS/Worker 为文本帧/消息）。</summary>
    public string Payload { get; init; } = string.Empty;
    /// <summary>时间戳（UTC）。</summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
