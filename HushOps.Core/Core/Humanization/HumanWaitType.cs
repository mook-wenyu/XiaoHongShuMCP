namespace HushOps.Core.Humanization;

/// <summary>
/// 拟人化等待类型（Core枚举）。
/// 说明：用于 DelayManager/PacingAdvisor 协同定义通用节律；
/// 仅表达“节律语义”，不绑定具体实现或业务。
/// </summary>
public enum HumanWaitType
{
    ThinkingPause,
    ReviewPause,
    BetweenActions,
    ClickPreparation,
    HoverPause,
    TypingCharacter,
    TypingSemanticUnit,
    RetryBackoff,
    ModalWaiting,
    PageLoading,
    NetworkResponse,
    ContentLoading,
    ScrollPreparation,
    ScrollExecution,
    ScrollCompletion,
    VirtualListUpdate
}

