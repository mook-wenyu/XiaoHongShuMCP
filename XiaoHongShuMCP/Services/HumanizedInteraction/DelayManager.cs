namespace XiaoHongShuMCP.Services;

/// <summary>
/// 延时管理器实现类
/// 统一管理所有类型的拟人化延时，使用硬编码配置
/// </summary>
public class DelayManager : IDelayManager
{
    #region 硬编码延时配置
    
    /// <summary>
    /// 思考停顿时间范围（毫秒）
    /// </summary>
    private readonly (int Min, int Max) _thinkingPause = (500, 2000);
    
    /// <summary>
    /// 检查停顿时间范围（毫秒）
    /// </summary>
    private readonly (int Min, int Max) _reviewPause = (100, 300);
    
    /// <summary>
    /// 点击延时范围（毫秒）
    /// </summary>
    private readonly (int Min, int Max) _clickDelay = (50, 200);
    
    /// <summary>
    /// 滚动延时范围（毫秒）
    /// </summary>
    private readonly (int Min, int Max) _scrollDelay = (300, 800);
    
    /// <summary>
    /// 悬停延时范围（毫秒）
    /// </summary>
    private readonly (int Min, int Max) _hoverDelay = (200, 500);
    
    /// <summary>
    /// 字符输入延时范围（毫秒）
    /// </summary>
    private readonly (int Min, int Max) _characterTypingDelay = (30, 80);
    
    /// <summary>
    /// 语义单位间延时范围（毫秒）
    /// </summary>
    private readonly (int Min, int Max) _semanticUnitDelay = (100, 300);
    
    /// <summary>
    /// 动作间延时范围（毫秒）
    /// </summary>
    private readonly (int Min, int Max) _betweenActionsDelay = (300, 800);
    
    /// <summary>
    /// 重试基础延时（毫秒）
    /// </summary>
    private const int RetryBaseDelay = 500;
    
    #endregion
    
    #region 延时获取方法
    
    /// <inheritdoc />
    public int GetThinkingPauseDelay() => 
        Random.Shared.Next(_thinkingPause.Min, _thinkingPause.Max);
    
    /// <inheritdoc />
    public int GetReviewPauseDelay() => 
        Random.Shared.Next(_reviewPause.Min, _reviewPause.Max);
    
    /// <inheritdoc />
    public int GetClickDelay() => 
        Random.Shared.Next(_clickDelay.Min, _clickDelay.Max);
    
    /// <inheritdoc />
    public int GetScrollDelay() => 
        Random.Shared.Next(_scrollDelay.Min, _scrollDelay.Max);
    
    /// <inheritdoc />
    public int GetHoverDelay() => 
        Random.Shared.Next(_hoverDelay.Min, _hoverDelay.Max);
    
    /// <inheritdoc />
    public int GetCharacterTypingDelay() => 
        Random.Shared.Next(_characterTypingDelay.Min, _characterTypingDelay.Max);
    
    /// <inheritdoc />
    public int GetSemanticUnitDelay() => 
        Random.Shared.Next(_semanticUnitDelay.Min, _semanticUnitDelay.Max);
    
    /// <inheritdoc />
    public int GetRetryDelay(int attemptNumber) => 
        RetryBaseDelay * attemptNumber;
    
    /// <inheritdoc />
    public int GetBetweenActionsDelay() => 
        Random.Shared.Next(_betweenActionsDelay.Min, _betweenActionsDelay.Max);
    
    #endregion
    
    #region 等待方法
    
    /// <inheritdoc />
    public async Task WaitAsync(HumanWaitType waitType)
    {
        var delay = waitType switch
        {
            HumanWaitType.ThinkingPause => GetThinkingPauseDelay(),
            HumanWaitType.ReviewPause => GetReviewPauseDelay(), 
            HumanWaitType.BetweenActions => GetBetweenActionsDelay(),
            HumanWaitType.ModalWaiting => Random.Shared.Next(1000, 2000),
            HumanWaitType.PageLoading => Random.Shared.Next(2000, 4000),
            HumanWaitType.NetworkResponse => Random.Shared.Next(500, 1500),
            
            // 虚拟化列表滚动搜索相关的新等待类型
            HumanWaitType.ContentLoading => Random.Shared.Next(800, 1500),     // 虚拟化列表新内容渲染
            HumanWaitType.ScrollPreparation => Random.Shared.Next(300, 700),    // 滚动前观察准备
            HumanWaitType.ScrollExecution => Random.Shared.Next(200, 500),      // 滚动步骤间隔
            HumanWaitType.ScrollCompletion => Random.Shared.Next(400, 900),     // 滚动完成后观察
            HumanWaitType.VirtualListUpdate => Random.Shared.Next(600, 1200),   // 虚拟列表DOM更新
            
            _ => Random.Shared.Next(500, 1000) // 默认延时
        };
            
        await Task.Delay(delay);
    }
    
    #endregion
    
    #region 辅助方法
    
    /// <summary>
    /// 根据文本长度获取检查停顿延时
    /// </summary>
    public int GetTextReviewDelay(string text, bool hasEndPunctuation = false)
    {
        if (text.Length > 4 || hasEndPunctuation)
        {
            return GetReviewPauseDelay();
        }
        
        return Random.Shared.Next(100, 300);
    }
    
    /// <summary>
    /// 获取随机语义单位分割点的停顿时间
    /// </summary>
    public int GetRandomSemanticBreakDelay()
    {
        return Random.Shared.Next(200, 600);
    }
    
    #endregion
}