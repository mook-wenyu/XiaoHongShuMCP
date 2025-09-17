using System;
using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Core.Automation.Abstractions
{
    /// <summary>
    /// 网络事件种类（统一事件总线）。
    /// </summary>
    public enum NetworkEventKind
    {
        /// <summary>HTTP 响应事件。</summary>
        HttpResponse,
        /// <summary>WebSocket 帧事件（收/发）。</summary>
        WebSocketFrame,
        /// <summary>Worker 消息事件（预留）。</summary>
        WorkerMessage
    }

    /// <summary>
    /// 事件方向（仅当 Kind=WebSocketFrame 或 WorkerMessage 时生效）。
    /// </summary>
    public enum NetworkDirection
    {
        /// <summary>入站（从远端到页面）。</summary>
        Inbound,
        /// <summary>出站（从页面到远端）。</summary>
        Outbound
    }

    /// <summary>
    /// 网络响应抽象。平台无关，仅暴露通用字段与读取正文的方法。
    /// </summary>
    public interface INetworkResponse
    {
        /// <summary>响应的完整 URL。</summary>
        string Url { get; }

        /// <summary>HTTP 状态码。</summary>
        int Status { get; }

        /// <summary>
        /// 读取响应正文字符串（实现可选择缓存）。
        /// </summary>
        Task<string> ReadBodyAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// 统一网络事件：覆盖 HTTP/WS/Worker 等多种网络相关事件，作为被动监听的唯一通道。
    /// - 对于 HTTP：<see cref="Kind"/>=HttpResponse，<see cref="Status"/> 有值，<see cref="Http"/> 不为 null，<see cref="Payload"/> 可为空（由上层决定是否读取正文）。
    /// - 对于 WebSocket：<see cref="Kind"/>=WebSocketFrame，<see cref="Direction"/> 指示收/发，<see cref="Payload"/> 为文本帧内容（二进制可按需要转码为 Base64）。
    /// - 对于 Worker：<see cref="Kind"/>=WorkerMessage，<see cref="Payload"/> 为消息文本（或小型 JSON 字符串）。
    /// 设计目标：低基数、只读、可观测，禁止在业务层进行脚本注入或主动请求修改。
    /// </summary>
    public interface INetworkEvent
    {
        /// <summary>事件种类。</summary>
        NetworkEventKind Kind { get; }
        /// <summary>关联的 URL（HTTP 为请求 URL，WS 为握手 URL，Worker 为脚本 URL 或消息源）。</summary>
        string Url { get; }
        /// <summary>HTTP 状态码（仅 Kind=HttpResponse 时有值）。</summary>
        int? Status { get; }
        /// <summary>方向（仅 WS/Worker 生效）。</summary>
        NetworkDirection? Direction { get; }
        /// <summary>小型载荷：HTTP 可为空或为截断正文；WS/Worker 为文本帧/消息（低基数约束）。</summary>
        string? Payload { get; }
        /// <summary>HTTP 事件的响应对象（用于延迟读取正文，避免在监听线程中阻塞）。</summary>
        INetworkResponse? Http { get; }
        /// <summary>事件时间（UTC）。</summary>
        DateTime TimestampUtc { get; }
        /// <summary>
        /// 估算往返时延（毫秒，可为空）。
        /// - 实现依据平台能力估算（例如 Playwright 的 Request/Response 事件时间差或同 URL 的发送-响应时间差）。
        /// - 仅 Kind=HttpResponse 时可能有值；Ws/Worker 为 null。
        /// - 为“粗粒度可观测”设计，非高精基准。
        /// </summary>
        double? RttMs { get; }
    }

    /// <summary>
    /// 网络监听抽象。负责将底层驱动的网络事件归一化为 <see cref="INetworkEvent"/> 并通过事件发出。
    /// </summary>
    public interface INetworkMonitor : IDisposable
    {
        /// <summary>
        /// 监听到任何网络事件时触发；实现应在捕获到底层事件后尽量快速地发出，避免阻塞驱动线程。
        /// </summary>
        event Action<INetworkEvent>? Event;

        /// <summary>
        /// 绑定到抽象页面以开始监听对应上下文的网络事件。
        /// </summary>
        Task BindAsync(IAutoPage page, CancellationToken ct = default);

        /// <summary>
        /// 解绑并停止监听。
        /// </summary>
        Task UnbindAsync(CancellationToken ct = default);
    }
}
