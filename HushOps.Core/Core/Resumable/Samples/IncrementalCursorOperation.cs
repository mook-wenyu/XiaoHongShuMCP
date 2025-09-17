using System;
using System.Threading.Tasks;

namespace HushOps.Core.Resumable.Samples;

/// <summary>
/// 示例：按步计数的可恢复操作（每次前进一步，写一次检查点）。
/// 用于验证 IResumableOperation 与 ICheckpointRepository 协作逻辑。
/// </summary>
public sealed class IncrementalCursorOperation : IResumableOperation<IncrementalCheckpoint>
{
    private readonly int targetSteps;

    public IncrementalCursorOperation(int targetSteps)
    {
        if (targetSteps <= 0) throw new ArgumentOutOfRangeException(nameof(targetSteps));
        this.targetSteps = targetSteps;
    }

    public async Task<OperationResult<IncrementalCheckpoint>> RunOrResumeAsync(OperationContext ctx)
    {
        var latest = await ctx.Repository.LoadLatestAsync(ctx.OperationId, ctx.CancellationToken);
        var last = latest == null ? new IncrementalCheckpoint(0, "start", false) : CheckpointSerializer.Unpack<IncrementalCheckpoint>(latest);
        var nextStep = last.Step + 1;
        var completed = nextStep >= targetSteps;
        var cursor = $"s-{nextStep}";
        var ckpt = new IncrementalCheckpoint(nextStep, cursor, completed);
        var seq = (latest?.Seq ?? 0) + 1;
        await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, seq, ckpt), ctx.CancellationToken);
        return new OperationResult<IncrementalCheckpoint> { Completed = completed, LastCheckpoint = ckpt, Seq = seq };
    }
}

/// <summary>
/// 示例检查点：记录当前步数与游标，以及是否完成。
/// </summary>
public readonly record struct IncrementalCheckpoint(int Step, string Cursor, bool Completed);
