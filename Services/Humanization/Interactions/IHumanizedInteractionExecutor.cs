using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：定义拟人化动作执行器，将动作脚本映射为实际的 Playwright 操作。
/// English: Describes an executor that performs humanized action scripts on a Playwright page.
/// </summary>
public interface IHumanizedInteractionExecutor
{
    /// <summary>
    /// 中文：逐个执行动作脚本中的所有动作。
    /// English: Executes all actions contained in the provided script sequentially.
    /// </summary>
    Task ExecuteAsync(IPage page, HumanizedActionScript script, CancellationToken cancellationToken = default);

    /// <summary>
    /// 中文：执行单个动作。
    /// English: Executes a single action.
    /// </summary>
    Task ExecuteAsync(IPage page, HumanizedAction action, CancellationToken cancellationToken = default);
}
