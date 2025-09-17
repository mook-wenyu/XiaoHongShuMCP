using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using HushOps.Core.Observability;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 基于 Polly v8 的熔断适配器（替换自研 SimpleCircuitBreaker）。
/// - 将 XhsSettings.Concurrency.Breaker 的“窗口、阈值、打开时长”映射为 Polly 高级熔断策略：
///   FailureRatio=1.0、MinimumThroughput=FailureThreshold、SamplingDuration=WindowSeconds、BreakDuration=OpenSeconds；
/// - 通过向管道注入合成异常 <see cref="SyntheticBreakerFailureException"/> 记录失败；成功则注入空操作计入采样；
/// - 支持查询当前是否打开以及剩余打开时间（基于 OnOpened 事件与配置的 BreakDuration 计算）。
/// </summary>
public sealed class PollyCircuitBreakerAdapter : ICircuitBreaker, ICircuitBreakerDiagnostics
{
    private readonly XhsSettings.ConcurrencySection _cfg;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly IMetrics? _metrics;
    private readonly ICounter? _opened;

    public PollyCircuitBreakerAdapter(IOptions<XhsSettings> options, IMetrics? metrics = null)
    {
        _cfg = options.Value?.Concurrency ?? new XhsSettings.ConcurrencySection();
        _metrics = metrics;
        if (_metrics != null)
        {
            _opened = _metrics.CreateCounter("circuit_open_total", "熔断打开次数");
        }
    }

    private sealed class Entry
    {
        public ResiliencePipeline Pipeline { get; set; } = default!;
        public CircuitBreakerStateProvider State { get; set; } = default!;
        public TimeSpan BreakDuration { get; set; }
        public DateTime? OpenedUtc { get; set; }
    }

    private Entry GetOrCreate(string key)
    {
        return _entries.GetOrAdd(key, _ =>
        {
            var state = new CircuitBreakerStateProvider();
            var opts = _cfg.Breaker;
            var breakDuration = TimeSpan.FromSeconds(Math.Max(30, opts.OpenSeconds));

            var entry = new Entry
            {
                Pipeline = default!,
                State = state,
                BreakDuration = breakDuration,
                OpenedUtc = null
            };

            var pipeline = new ResiliencePipelineBuilder()
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    // 仅处理合成失败异常（由 RecordFailure 注入）
                    ShouldHandle = new PredicateBuilder().Handle<SyntheticBreakerFailureException>(),
                    FailureRatio = 1.0d,
                    MinimumThroughput = Math.Max(1, opts.FailureThreshold),
                    SamplingDuration = TimeSpan.FromSeconds(Math.Max(10, opts.WindowSeconds)),
                    BreakDuration = breakDuration,
                    StateProvider = state,
                    OnOpened = _ => { entry.OpenedUtc = DateTime.UtcNow; try { _opened?.Add(1, LabelSet.From(("type", "breaker"), ("status", "open"))); } catch { } return ValueTask.CompletedTask; },
                    OnClosed = _ => { entry.OpenedUtc = null; return ValueTask.CompletedTask; },
                    OnHalfOpened = _ => ValueTask.CompletedTask
                })
                .Build();

            entry.Pipeline = pipeline;
            return entry;
        });
    }

    public bool IsOpen(string key)
    {
        var e = GetOrCreate(key);
        return e.State.CircuitState is CircuitState.Open or CircuitState.Isolated;
    }

    public TimeSpan? RemainingOpen(string key)
    {
        var e = GetOrCreate(key);
        if (e.State.CircuitState is CircuitState.Open or CircuitState.Isolated)
        {
            // Polly v8 未直接暴露剩余时间；这里使用“最近一次触发打开时间”的近似
            // 若未记录打开起点，则返回配置的 BreakDuration 作为保守估计
            var opened = e.OpenedUtc;
            if (opened is null) return e.BreakDuration;
            var remain = opened.Value.Add(e.BreakDuration) - DateTime.UtcNow;
            return remain > TimeSpan.Zero ? remain : TimeSpan.Zero;
        }
        return null;
    }

    public void RecordSuccess(string key)
    {
        var e = GetOrCreate(key);
        try
        {
            // 成功样本：执行空操作，用于 HalfOpen 探测与采样基数累积
            e.Pipeline.Execute(static () => { });
            if (e.State.CircuitState == CircuitState.Closed)
            {
                e.OpenedUtc = null;
            }
        }
        catch
        {
            // 熔断打开时执行会抛出 BrokenCircuitException，忽略即可
        }
    }

    public void RecordFailure(string key, string reasonCode)
    {
        var e = GetOrCreate(key);
        try
        {
            e.Pipeline.Execute(static () => throw new SyntheticBreakerFailureException());
        }
        catch (BrokenCircuitException)
        {
            // 一旦打开，记录起点，便于 RemainingOpen 近似估算
            if (e.OpenedUtc is null) e.OpenedUtc = DateTime.UtcNow;
        }
        catch (SyntheticBreakerFailureException)
        {
            // 失败被计入采样；若达到阈值，下一次进入将抛出 BrokenCircuitException
            if (e.State.CircuitState == CircuitState.Open && e.OpenedUtc is null)
                e.OpenedUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 诊断快照：导出每个 key 的熔断状态与剩余打开时间。
    /// </summary>
    public IReadOnlyDictionary<string, object> GetSnapshot()
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var kv in _entries)
        {
            var state = kv.Value.State.CircuitState.ToString();
            TimeSpan? left = null;
            try { left = RemainingOpen(kv.Key); } catch { }
            dict[kv.Key] = new { state, remainingOpenSeconds = left?.TotalSeconds };
        }
        return dict;
    }
}

/// <summary>
/// 合成失败异常：用于向 Polly 熔断策略注入“失败”信号（不携带敏感信息）。
/// </summary>
internal sealed class SyntheticBreakerFailureException : Exception { }
