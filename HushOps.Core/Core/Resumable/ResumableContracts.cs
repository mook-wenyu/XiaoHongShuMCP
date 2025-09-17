using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Core.Resumable;

/// <summary>
/// 统一的检查点数据封装（序列化后存储）。
/// - Data 为 JSON 字符串，Type 用于断言与调试；Seq 为严格递增序号。
/// </summary>
public sealed class CheckpointEnvelope
{
    public required string OperationId { get; init; }
    public required long Seq { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string Type { get; init; }
    public required string Data { get; init; }
}

/// <summary>
/// 检查点仓库抽象：持久化/读取/删除。
/// </summary>
public interface ICheckpointRepository : IAsyncDisposable
{
    Task SaveAsync(CheckpointEnvelope envelope, CancellationToken ct = default);
    Task<CheckpointEnvelope?> LoadLatestAsync(string operationId, CancellationToken ct = default);
    Task DeleteAsync(string operationId, CancellationToken ct = default);
    /// <summary>
    /// 列出最近的检查点（按 seq 倒序），可选按 operationId 前缀过滤。
    /// </summary>
    Task<IReadOnlyList<CheckpointEnvelope>> ListLatestAsync(string? operationIdPrefix, int topN = 50, CancellationToken ct = default);
}

/// <summary>
/// 可恢复操作的运行上下文（含幂等键与仓库）。
/// </summary>
public sealed class OperationContext
{
    public required string OperationId { get; init; }
    public required ICheckpointRepository Repository { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// 可恢复操作结果。
/// </summary>
public sealed class OperationResult<TCkpt>
{
    public required bool Completed { get; init; }
    public required TCkpt LastCheckpoint { get; init; }
    public required long Seq { get; init; }
}

/// <summary>
/// 可恢复操作接口：运行或从最新检查点恢复继续执行。
/// </summary>
public interface IResumableOperation<TCkpt>
{
    /// <summary>
    /// 执行一次可恢复的处理，并在必要时持久化检查点。
    /// 返回是否完成与最新检查点。
    /// </summary>
    Task<OperationResult<TCkpt>> RunOrResumeAsync(OperationContext ctx);
}

/// <summary>
/// 工具方法：在仓库中序列化/反序列化强类型检查点。
/// </summary>
public static class CheckpointSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Default)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static CheckpointEnvelope Pack<T>(string operationId, long seq, T checkpoint)
    {
        return new CheckpointEnvelope
        {
            OperationId = operationId,
            Seq = seq,
            Timestamp = DateTimeOffset.UtcNow,
            Type = typeof(T).FullName ?? typeof(T).Name,
            Data = JsonSerializer.Serialize(checkpoint, Options)
        };
    }

    public static T Unpack<T>(CheckpointEnvelope env)
        => JsonSerializer.Deserialize<T>(env.Data, Options)!;
}
