using System.Collections.Generic;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// API 网络统计快照。
/// </summary>
public sealed class NetworkStats
{
    /// <summary>采集窗口内的响应总数。</summary>
    public long TotalResponses { get; init; }

    /// <summary>2xx 成功数量。</summary>
    public long Success2xx { get; init; }

    /// <summary>HTTP 429 数量。</summary>
    public long Http429 { get; init; }

    /// <summary>HTTP 403 数量。</summary>
    public long Http403 { get; init; }

    /// <summary>验证码或滑块提示次数。</summary>
    public long CaptchaHints { get; init; }

    /// <summary>各端点的命中统计。</summary>
    public Dictionary<ApiEndpointType, long> EndpointHits { get; init; } = new();
}
