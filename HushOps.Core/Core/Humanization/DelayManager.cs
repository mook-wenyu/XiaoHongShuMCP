using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Observability;

namespace HushOps.Core.Humanization;

/// <summary>
/// 延时管理器（Core 默认实现）。
/// - 以“人类最快自然节奏”为上限，提供统一 WaitAsync；
/// - 不负责真实外部事件等待，重型等待应交由专用服务；
/// - 结合 IPacingAdvisor 的倍率进行动态调整。
/// </summary>
public sealed class DelayManager : IDelayManager
{
    private readonly IPacingAdvisor _pacing;
    private readonly IHistogram? _humanDelayMs;
    public DelayManager(IPacingAdvisor? pacing = null, IMetrics? metrics = null)
    {
        _pacing = pacing ?? new PacingAdvisor(Microsoft.Extensions.Options.Options.Create(new PersonaOptions()));
        _humanDelayMs = metrics?.CreateHistogram("human_delay_ms", "人类化等待耗时（毫秒）");
    }

    public async Task WaitAsync(HumanWaitType waitType, int attemptNumber = 1, CancellationToken cancellationToken = default)
    {
        int delay = waitType switch
        {
            HumanWaitType.ThinkingPause => Random.Shared.Next(80, 200),
            HumanWaitType.ReviewPause => Random.Shared.Next(50, 150),
            HumanWaitType.BetweenActions => Random.Shared.Next(60, 180),
            HumanWaitType.ClickPreparation => Random.Shared.Next(40, 120),
            HumanWaitType.HoverPause => Random.Shared.Next(50, 140),
            HumanWaitType.TypingCharacter => Random.Shared.Next(40, 80),
            HumanWaitType.TypingSemanticUnit => Random.Shared.Next(150, 350),
            HumanWaitType.RetryBackoff => Math.Min(200, 30 * Math.Max(1, attemptNumber)),
            HumanWaitType.ModalWaiting => Random.Shared.Next(150, 300),
            HumanWaitType.PageLoading => Random.Shared.Next(300, 600),
            HumanWaitType.NetworkResponse => Random.Shared.Next(100, 250),
            HumanWaitType.ContentLoading => Random.Shared.Next(200, 400),
            HumanWaitType.ScrollPreparation => Random.Shared.Next(50, 120),
            HumanWaitType.ScrollExecution => Random.Shared.Next(30, 80),
            HumanWaitType.ScrollCompletion => Random.Shared.Next(100, 250),
            HumanWaitType.VirtualListUpdate => Random.Shared.Next(200, 400),
            _ => Random.Shared.Next(80, 200)
        };

        var mult = Math.Clamp(_pacing.CurrentMultiplier, 1.0, 5.0);
        var final = (int)Math.Clamp(delay * mult, 1, 10_000);
        try { _humanDelayMs?.Record(final, LabelSet.From(("wait_type", waitType.ToString()), ("multiplier", mult))); } catch { }
        await Task.Delay(final, cancellationToken);
    }
}
