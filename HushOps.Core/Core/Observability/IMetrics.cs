using System.Collections.Generic;

namespace HushOps.Core.Observability;

/// <summary>
/// 指标抽象（驱动与具体可观测实现无关）。
/// - 设计目标：解耦业务与具体指标管线，实现可插拔且可逐步收敛的观测策略。
/// - 默认实现 InProcessMetrics 在进程内聚合并保持标签白名单。
/// - 标签治理：通过 <see cref="LabelSet"/> 封装标签键值，具体实现负责白名单过滤和基数控制。
/// </summary>
public interface IMetrics
{
    /// <summary>创建计数器（长整型）。</summary>
    ICounter CreateCounter(string name, string? description = null);

    /// <summary>创建直方图（双精度）。</summary>
    IHistogram CreateHistogram(string name, string? description = null);
}

/// <summary>
/// 计数器接口（Add）。
/// </summary>
public interface ICounter
{
    /// <summary>
    /// 增加计数值。
    /// </summary>
    void Add(long value, in LabelSet labels);
}

/// <summary>
/// 直方图接口（Record）。
/// </summary>
public interface IHistogram
{
    /// <summary>
    /// 记录一个样本值。
    /// </summary>
    void Record(double value, in LabelSet labels);
}

/// <summary>
/// 标签集合（不可变），用于传递至指标实现。
/// - 注意：具体实现应对标签进行白名单过滤与值脱敏，不应直接使用本集合原始数据进行导出。
/// </summary>
public readonly struct LabelSet
{
    /// <summary>内部标签字典（键大小写不敏感）。</summary>
    public IReadOnlyDictionary<string, object?> Labels { get; }

    private LabelSet(IReadOnlyDictionary<string, object?> map)
    {
        Labels = map;
    }

    /// <summary>
    /// 使用键值构建标签集合。重复键后者覆盖前者，键名统一为小写。
    /// </summary>
    public static LabelSet From(params (string Key, object? Value)[] pairs)
    {
        var cmp = System.StringComparer.OrdinalIgnoreCase;
        var dict = new Dictionary<string, object?>(cmp);
        if (pairs != null)
        {
            foreach (var (k, v) in pairs)
            {
                if (string.IsNullOrWhiteSpace(k)) continue;
                dict[k.Trim().ToLowerInvariant()] = v;
            }
        }
        return new LabelSet(dict);
    }
}

/// <summary>
/// 空实现（No-Op）：用于禁用指标时的注入对象，保证业务代码无需判空即可调用。
/// </summary>
public sealed class NoopMetrics : IMetrics
{
    private sealed class NoopCounter : ICounter { public void Add(long value, in LabelSet labels) { } }
    private sealed class NoopHistogram : IHistogram { public void Record(double value, in LabelSet labels) { } }

    public ICounter CreateCounter(string name, string? description = null) => new NoopCounter();
    public IHistogram CreateHistogram(string name, string? description = null) => new NoopHistogram();
}
