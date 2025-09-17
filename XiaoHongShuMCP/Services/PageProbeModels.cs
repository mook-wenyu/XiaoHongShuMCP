using System.Collections.Generic;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 页面探测（服务层返回模型）：包含总体结果、URL、HTML 采样与别名命中明细。
/// </summary>
public sealed record PageProbeResult(
    bool Success,
    string Url,
    string HtmlSample,
    IReadOnlyList<PageProbeAliasDetail> Aliases,
    string Message
);

/// <summary>
/// 单个选择器别名的探测结果：首个命中的选择器、匹配数量与首个匹配元素的安全标记采样。
/// </summary>
public sealed record PageProbeAliasDetail(
    string Alias,
    string? FirstMatchedSelector,
    int MatchCount,
    string? FirstMarkupSample
);
