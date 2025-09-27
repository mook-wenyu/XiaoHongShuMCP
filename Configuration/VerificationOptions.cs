using System.ComponentModel.DataAnnotations;

namespace HushOps.Servers.XiaoHongShu.Configuration;

/// <summary>
/// 中文：验证场景配置，允许自定义示例流程使用的状态码端点。
/// English: Configuration for verification scenarios, enabling a custom status endpoint.
/// </summary>
public sealed class VerificationOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "verification";

    /// <summary>
    /// 中文：用于触发缓解逻辑的状态码端点（默认 https://httpbin.org/status/429）。
    /// English: Endpoint that returns a throttling status code (defaults to https://httpbin.org/status/429).
    /// </summary>
    [Url]
    public string? StatusUrl { get; init; }

    /// <summary>
    /// 中文：可选的模拟状态码；设置后将拦截请求并返回该状态码。
    /// English: Optional mock status code; when set, the request is intercepted and fulfilled locally.
    /// </summary>
    [Range(100, 599)]
    public int? MockStatusCode { get; init; }
}
