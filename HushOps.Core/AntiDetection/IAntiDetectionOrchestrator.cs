namespace HushOps.Core.AntiDetection;

/// <summary>
/// 反检测调优器接口，负责根据监控信号生成节奏调整策略。
/// </summary>
public interface IAntiDetectionOrchestrator
{
    /// <summary>
    /// 记录一次监控信号并返回最新的调优决策。
    /// </summary>
    /// <param name="signal">监控信号。</param>
    /// <param name="ct">取消标记。</param>
    /// <returns>最新决策。</returns>
    Task<AntiDetectionAdjustment> RecordAsync(AntiDetectionSignal signal, CancellationToken ct = default);

    /// <summary>
    /// 获取指定上下文当前的状态快照。
    /// </summary>
    Task<AntiDetectionContextState?> GetStateAsync(string contextId, CancellationToken ct = default);

    /// <summary>
    /// 获取指定上下文最近的决策列表（按时间倒序）。
    /// </summary>
    Task<IReadOnlyList<AntiDetectionAdjustment>> GetRecentAdjustmentsAsync(string contextId, int take = 5, CancellationToken ct = default);
}
