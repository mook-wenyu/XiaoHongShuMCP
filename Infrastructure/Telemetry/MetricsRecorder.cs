using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace HushOps.Servers.XiaoHongShu.Infrastructure.Telemetry;

/// <summary>
/// 中文：指标记录器，用于记录自定义业务指标。
/// English: Metrics recorder for recording custom business metrics.
/// </summary>
public sealed class MetricsRecorder
{
    private const string MeterName = "HushOps.Servers.XiaoHongShu";
    private readonly Meter _meter;

    // 中文：工具执行计数器 | English: Tool execution counter
    private readonly Counter<long> _toolExecutionCounter;

    // 中文：工具执行延迟直方图 | English: Tool execution duration histogram
    private readonly Histogram<double> _toolExecutionDuration;

    // 中文：工具执行错误计数器 | English: Tool execution error counter
    private readonly Counter<long> _toolExecutionErrorCounter;

    public MetricsRecorder()
    {
        _meter = new Meter(MeterName, "1.0.0");

        _toolExecutionCounter = _meter.CreateCounter<long>(
            "tool_execution_total",
            description: "工具执行总次数 | Total tool executions");

        _toolExecutionDuration = _meter.CreateHistogram<double>(
            "tool_execution_duration_ms",
            unit: "ms",
            description: "工具执行延迟（毫秒）| Tool execution duration in milliseconds");

        _toolExecutionErrorCounter = _meter.CreateCounter<long>(
            "tool_execution_errors_total",
            description: "工具执行错误总次数 | Total tool execution errors");
    }

    /// <summary>
    /// 中文：记录工具执行。
    /// English: Record tool execution.
    /// </summary>
    /// <param name="toolName">工具名称 | Tool name</param>
    /// <param name="durationMs">执行延迟（毫秒）| Execution duration in milliseconds</param>
    /// <param name="success">是否成功 | Whether the execution was successful</param>
    public void RecordToolExecution(string toolName, double durationMs, bool success)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new ArgumentException("工具名称不能为空 | Tool name cannot be empty", nameof(toolName));
        }

        var tags = new[]
        {
            new KeyValuePair<string, object?>("tool", toolName),
            new KeyValuePair<string, object?>("success", success)
        };

        _toolExecutionCounter.Add(1, tags);
        _toolExecutionDuration.Record(durationMs, new KeyValuePair<string, object?>("tool", toolName));

        if (!success)
        {
            _toolExecutionErrorCounter.Add(1, new KeyValuePair<string, object?>("tool", toolName));
        }
    }

    /// <summary>
    /// 中文：记录工具执行成功。
    /// English: Record successful tool execution.
    /// </summary>
    /// <param name="toolName">工具名称 | Tool name</param>
    /// <param name="durationMs">执行延迟（毫秒）| Execution duration in milliseconds</param>
    public void RecordSuccess(string toolName, double durationMs)
    {
        RecordToolExecution(toolName, durationMs, success: true);
    }

    /// <summary>
    /// 中文：记录工具执行失败。
    /// English: Record failed tool execution.
    /// </summary>
    /// <param name="toolName">工具名称 | Tool name</param>
    /// <param name="durationMs">执行延迟（毫秒）| Execution duration in milliseconds</param>
    public void RecordFailure(string toolName, double durationMs)
    {
        RecordToolExecution(toolName, durationMs, success: false);
    }
}
