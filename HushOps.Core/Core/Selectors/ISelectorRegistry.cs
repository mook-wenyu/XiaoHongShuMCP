using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Core.Core.Selectors;

/// <summary>
/// 选择器注册表接口，负责记录、查询与回滚工作流选择器。
/// </summary>
public interface ISelectorRegistry
{
    /// <summary>发布新版本。</summary>
    Task<SelectorRegistryItem> PublishAsync(SelectorRevision revision, CancellationToken ct = default);

    /// <summary>回滚到历史版本。</summary>
    Task<SelectorRegistryItem> RollbackAsync(string alias, string version, CancellationToken ct = default);

    /// <summary>获取当前快照。</summary>
    Task<SelectorRegistrySnapshot> GetSnapshotAsync(CancellationToken ct = default);

    /// <summary>按别名获取当前版本。</summary>
    Task<SelectorRegistryItem?> GetActiveAsync(string alias, CancellationToken ct = default);

    /// <summary>按工作流获取当前 plan。</summary>
    Task<HushOps.Core.Selectors.WeakSelectorPlan> BuildPlanAsync(string workflow, CancellationToken ct = default);
}
