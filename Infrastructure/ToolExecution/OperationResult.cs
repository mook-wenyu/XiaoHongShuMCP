using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;

/// <summary>
/// 中文：统一工具执行结果封装，使用record类型确保可JSON序列化。
/// English: Unified tool execution result wrapper, using record type to ensure JSON serializability.
/// </summary>
public sealed record OperationResult<T>(
    bool Success,
    string Status,
    T? Data,
    string? ErrorMessage,
    IReadOnlyDictionary<string, string> Metadata)
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// 中文：创建成功结果。
    /// English: Creates a success result.
    /// </summary>
    public static OperationResult<T> Ok(T data, string status = "ok", IReadOnlyDictionary<string, string>? metadata = null)
        => new(true, status, data, null, metadata ?? EmptyMetadata);

    /// <summary>
    /// 中文：创建失败结果。
    /// English: Creates a failure result.
    /// </summary>
    public static OperationResult<T> Fail(string status, string? errorMessage = null, IReadOnlyDictionary<string, string>? metadata = null)
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
        Func<Exception, string?, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(onFailure);

        return ExecuteAsync(logger, category, action, requestId, callback, onFailure);
    }

    private static async Task<TResult> ExecuteAsync<TResult>(
        ILogger logger,
        string category,
        string action,
        string? requestId,
        Func<Task<TResult>> callback,
        Func<Exception, string?, TResult> onFailure)
    {
        var rid = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId!.Trim();
        try
        {
            logger.LogInformation("[{Category}] {Action} start rid={RequestId}", category, action, rid);
            var result = await callback().ConfigureAwait(false);
            logger.LogInformation("[{Category}] {Action} success rid={RequestId}", category, action, rid);
            return result;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("[{Category}] {Action} cancelled rid={RequestId}", category, action, rid);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Category}] {Action} failed rid={RequestId}", category, action, rid);
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
