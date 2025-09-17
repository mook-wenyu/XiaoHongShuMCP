using System;
using System.Diagnostics;
using System.Threading;
using Serilog;

namespace XiaoHongShuMCP.Infrastructure;

/// <summary>
/// MCP 工具执行辅助：提供统一的 try/catch 包装与日志记录，保证核心功能在最小治理前提下稳定运行。
/// </summary>
public static class McpToolExecutor
{
    /// <summary>
    /// 包装带有取消令牌的异步操作，输出结构化日志并统一错误映射。
    /// </summary>
    public static async Task<T> TryWithPolicyAsync<T>(
        string declaringTypeName,
        string methodName,
        Func<CancellationToken, Task<T>> op,
        Func<Exception, string, T> onError,
        string? idempotencyKey = null,
        string? requestId = null)
    {
        var methodFullName = $"{declaringTypeName}.{methodName}";
        var rid = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId!;
        try
        {
            using var cts = new CancellationTokenSource();
            var sw = Stopwatch.StartNew();
            var result = await op(cts.Token).ConfigureAwait(false);
            sw.Stop();
            Log.Information("[MCP] tool ok, requestId={RequestId}, method={Method}, elapsedMs={Elapsed}", rid, methodFullName, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            var code = MapExceptionCode(ex);
            Log.Warning(ex, "[MCP] tool error, requestId={RequestId}, method={Method}, code={Code}", rid, methodFullName, code);
            return onError(ex, rid);
        }
    }

    /// <summary>
    /// 包装无取消令牌的异步操作，保持一致的日志与错误处理。
    /// </summary>
    public static async Task<T> TryAsync<T>(Func<Task<T>> op, Func<Exception, string, T> onError, string? requestId = null)
    {
        var rid = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId!;
        try
        {
            var sw = Stopwatch.StartNew();
            var result = await op().ConfigureAwait(false);
            sw.Stop();
            Log.Information("[MCP] tool ok, requestId={RequestId}, elapsedMs={Elapsed}", rid, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            var code = MapExceptionCode(ex);
            Log.Warning(ex, "[MCP] tool error, requestId={RequestId}, code={Code}", rid, code);
            return onError(ex, rid);
        }
    }

    /// <summary>
    /// 包装同步操作，统一成功与异常的日志表现。
    /// </summary>
    public static T Try<T>(Func<T> op, Func<Exception, string, T> onError, string? requestId = null)
    {
        var rid = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId!;
        try
        {
            var result = op();
            Log.Information("[MCP] tool ok, requestId={RequestId}", rid);
            return result;
        }
        catch (Exception ex)
        {
            var code = MapExceptionCode(ex);
            Log.Warning(ex, "[MCP] tool error, requestId={RequestId}, code={Code}", rid, code);
            return onError(ex, rid);
        }
    }

    /// <summary>
    /// 将异常类型映射为 MCP 约定的错误代码。
    /// </summary>
    public static string MapExceptionCode(Exception ex)
    {
        return ex switch
        {
            TimeoutException => "ERR_TIMEOUT",
            OperationCanceledException => "ERR_TIMEOUT",
            ArgumentException => "ERR_VALIDATION",
            InvalidOperationException => "ERR_VALIDATION",
            _ => "ERR_UNEXPECTED"
        };
    }
}
