namespace XiaoHongShuMCP.Services;

/// <summary>
/// 可配置的搜索相关超时（毫秒）
/// </summary>
public class SearchTimeoutsConfig
{
    // 统一 UI 等待：输入框、结果URL/容器、筛选按钮/面板/选项
    public int UiWaitMs { get; set; } = 12000;
    // API 收集窗口：与 UI 等待量级不同，单独配置
    public int ApiCollectionMaxWaitMs { get; set; } = 60000;
}
