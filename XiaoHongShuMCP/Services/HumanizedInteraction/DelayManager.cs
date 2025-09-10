namespace XiaoHongShuMCP.Services;

/// <summary>
/// 延时管理器实现类（加速版）。
/// - 职责：统一管理所有等待类型（通过 HumanWaitType），不再暴露各类 Get*Delay 方法；
/// - 目标：在人类“最快自然节奏”范围内模拟微停顿，将主要稳定性交给专用等待服务（如 PageLoadWaitService）。
/// - 说明：此等待仅用于节奏模拟，不做任何风控绕过承诺。
/// </summary>
public class DelayManager : IDelayManager
{
    #region 等待方法
    /// <inheritdoc />
    /// 加速版等待：将所有等待时间缩减到“人类最快节奏”的微小范围；
    /// 真实加载/网络由专用服务兜底，此处仅模拟微停顿。
    public async Task WaitAsync(HumanWaitType waitType, int attemptNumber = 1, CancellationToken cancellationToken = default)
    {
        int delay = waitType switch
        {
            // 基础自然节拍：更接近真实人类操作的短暂停顿
            HumanWaitType.ThinkingPause => Random.Shared.Next(80, 200),      // 思考停顿
            HumanWaitType.ReviewPause => Random.Shared.Next(50, 150),        // 审查停顿
            HumanWaitType.BetweenActions => Random.Shared.Next(60, 180),     // 动作间隔
            HumanWaitType.ClickPreparation => Random.Shared.Next(40, 120),   // 点击准备
            HumanWaitType.HoverPause => Random.Shared.Next(50, 140),         // 悬停停顿
            HumanWaitType.TypingCharacter => Random.Shared.Next(40, 80),     // 打字字符间隔
            HumanWaitType.TypingSemanticUnit => Random.Shared.Next(150, 350), // 语义单元间隔

            // 重试退避：极短线性退避，防止紧锣密鼓的空转
            HumanWaitType.RetryBackoff => Math.Min(200, 30 * Math.Max(1, attemptNumber)),

            // 内容/页面/网络：自然人类等待感知
            HumanWaitType.ModalWaiting => Random.Shared.Next(150, 300),      // 模态框等待
            HumanWaitType.PageLoading => Random.Shared.Next(300, 600),       // 页面加载
            HumanWaitType.NetworkResponse => Random.Shared.Next(100, 250),   // 网络响应
            HumanWaitType.ContentLoading => Random.Shared.Next(200, 400),    // 内容加载

            // 滚动节拍：更符合人类滚动习惯
            HumanWaitType.ScrollPreparation => Random.Shared.Next(50, 120),  // 滚动准备
            HumanWaitType.ScrollExecution => Random.Shared.Next(30, 80),     // 滚动执行
            HumanWaitType.ScrollCompletion => Random.Shared.Next(100, 250),  // 滚动完成
            HumanWaitType.VirtualListUpdate => Random.Shared.Next(200, 400), // 虚拟列表更新

            _ => Random.Shared.Next(80, 200)  // 默认自然停顿
        };

        await Task.Delay(delay, cancellationToken);
    }
    #endregion
}
