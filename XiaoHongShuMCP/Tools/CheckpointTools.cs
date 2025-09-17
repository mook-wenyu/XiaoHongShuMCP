using Microsoft.Extensions.DependencyInjection;
using HushOps.Core.Resumable;

namespace XiaoHongShuMCP.Tools;

/// <summary>
/// 检查点工具：读取/列出/清理最新检查点。
/// </summary>
public static class CheckpointTools
{
    /// <summary>
    /// 读取指定 operationId 的最新检查点。
    /// </summary>
    public static async Task<object> GetCheckpoint(
        string operationId,
        IServiceProvider? serviceProvider = null)
    {
        if (serviceProvider is null) throw new ArgumentNullException(nameof(serviceProvider));
        var repo = serviceProvider.GetRequiredService<ICheckpointRepository>();
        var env = await repo.LoadLatestAsync(operationId);
        if (env == null) return new { exists = false, operationId };
        return new
        {
            exists = true,
            operationId,
            seq = env.Seq,
            timestamp = env.Timestamp,
            type = env.Type,
            data = env.Data
        };
    }

    /// <summary>
    /// 列出最近的检查点（按 seq 倒序）。
    /// </summary>
    public static async Task<object> ListCheckpoints(
        string? prefix = null,
        int top = 50,
        IServiceProvider? serviceProvider = null)
    {
        if (serviceProvider is null) throw new ArgumentNullException(nameof(serviceProvider));
        var repo = serviceProvider.GetRequiredService<ICheckpointRepository>();
        var rows = await repo.ListLatestAsync(prefix, Math.Max(1, top));
        return rows.Select(r => new
        {
            operationId = r.OperationId,
            seq = r.Seq,
            timestamp = r.Timestamp,
            type = r.Type
        }).ToArray();
    }

    /// <summary>
    /// 清理指定 operationId 的所有检查点。
    /// </summary>
    public static async Task<object> ClearCheckpoint(
        string operationId,
        IServiceProvider? serviceProvider = null)
    {
        if (serviceProvider is null) throw new ArgumentNullException(nameof(serviceProvider));
        var repo = serviceProvider.GetRequiredService<ICheckpointRepository>();
        await repo.DeleteAsync(operationId);
        return new { success = true, operationId };
    }
}

