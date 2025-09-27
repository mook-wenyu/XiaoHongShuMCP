using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;

/// <summary>
/// 中文：统一工具执行结果封装，提供成功状态、错误码与数据。
/// </summary>
public sealed class OperationResult<T>
{
    private OperationResult(bool success, string status, T? data, string? errorMessage, IReadOnlyDictionary<string, string>? metadata)
    {
        Success = success;
        Status = status;
        Data = data;
        ErrorMessage = errorMessage;
        Metadata = metadata ?? EmptyMetadata;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    public bool Success { get; }

    public string Status { get; }

    public T? Data { get; }

    public string? ErrorMessage { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public static OperationResult<T> Ok(T data, string status = "ok", IReadOnlyDictionary<string, string>? metadata = null)
        => new(true, status, data, null, metadata);

    public static OperationResult<T> Fail(string status, string? errorMessage = null, IReadOnlyDictionary<string, string>? metadata = null)
        => new(false, string.IsNullOrWhiteSpace(status) ? "ERR_UNEXPECTED" : status, default, errorMessage, metadata);
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
