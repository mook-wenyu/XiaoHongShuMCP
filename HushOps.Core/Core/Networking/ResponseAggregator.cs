using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Observability;

namespace HushOps.Core.Networking;

/// <summary>
/// 默认响应聚合器：窗口内（默认2分钟）按端点键去重聚合。
/// </summary>
public sealed class ResponseAggregator : IResponseAggregator
{
    private readonly TimeSpan window;
    private readonly ConcurrentDictionary<string, List<(string DedupKey, AggregatedResponse Item)>> store = new(StringComparer.Ordinal);
    private readonly IMetrics? _metrics;
    private readonly ICounter? _cTotal;
    private readonly IHistogram? _hWindowItems;

    public ResponseAggregator(TimeSpan? window = null, IMetrics? metrics = null)
    {
        this.window = window ?? TimeSpan.FromMinutes(2);
        _metrics = metrics;
        if (_metrics != null)
        {
            _cTotal = _metrics.CreateCounter("net_aggregated_total", "被动网络聚合计数");
            _hWindowItems = _metrics.CreateHistogram("net_window_items", "端点窗口内项数");
        }
    }

    /// <summary>
    /// 处理统一网络事件：分类→窗口化去重→计数。
    /// </summary>
    public void OnEvent(INetworkEvent ev, IEndpointClassifier classifier)
    {
        var url = ev.Url ?? string.Empty;
        var status = ev.Status;
        var payload = ev.Payload;
        var endpoint = classifier.Classify(ev.Kind, url, status, payload, ev.Direction) ?? "unknown";
        var now = ev.TimestampUtc == default ? DateTime.UtcNow : ev.TimestampUtc;
        var item = new AggregatedResponse
        {
            Kind = ev.Kind,
            Endpoint = endpoint,
            Url = url,
            Status = status,
            Direction = ev.Direction,
            Payload = payload ?? string.Empty,
            TimestampUtc = now
        };
        var dedup = MakeKey(url, payload);
        var list = store.GetOrAdd(endpoint, _ => new List<(string, AggregatedResponse)>());
        lock (list)
        {
            // 清理过期
            var cutoff = now - window;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Item.TimestampUtc < cutoff) list.RemoveAt(i);
            }
            // 去重
            if (list.Any(t => t.DedupKey == dedup)) return;
            list.Add((dedup, item));
        }
        try { _cTotal?.Add(1, LabelSet.From(("endpoint", endpoint), ("status", status ?? 0), ("kind", ev.Kind.ToString()))); } catch { }
    }

    public IReadOnlyList<AggregatedResponse> GetResponses(string endpointKey)
    {
        if (!store.TryGetValue(endpointKey, out var list)) return Array.Empty<AggregatedResponse>();
        lock (list)
        {
            var snapshot = list.Select(t => t.Item).ToList();
            try { _hWindowItems?.Record(snapshot.Count, LabelSet.From(("endpoint", endpointKey))); } catch { }
            return snapshot;
        }
    }

    public void Clear(string? endpointKey = null)
    {
        if (string.IsNullOrEmpty(endpointKey)) { store.Clear(); return; }
        store.TryRemove(endpointKey, out _);
    }

    private static string MakeKey(string url, string? payload)
    {
        try
        {
            var input = url + "#" + (payload ?? string.Empty);
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes.AsSpan(0, 8)); // 16 hex chars
        }
        catch { return url; }
    }
}
