using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 智能收集控制器
/// </summary>
public class SmartCollectionController : ISmartCollectionController
{
    private readonly ILogger<SmartCollectionController> _logger;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly IPageLoadWaitService _pageLoadWaitService;
    private readonly IBrowserManager _browserManager;
    private readonly IUniversalApiMonitor _universalApiMonitor;
    private readonly McpSettings _mcpSettings;

    // 智能收集状态管理
    private readonly List<NoteInfo> _collectedNotes;
    private readonly HashSet<string> _seenNoteIds;
    private readonly object _stateLock = new();

    // 性能监控
    private SmartCollectionStatus _currentStatus;

    public SmartCollectionController(
        ILogger<SmartCollectionController> logger,
        IHumanizedInteractionService humanizedInteraction,
        IPageLoadWaitService pageLoadWaitService,
        IBrowserManager browserManager,
        IUniversalApiMonitor universalApiMonitor,
        Microsoft.Extensions.Options.IOptions<McpSettings> mcpSettings)
    {
        _logger = logger;
        _humanizedInteraction = humanizedInteraction;
        _pageLoadWaitService = pageLoadWaitService;
        _browserManager = browserManager;
        _universalApiMonitor = universalApiMonitor;
        _mcpSettings = mcpSettings.Value ?? new McpSettings();

        _collectedNotes = [];
        _seenNoteIds = [];
    }

    /// <inheritdoc />
    /// <remarks>
    /// 此方法已重构为统一架构模式，仅通过 API 监听器获取数据。
    /// 不再从页面 DOM 收集数据；必要的 API 监听在此方法内部设置。
    /// </remarks>
    public async Task<SmartCollectionResult> ExecuteSmartCollectionAsync(
        IBrowserContext context, IPage page,
        int targetCount, RecommendCollectionMode mode, TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        // 统一等待时长：若未传入 timeout，则使用 McpSettings.WaitTimeoutMs（默认 10 分钟）。
        var cfgMs = _mcpSettings.WaitTimeoutMs;
        timeout ??= TimeSpan.FromMilliseconds(cfgMs > 0 ? cfgMs : 600_000);
        
        _logger.LogInformation("开始智能收集（统一架构模式）：目标={TargetCount}, 模式={Mode}, 超时={Timeout}ms", 
            targetCount, mode, timeout.Value.TotalMilliseconds);
        
        _logger.LogDebug("智能收集使用统一架构：API监听由调用方管理，此方法专注API数据收集");

        try
        {
            // 仅通过 API 监听收集数据，不再从 DOM 抓取
            // 1) 设置监听器
            var endpointsToMonitor = new HashSet<ApiEndpointType> { ApiEndpointType.Homefeed };
            var setupOk = _universalApiMonitor.SetupMonitor(page, endpointsToMonitor);
            if (!setupOk)
            {
                return SmartCollectionResult.CreateFailure(
                    "无法设置Homefeed API监听器",
                    null,
                    targetCount,
                    0,
                    DateTime.UtcNow - startTime);
            }

            // 2) 导航以触发 API
            var navigationResult = await DirectNavigateToDiscoverAsync(page);
            if (!navigationResult.Success)
            {
                return SmartCollectionResult.CreateFailure(
                    navigationResult.ErrorMessage ?? "导航失败",
                    null,
                    targetCount,
                    0,
                    DateTime.UtcNow - startTime);
            }

            // 3) 等待至少一次 Homefeed 响应
            await _universalApiMonitor.WaitForResponsesAsync(ApiEndpointType.Homefeed, 1);

            // 4) 从监听器获取数据并裁剪到目标数量
            var details = _universalApiMonitor.GetMonitoredNoteDetails(ApiEndpointType.Homefeed);
            var noteInfos = details
                .Select(d => new NoteInfo
                {
                    Id = d.Id,
                    Title = d.Title,
                    Author = d.Author,
                    AuthorId = d.AuthorId,
                    AuthorAvatar = d.AuthorAvatar,
                    CoverImage = d.CoverImage,
                    LikeCount = d.LikeCount,
                    CommentCount = d.CommentCount,
                    FavoriteCount = d.FavoriteCount,
                    PublishTime = d.PublishTime,
                    Url = d.Url,
                    Content = d.Content,
                    Type = d.Type,
                    ExtractedAt = d.ExtractedAt,
                    Quality = d.Quality,
                    MissingFields = d.MissingFields
                })
                .Take(targetCount)
                .ToList();

            var duration = DateTime.UtcNow - startTime;
            var rawResponses = _universalApiMonitor.GetRawResponses(ApiEndpointType.Homefeed);
            var perf = new CollectionPerformanceMetrics(
                successfulRequests: rawResponses.Count,
                failedRequests: 0,
                scrollCount: 0,
                duration: duration);

            // 5) 生成结果
            var result = SmartCollectionResult.CreateSuccess(
                noteInfos,
                targetCount,
                perf.RequestCount,
                duration,
                perf);

            _logger.LogInformation("智能收集完成（API-only）：收集={Actual}/{Target}, 耗时={Duration}ms",
                result.CollectedNotes.Count, targetCount, duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "智能收集执行异常，耗时{Duration}ms", duration.TotalMilliseconds);
            
            return SmartCollectionResult.CreateFailure(
                ex.Message,
                [],
                targetCount,
                0,
                duration);
        }
    }

