using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using HushOps.Core.Resumable;
using HushOps.Persistence;

namespace Tests.Core.Resumable;

/// <summary>
/// 文件(JSON)检查点仓库的基本读写与覆盖测试（中文注释）。
/// </summary>
[TestFixture]
public class FileJsonCheckpointRepositoryTests
{
    private string TempDir() => Path.Combine(Path.GetTempPath(), $"xhs_ckpt_{Guid.NewGuid():N}");

    private sealed record DemoCkpt(int Step, string Cursor);

    [Test]
    public async Task Save_And_LoadLatest_Should_Work()
    {
        var dir = TempDir();
        await using var repo = new FileJsonCheckpointRepository(dir);
        var op = "op-001";

        // 初次无记录
        var none = await repo.LoadLatestAsync(op);
        Assert.That(none, Is.Null);

        // 保存三次递增检查点
        for (var i = 1; i <= 3; i++)
        {
            var env = CheckpointSerializer.Pack(op, i, new DemoCkpt(i, $"cur-{i}"));
            await repo.SaveAsync(env);
        }

        var latest = await repo.LoadLatestAsync(op);
        Assert.That(latest, Is.Not.Null);
        Assert.That(latest!.Seq, Is.EqualTo(3));
        var ckpt = CheckpointSerializer.Unpack<DemoCkpt>(latest);
        Assert.That(ckpt.Step, Is.EqualTo(3));
        Assert.That(ckpt.Cursor, Is.EqualTo("cur-3"));

        // 删除并确认为空
        await repo.DeleteAsync(op);
        var afterDel = await repo.LoadLatestAsync(op);
        Assert.That(afterDel, Is.Null);
    }

    [Test]
    public async Task ListLatest_Should_Filter_By_Prefix_And_Limit()
    {
        var dir = TempDir();
        await using var repo = new FileJsonCheckpointRepository(dir);
        // op-a-1..3, op-b-1..2
        for (var i = 1; i <= 3; i++)
            await repo.SaveAsync(CheckpointSerializer.Pack($"op-a", i, new DemoCkpt(i, $"a-{i}")));
        for (var i = 1; i <= 2; i++)
            await repo.SaveAsync(CheckpointSerializer.Pack($"op-b", i, new DemoCkpt(i, $"b-{i}")));

        var all = await repo.ListLatestAsync(null, 10);
        Assert.That(all.Count, Is.EqualTo(2));
        Assert.That(all.Any(x => x.OperationId == "op-a" && x.Seq == 3), Is.True);
        Assert.That(all.Any(x => x.OperationId == "op-b" && x.Seq == 2), Is.True);

        var onlyA = await repo.ListLatestAsync("op-a", 10);
        Assert.That(onlyA.Count, Is.EqualTo(1));
        Assert.That(onlyA[0].OperationId, Is.EqualTo("op-a"));
        Assert.That(onlyA[0].Seq, Is.EqualTo(3));

        var top1 = await repo.ListLatestAsync(null, 1);
        Assert.That(top1.Count, Is.EqualTo(1));
    }
}

