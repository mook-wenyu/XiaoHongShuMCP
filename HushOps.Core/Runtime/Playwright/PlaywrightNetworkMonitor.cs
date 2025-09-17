using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using HushOps.Core.Automation.Abstractions;

namespace HushOps.Core.Runtime.Playwright;

/// <summary>
/// 基于 Playwright 的网络监听实现，将响应标准化为 INetworkResponse。
/// </summary>
public sealed class PlaywrightNetworkMonitor : INetworkMonitor
{
    private IPage? _page;
    private readonly List<IWebSocket> _sockets = new();
    // 记录最近一次相同 URL 的请求发起时间，用于粗粒度 RTT 估算
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _requestStartUtc = new(StringComparer.Ordinal);
    public event Action<INetworkEvent>? Event;

    public async Task BindAsync(IAutoPage autoPage, CancellationToken ct = default)
    {
        var page = PlaywrightAutoFactory.TryUnwrap(autoPage);
        if (page == null) throw new ArgumentException("BindAsync 仅支持 Playwright IAutoPage 实例");
        _page = page;
        _page.Request += OnPageRequest;
        _page.Response += OnPageResponse;
        _page.WebSocket += OnPageWebSocket;
        await Task.CompletedTask;
    }

    public async Task UnbindAsync(CancellationToken ct = default)
    {
        if (_page != null)
        {
            _page.Request -= OnPageRequest;
            _page.Response -= OnPageResponse;
            _page.WebSocket -= OnPageWebSocket;
        }
        // 解绑所有已订阅的 WebSocket 事件
        foreach (var ws in _sockets)
        {
            try
            {
                ws.FrameReceived -= OnWsFrameReceived;
                ws.FrameSent -= OnWsFrameSent;
                ws.Close -= OnWsClose;
            }
            catch { }
        }
        _sockets.Clear();
        _page = null;
        await Task.CompletedTask;
    }

    private void OnPageResponse(object? sender, IResponse resp)
    {
        try
        {
            var std = new StdResponse(resp);
            var now = DateTime.UtcNow;
            double? rtt = null;
            try
            {
                var url = resp.Url;
                if (!string.IsNullOrEmpty(url) && _requestStartUtc.TryGetValue(url, out var start))
                {
                    rtt = Math.Max(0, (now - start).TotalMilliseconds);
                }
            }
            catch { }
            Event?.Invoke(new StdHttpEvent(std, now, rtt));
        }
        catch { }
    }

    private void OnPageRequest(object? sender, IRequest req)
    {
        try
        {
            var url = req.Url;
            if (!string.IsNullOrEmpty(url)) _requestStartUtc[url] = DateTime.UtcNow;
        }
        catch { }
    }

    private void OnPageWebSocket(object? sender, IWebSocket ws)
    {
        // 订阅帧事件（收/发）与关闭
        try
        {
            _sockets.Add(ws);
            ws.FrameReceived += OnWsFrameReceived;
            ws.FrameSent += OnWsFrameSent;
            ws.Close += OnWsClose;
        }
        catch { }
    }

    private void OnWsClose(object? sender, IWebSocket ws)
    {
        // 移除跟踪并解除订阅
        _sockets.Remove(ws);
        try
        {
            ws.FrameReceived -= OnWsFrameReceived;
            ws.FrameSent -= OnWsFrameSent;
            ws.Close -= OnWsClose;
        }
        catch { }
    }

    private void OnWsFrameReceived(object? sender, IWebSocketFrame frame)
    {
        try
        {
            if (sender is IWebSocket ws)
            {
                var text = SafeGetWsText(frame);
                var ev = new StdWsEvent(ws.Url, NetworkDirection.Inbound, text);
                Event?.Invoke(ev);
            }
        }
        catch { }
    }

    private void OnWsFrameSent(object? sender, IWebSocketFrame frame)
    {
        try
        {
            if (sender is IWebSocket ws)
            {
                var text = SafeGetWsText(frame);
                var ev = new StdWsEvent(ws.Url, NetworkDirection.Outbound, text);
                Event?.Invoke(ev);
            }
        }
        catch { }
    }

    private static string SafeGetWsText(IWebSocketFrame frame)
    {
        try
        {
            // 文本帧优先；二进制转换为 Base64 以控制体积与避免编码问题
            var t = frame.Text;
            if (!string.IsNullOrEmpty(t)) return Truncate(t, 4096);
            var bin = frame.Binary;
            if (bin is { Length: > 0 }) return "base64:" + Convert.ToBase64String(bin.AsSpan(0, Math.Min(2048, bin.Length)).ToArray());
        }
        catch { }
        return string.Empty;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max);

    public void Dispose()
    {
        // 自动解除订阅
        if (_page != null)
        {
            _page.Response -= OnPageResponse;
            _page.WebSocket -= OnPageWebSocket;
        }
        foreach (var ws in _sockets)
        {
            try
            {
                ws.FrameReceived -= OnWsFrameReceived;
                ws.FrameSent -= OnWsFrameSent;
                ws.Close -= OnWsClose;
            }
            catch { }
        }
        _sockets.Clear();
        _page = null;
    }

    private sealed class StdResponse : INetworkResponse
    {
        private readonly IResponse r;
        public StdResponse(IResponse r) => this.r = r;
        public string Url => r.Url;
        public int Status => r.Status;
        public async Task<string> ReadBodyAsync(CancellationToken ct = default)
        {
            try
            {
                var bytes = await r.BodyAsync();
                return Encoding.UTF8.GetString(bytes);
            }
            catch { return string.Empty; }
        }
    }

    /// <summary>
    /// HTTP 事件实现：不主动读取正文，由上层（如 UAM）按需调用 <see cref="INetworkResponse.ReadBodyAsync"/>。
    /// </summary>
    private sealed class StdHttpEvent : INetworkEvent
    {
        private readonly INetworkResponse resp;
        public StdHttpEvent(INetworkResponse r, DateTime ts, double? rttMs) { resp = r; TimestampUtc = ts; RttMs = rttMs; }
        public NetworkEventKind Kind => NetworkEventKind.HttpResponse;
        public string Url => resp.Url;
        public int? Status => resp.Status;
        public NetworkDirection? Direction => null;
        public string? Payload => null;
        public INetworkResponse? Http => resp;
        public DateTime TimestampUtc { get; }
        public double? RttMs { get; }
    }

    /// <summary>
    /// WebSocket 事件实现：仅承载方向、URL 与文本帧（或二进制转 Base64）。
    /// </summary>
    private sealed class StdWsEvent : INetworkEvent
    {
        public StdWsEvent(string url, NetworkDirection direction, string? payload)
        { Url = url; Direction = direction; Payload = payload; }
        public NetworkEventKind Kind => NetworkEventKind.WebSocketFrame;
        public string Url { get; }
        public int? Status => null;
        public NetworkDirection? Direction { get; }
        public string? Payload { get; }
        public INetworkResponse? Http => null;
        public DateTime TimestampUtc { get; } = DateTime.UtcNow;
        public double? RttMs => null;
    }
}
