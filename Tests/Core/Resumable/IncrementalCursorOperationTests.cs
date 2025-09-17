using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using HushOps.Core.Resumable;
using HushOps.Core.Resumable.Samples;
using HushOps.Persistence;

namespace Tests.Core.Resumable;

/// <summary>
/// 增量游标可恢复操作的端到端测试：验证断点续写（跨进程模拟）。
/// </summary>
[TestFixture]
public class IncrementalCursorOperationTests
{
    private string TempDir() => Path.Combine(Path.GetTempPath(), $"xhs_ckpt_{Guid.NewGuid():N}");

    [Test]
    public async Task RunOrResume_Should_Advance_And_Persist_Checkpoints()
    {
        var dir = TempDir();
        var opId = "inc-001";
        await using var repo1 = new FileJsonCheckpointRepository(dir);
        var op = new IncrementalCursorOperation(targetSteps: 3);
        var ctx = new OperationContext { OperationId = opId, Repository = repo1, CancellationToken = CancellationToken.None };

        // 第一次执行：从 0 -> 1
        var r1 = await op.RunOrResumeAsync(ctx);
        Assert.That(r1.Completed, Is.False);
        Assert.That(r1.LastCheckpoint.Step, Is.EqualTo(1));
        Assert.That(r1.Seq, Is.EqualTo(1));

        // 模拟“进程重启”：重新打开仓库与操作
        await using var repo2 = new FileJsonCheckpointRepository(dir);
        var op2 = new IncrementalCursorOperation(targetSteps: 3);
        var ctx2 = new OperationContext { OperationId = opId, Repository = repo2, CancellationToken = CancellationToken.None };

        var r2 = await op2.RunOrResumeAsync(ctx2); // 1 -> 2
        Assert.That(r2.LastCheckpoint.Step, Is.EqualTo(2));
        Assert.That(r2.Seq, Is.EqualTo(2));
        Assert.That(r2.Completed, Is.False);

        var r3 = await op2.RunOrResumeAsync(ctx2); // 2 -> 3 (完成)
        Assert.That(r3.LastCheckpoint.Step, Is.EqualTo(3));
        Assert.That(r3.Completed, Is.True);
        Assert.That(r3.Seq, Is.EqualTo(3));
    }
}
