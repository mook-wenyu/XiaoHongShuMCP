using HushOps.Core.Automation.Abstractions;

namespace HushOps.Core.Networking;

/// <summary>
/// 端点分类器：根据统一网络事件（HTTP/WS/Worker）归类到低基数端点键（如 "Homefeed"、"Feed"）。
/// - 要求：返回值必须低基数、稳定；未知返回 null。
/// - 注意：实现应避免依赖高基数自由文本，通常基于 URL 结构、状态码与小型文本体征进行判断。
/// </summary>
public interface IEndpointClassifier
{
    /// <summary>
    /// 将统一事件映射为端点键，未知返回 null。
    /// </summary>
    /// <param name="kind">事件类型（HTTP/WS/Worker）。</param>
    /// <param name="url">相关 URL。</param>
    /// <param name="status">HTTP 状态码（仅 HTTP）。</param>
    /// <param name="payload">小型载荷（HTTP 可为空，WS/Worker 为文本帧/消息）。</param>
    /// <param name="direction">方向（WS/Worker 可用）。</param>
    string? Classify(NetworkEventKind kind, string url, int? status, string? payload, NetworkDirection? direction);
}
