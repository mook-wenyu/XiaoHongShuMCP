using System.Diagnostics.Metrics;

namespace HushOps.Core.Observability;

public static class Metrics
{
    public const string MeterName = "xhs.core";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Histogram<double> HumanTrajectoryDurationMs =
        Meter.CreateHistogram<double>(
            name: "human.trajectory.duration.ms",
            unit: "ms",
            description: "拟人轨迹时长（毫秒）");

    public static readonly Counter<long> HumanTrajectorySteps =
        Meter.CreateCounter<long>(
            name: "human.trajectory.steps",
            unit: "count",
            description: "拟人轨迹步数");

    public static readonly Counter<long> HumanHotspotPauses =
        Meter.CreateCounter<long>(
            name: "human.hotspot.pauses",
            unit: "count",
            description: "热点停顿次数");

    public static readonly Counter<long> AntiDetectSnapshotCount =
        Meter.CreateCounter<long>(
            name: "anti_detect.snapshot.count",
            unit: "count",
            description: "反检测快照生成次数");

    public static readonly Counter<long> UiInjectionCount =
        Meter.CreateCounter<long>(
            name: "ui.injection.count",
            unit: "count",
            description: "UI 注入计数（type 低基数标签）");

    public static readonly Counter<long> NetAggregateCount =
        Meter.CreateCounter<long>(
            name: "net.aggregate.count",
            unit: "count",
            description: "网络事件聚合计数（endpoint/kind/status_class 低基数标签）");

    public static readonly UpDownCounter<long> NetWindowItems =
        Meter.CreateUpDownCounter<long>(
            name: "net.window.items",
            unit: "count",
            description: "去重窗口内项目数（endpoint 低基数标签）");

    private static Func<double>? _unknownRatioProvider;
    public static void RegisterUnknownRatioProvider(Func<double> provider)
        => _unknownRatioProvider = provider;

    private static double ObserveUnknownRatio() => _unknownRatioProvider?.Invoke() ?? 0.0;
    private static readonly ObservableGauge<double> NetUnknownRatio =
        Meter.CreateObservableGauge(
            name: "net.classify.unknown.ratio",
            observeValue: ObserveUnknownRatio,
            unit: "ratio",
            description: "端点分类 unknown 比例（滑动窗口）");
}

