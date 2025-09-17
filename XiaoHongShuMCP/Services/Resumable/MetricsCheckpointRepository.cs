using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Observability;
using HushOps.Core.Resumable;

// 说明：命名空间迁移至 HushOps.Services.Resumable。
namespace XiaoHongShuMCP.Services.Resumable;

/// <summary>
/// 带指标的检查点仓库装饰器：记录 save/load/delete/list 的数量与耗时（ms）。
/// - 指标前缀：ckpt_*
/// - 标签：op（是否提供了前缀/具体id，防基数：只区分 has_id/has_prefix/none）
/// </summary>
public sealed class MetricsCheckpointRepository : ICheckpointRepository
{
    private readonly ICheckpointRepository inner;
    private readonly ICounter cSave;
    private readonly ICounter cLoad;
    private readonly ICounter cDelete;
    private readonly ICounter cList;
    private readonly IHistogram hMs;

    public MetricsCheckpointRepository(ICheckpointRepository inner, IMetrics? metrics)
    {
        this.inner = inner;
        metrics ??= new NoopMetrics();
        cSave = metrics.CreateCounter("ckpt_save_total", "检查点保存次数");
        cLoad = metrics.CreateCounter("ckpt_load_total", "检查点读取次数");
        cDelete = metrics.CreateCounter("ckpt_delete_total", "检查点删除次数");
        cList = metrics.CreateCounter("ckpt_list_total", "检查点列举次数");
        hMs = metrics.CreateHistogram("ckpt_op_duration_ms", "检查点操作耗时（毫秒）");
    }

    public async Task SaveAsync(CheckpointEnvelope envelope, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await inner.SaveAsync(envelope, ct);
        sw.Stop();
        var l = LabelSet.From(("op", "has_id"));
        cSave.Add(1, in l);
        hMs.Record(sw.Elapsed.TotalMilliseconds, in l);
    }

    public async Task<CheckpointEnvelope?> LoadLatestAsync(string operationId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var r = await inner.LoadLatestAsync(operationId, ct);
        sw.Stop();
        var l = LabelSet.From(("op", "has_id"));
        cLoad.Add(1, in l);
        hMs.Record(sw.Elapsed.TotalMilliseconds, in l);
        return r;
    }

    public async Task DeleteAsync(string operationId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await inner.DeleteAsync(operationId, ct);
        sw.Stop();
        var l = LabelSet.From(("op", "has_id"));
        cDelete.Add(1, in l);
        hMs.Record(sw.Elapsed.TotalMilliseconds, in l);
    }

    public async Task<IReadOnlyList<CheckpointEnvelope>> ListLatestAsync(string? operationIdPrefix, int topN = 50, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var r = await inner.ListLatestAsync(operationIdPrefix, topN, ct);
        sw.Stop();
        var l = LabelSet.From(("op", string.IsNullOrEmpty(operationIdPrefix) ? "none" : "has_prefix"));
        cList.Add(1, in l);
        hMs.Record(sw.Elapsed.TotalMilliseconds, in l);
        return r;
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