    /// <summary>
    /// 直接导航到发现页面
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage)> DirectNavigateToDiscoverAsync(IPage page)
    {
        try
        {
            _logger.LogDebug("直接导航到发现页面");
            
            var discoverUrl = "https://www.xiaohongshu.com/explore";
            _browserManager.BeginOperation();
            try
            {
                try
                {
                    await page.GotoAsync(discoverUrl, new PageGotoOptions
                    {
                        // 避免 NetworkIdle 在 SPA 上长期不达成
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 15000
                    });
                }
                catch (Microsoft.Playwright.PlaywrightException)
                {
                    _logger.LogWarning("页面在导航时关闭，尝试重新获取页面并重试");
                    page = await _browserManager.GetPageAsync();
                    await page.GotoAsync(discoverUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 15000
                    });
                }

                // 使用统一等待服务（具备降级/重试）替代固定延时
                await _pageLoadWaitService.WaitForPageLoadAsync(page);
            }
            finally
            {
                _browserManager.EndOperation();
            }

            // 验证是否成功到达发现页面
            var currentUrl = page.Url;
            if (!currentUrl.Contains("/explore"))
            {
                _logger.LogWarning("页面URL验证失败: 期望包含 '/explore'，实际URL: {ActualUrl}", currentUrl);
                return (false, $"页面URL不正确: {currentUrl}");
            }

            _logger.LogInformation("成功导航到发现页面: {Url}", currentUrl);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导航到发现页面失败");
            return (false, ex.Message);
        }
    }

    // 统一架构后不再需要滚动策略/效率评分等逻辑

    /// <summary>
    /// 获取当前状态
    /// </summary>
    public CollectionStatus GetCurrentStatus() 
    {
        return new CollectionStatus
        {
            Phase = _currentStatus switch
            {
                SmartCollectionStatus.Idle => CollectionPhase.Initializing,
                SmartCollectionStatus.Collecting => CollectionPhase.Collecting,
                SmartCollectionStatus.Completed => CollectionPhase.Completed,
                SmartCollectionStatus.Failed => CollectionPhase.Failed,
                _ => CollectionPhase.Initializing
            },
            CurrentCount = _collectedNotes.Count,
            TargetCount = 0, // 这个需要在收集过程中设置
            Progress = 0,    // 这个需要在收集过程中计算
            CollectionMode = RecommendCollectionMode.Standard,
            StartTime = DateTime.UtcNow,
            LastUpdateTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 重置收集状态
    /// </summary>
    public void ResetCollectionState()
    {
        lock (_stateLock)
        {
            _collectedNotes.Clear();
            _seenNoteIds.Clear();
            _currentStatus = SmartCollectionStatus.Idle;
            
            _logger.LogDebug("收集状态已重置");
        }
    }
}

/// <summary>
/// 收集循环结果
/// </summary>

/// <summary>
/// 智能收集状态枚举
/// </summary>
public enum SmartCollectionStatus
{
    Idle,           // 空闲状态
    Collecting,     // 收集中
    Completed,      // 已完成
    Failed          // 失败状态
}
