using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HushOps.Servers.XiaoHongShu.Infrastructure.Telemetry;

/// <summary>
/// 中文：遥测 ActivitySource 和 Meter 包装器，用于创建分布式跟踪活动和指标。
/// English: Telemetry ActivitySource and Meter wrapper for creating distributed tracing activities and metrics.
/// </summary>
public sealed class TelemetryActivitySource
{
    /// <summary>
    /// 中文：ActivitySource 名称
    /// English: ActivitySource name
    /// </summary>
    public const string SourceName = "HushOps.Servers.XiaoHongShu";

    /// <summary>
    /// 中文：Meter 名称
    /// English: Meter name
    /// </summary>
    public const string MeterName = "HushOps.Servers.XiaoHongShu";

    /// <summary>
    /// 中文：ActivitySource 实例（单例）
    /// English: ActivitySource instance (singleton)
    /// </summary>
    public static readonly ActivitySource Instance = new(SourceName, "1.0.0");

    /// <summary>
    /// 中文：Meter 实例（单例）
    /// English: Meter instance (singleton)
    /// </summary>
    public static readonly Meter MeterInstance = new(MeterName, "1.0.0");

    /// <summary>
    /// 中文：开始一个新的活动（Activity），用于分布式跟踪。
    /// English: Starts a new activity for distributed tracing.
    /// </summary>
    /// <param name="name">活动名称 | Activity name</param>
    /// <param name="kind">活动类型（默认为 Internal）| Activity kind (defaults to Internal)</param>
    /// <returns>活动实例，使用完毕后需调用 Dispose | Activity instance, call Dispose when done</returns>
    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return Instance.StartActivity(name, kind);
    }

    /// <summary>
    /// 中文：创建一个计数器（Counter），用于累加指标（例如：请求总数、错误总数）。
    /// English: Creates a counter for cumulative metrics (e.g., total requests, total errors).
    /// </summary>
    /// <typeparam name="T">计数器值类型 | Counter value type</typeparam>
    /// <param name="name">计数器名称 | Counter name</param>
    /// <param name="unit">单位（可选）| Unit (optional)</param>
    /// <param name="description">描述（可选）| Description (optional)</param>
    /// <returns>计数器实例 | Counter instance</returns>
    public static Counter<T> CreateCounter<T>(string name, string? unit = null, string? description = null)
        where T : struct
    {
        return MeterInstance.CreateCounter<T>(name, unit, description);
    }

    /// <summary>
    /// 中文：创建一个直方图（Histogram），用于分布指标（例如：请求延迟、响应大小）。
    /// English: Creates a histogram for distribution metrics (e.g., request latency, response size).
    /// </summary>
    /// <typeparam name="T">直方图值类型 | Histogram value type</typeparam>
    /// <param name="name">直方图名称 | Histogram name</param>
    /// <param name="unit">单位（可选）| Unit (optional)</param>
    /// <param name="description">描述（可选）| Description (optional)</param>
    /// <returns>直方图实例 | Histogram instance</returns>
    public static Histogram<T> CreateHistogram<T>(string name, string? unit = null, string? description = null)
        where T : struct
    {
        return MeterInstance.CreateHistogram<T>(name, unit, description);
    }

    /// <summary>
    /// 中文：创建一个观察计数器（ObservableCounter），用于异步观察的累加指标。
    /// English: Creates an observable counter for asynchronously observed cumulative metrics.
    /// </summary>
    /// <typeparam name="T">计数器值类型 | Counter value type</typeparam>
    /// <param name="name">计数器名称 | Counter name</param>
    /// <param name="observeValue">观察值的回调函数 | Callback to observe value</param>
    /// <param name="unit">单位（可选）| Unit (optional)</param>
    /// <param name="description">描述（可选）| Description (optional)</param>
    /// <returns>观察计数器实例 | Observable counter instance</returns>
    public static ObservableCounter<T> CreateObservableCounter<T>(
        string name,
        System.Func<T> observeValue,
        string? unit = null,
        string? description = null)
        where T : struct
    {
        return MeterInstance.CreateObservableCounter(name, observeValue, unit, description);
    }

    /// <summary>
    /// 中文：创建一个观察仪表（ObservableGauge），用于异步观察的瞬时值指标（例如：当前活动连接数、内存使用量）。
    /// English: Creates an observable gauge for asynchronously observed instantaneous value metrics (e.g., current active connections, memory usage).
    /// </summary>
    /// <typeparam name="T">仪表值类型 | Gauge value type</typeparam>
    /// <param name="name">仪表名称 | Gauge name</param>
    /// <param name="observeValue">观察值的回调函数 | Callback to observe value</param>
    /// <param name="unit">单位（可选）| Unit (optional)</param>
    /// <param name="description">描述（可选）| Description (optional)</param>
    /// <returns>观察仪表实例 | Observable gauge instance</returns>
    public static ObservableGauge<T> CreateObservableGauge<T>(
        string name,
        System.Func<T> observeValue,
        string? unit = null,
        string? description = null)
        where T : struct
    {
        return MeterInstance.CreateObservableGauge(name, observeValue, unit, description);
    }
}
