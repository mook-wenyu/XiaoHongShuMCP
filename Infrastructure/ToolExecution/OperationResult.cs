using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;

/// <summary>
/// 中文：统一工具执行结果封装，使用record类型和具体类型确保可JSON序列化。
/// English: Unified tool execution result wrapper, using record type and concrete types to ensure JSON serializability.
/// </summary>
public sealed record OperationResult<T>(
    bool Success,
    string Status,
    T? Data,
    string? ErrorMessage,
    Dictionary<string, string> Metadata)
{
    private static readonly Dictionary<string, string> EmptyMetadata = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 中文：创建成功结果。
    /// English: Creates a success result.
    /// </summary>
    public static OperationResult<T> Ok(T data, string status = "ok", Dictionary<string, string>? metadata = null)
        => new(true, status, data, null, metadata ?? EmptyMetadata);

    /// <summary>
    /// 中文：创建失败结果。
    /// English: Creates a failure result.
    /// </summary>
    public static OperationResult<T> Fail(string status, string? errorMessage = null, Dictionary<string, string>? metadata = null)
        => new(false, string.IsNullOrWhiteSpace(status) ? "ERR_UNEXPECTED" : status, default, errorMessage, metadata ?? EmptyMetadata);
}

/// <summary>
/// 中文：统一的工具执行器，带有 try/catch 封装与日志输出。
/// </summary>
public static class ServerToolExecutor
{
    public static Task<TResult> TryAsync<TResult>(
        ILogger logger,
        string category,
        string action,
        string? requestId,
        Func<Task<TResult>> callback,
        Func<Exception, string?, TResult> onFailure,
        MetricsRecorder? metricsRecorder = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(onFailure);

        return ExecuteAsync(logger, category, action, requestId, callback, onFailure, metricsRecorder);
    }

    private static async Task<TResult> ExecuteAsync<TResult>(
        ILogger logger,
        string category,
        string action,
        string? requestId,
        Func<Task<TResult>> callback,
        Func<Exception, string?, TResult> onFailure,
        MetricsRecorder? metricsRecorder)
    {
        var rid = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId!.Trim();
        var toolName = $"{category}.{action}";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("[{Category}] {Action} start rid={RequestId}", category, action, rid);
            var result = await callback().ConfigureAwait(false);
            stopwatch.Stop();

            logger.LogInformation("[{Category}] {Action} success rid={RequestId}, duration={Duration}ms",
                category, action, rid, stopwatch.Elapsed.TotalMilliseconds);

            // 中文：记录成功执行指标 | English: Record successful execution metrics
            metricsRecorder?.RecordSuccess(toolName, stopwatch.Elapsed.TotalMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogWarning("[{Category}] {Action} cancelled rid={RequestId}, duration={Duration}ms",
                category, action, rid, stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "[{Category}] {Action} failed rid={RequestId}, duration={Duration}ms",
                category, action, rid, stopwatch.Elapsed.TotalMilliseconds);

            // 中文：记录失败执行指标 | English: Record failed execution metrics
            metricsRecorder?.RecordFailure(toolName, stopwatch.Elapsed.TotalMilliseconds);

            return onFailure(ex, rid);
        }
    }

    public static string MapExceptionCode(Exception ex)
    {
        return ex switch
        {
            ArgumentException => "ERR_INVALID_ARGUMENT",
            InvalidOperationException => "ERR_INVALID_OPERATION",
            TimeoutException => "ERR_TIMEOUT",
            _ => "ERR_UNEXPECTED"
        };
    }
}
