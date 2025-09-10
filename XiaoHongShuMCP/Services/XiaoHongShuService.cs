using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Microsoft.Extensions.Options;
using NPOI.XSSF.UserModel;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 小红书核心服务实现
/// </summary>
public partial class XiaoHongShuService : IXiaoHongShuService
{
    private readonly ILogger<XiaoHongShuService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IBrowserManager _browserManager;
    private readonly IAccountManager _accountManager;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly IDomElementManager _domElementManager;
    private readonly IPageLoadWaitService _pageLoadWaitService;
    private readonly IPageStateGuard _pageStateGuard;
    private readonly ISmartCollectionController _smartCollectionController;
    private readonly IUniversalApiMonitor _universalApiMonitor;
    private readonly SearchTimeoutsConfig _timeouts;
    private readonly DetailMatchConfig _detailMatch;
    private readonly McpSettings _mcpSettings;
    private readonly EndpointRetryConfig _endpointRetry;

    /// <summary>
    /// URL构建常量和默认参数
    /// </summary>
    private const string BASE_CREATOR_URL = "https://creator.xiaohongshu.com";
    private const string DEFAULT_SOURCE = "web_explore_feed";
    private const string DEFAULT_XSEC_SOURCE = "pc_search";

    public XiaoHongShuService(
        ILogger<XiaoHongShuService> logger,
        ILoggerFactory loggerFactory,
        IBrowserManager browserManager,
        IAccountManager accountManager,
        IHumanizedInteractionService humanizedInteraction,
        IDomElementManager domElementManager,
        IPageLoadWaitService pageLoadWaitService,
        IPageStateGuard pageStateGuard,
        ISmartCollectionController smartCollectionController,
        IUniversalApiMonitor universalApiMonitor,
        IOptions<SearchTimeoutsConfig> timeouts,
        IOptions<DetailMatchConfig> detailMatch,
        IOptions<McpSettings> mcpSettings,
        IOptions<EndpointRetryConfig> endpointRetry)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _browserManager = browserManager;
        _accountManager = accountManager;
        _humanizedInteraction = humanizedInteraction;
        _domElementManager = domElementManager;
        _pageLoadWaitService = pageLoadWaitService;
        _pageStateGuard = pageStateGuard;
        _smartCollectionController = smartCollectionController;
        _universalApiMonitor = universalApiMonitor;
        _timeouts = timeouts.Value ?? new SearchTimeoutsConfig();
        _detailMatch = detailMatch.Value ?? new DetailMatchConfig();
        _mcpSettings = mcpSettings.Value ?? new McpSettings();
        _endpointRetry = endpointRetry.Value ?? new EndpointRetryConfig();
    }

    /// <summary>
    /// 基于关键词查找单个笔记详情
    /// 使用UniversalApiMonitor的Feed端点功能替代FeedApiMonitor
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="includeComments">是否包含评论</param>
    /// <returns>笔记详情操作结果</returns>
    public async Task<OperationResult<NoteDetail>> GetNoteDetailAsync(
        string keyword,
        bool includeComments = false)
    {
        _logger.LogInformation("开始获取笔记详情: 关键词={Keyword}, 包含评论={IncludeComments}",
            keyword, includeComments);

        try
        {
            // 1. 检查登录状态
            if (!await _accountManager.IsLoggedInAsync())
            {
                return OperationResult<NoteDetail>.Fail(
                    "用户未登录，请先登录",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }

            var page = await _browserManager.GetPageAsync();

            // 1.1 操作前环境校验：确保位于探索/发现/搜索页面；如在详情页则先退出
            var ensured = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(page);
            if (!ensured)
            {
                return OperationResult<NoteDetail>.Fail(
                    "无法导航至探索/发现/搜索页面",
                    ErrorType.NavigationError,
                    "ENTRY_PAGE_NOT_AVAILABLE");
            }

            // 2. 设置端点监听（详情 + 评论）
            _browserManager.BeginOperation();
            var endpointsToMonitor = new HashSet<ApiEndpointType>
            {
                ApiEndpointType.Feed,
                ApiEndpointType.Comments
            };

            var setupSuccess = _universalApiMonitor.SetupMonitor(page, endpointsToMonitor);
            if (!setupSuccess)
            {
                return OperationResult<NoteDetail>.Fail(
                    "无法设置Feed API监听器",
                    ErrorType.NetworkError,
                    "FEED_API_MONITOR_SETUP_FAILED");
            }

            _logger.LogDebug("Feed API监听器设置完成，开始查找匹配笔记");

            // 3. 通过拟人化操作找到并点击匹配的笔记
            var noteElement = await FindMatchingNoteElementAsync(keyword);
            if (noteElement == null)
            {
                // 兜底：首屏未命中则采用“虚拟化滚动搜索”再尝试一次（增强鲁棒性）
                _logger.LogInformation("首屏未找到匹配笔记，启用滚动搜索兜底...");
                var matchingNotesResult = await FindVisibleMatchingNotesAsync(keyword, 1);
                if (matchingNotesResult.Success && matchingNotesResult.Data?.Any() == true)
                {
                    noteElement = matchingNotesResult.Data.First();
                }
                else
                {
                    return OperationResult<NoteDetail>.Fail(
                        $"未找到匹配关键词的笔记: {keyword}",
                        ErrorType.ElementNotFound,
                        "NOTE_NOT_FOUND");
                }
            }

            // 4/5. 点击笔记元素触发Feed API + 等待端点（可配置重试）
            var maxRetries = Math.Max(0, _endpointRetry.MaxRetries);
            var perAttemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1, _endpointRetry.AttemptTimeoutMs));
            var attempt = 0;
            var feedApiReceived = false;
            while (attempt <= maxRetries)
            {
                // 最后一次重试前：先强制跳转到主页以刷新上下文
                if (maxRetries > 0 && attempt == maxRetries)
                {
                    _logger.LogInformation("最后一次重试：尝试切回发现/搜索入口上下文后再点击笔记");
                    var navOk = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(page);
                    if (!navOk)
                    {
                        _logger.LogWarning("强制跳转主页失败，继续按原路径重试");
                    }
                    await _pageLoadWaitService.WaitForPageLoadAsync(page);
                }
                // 如果不是第一次尝试，重新查找可见匹配笔记元素
                if (attempt > 0)
                {
                    noteElement = await FindMatchingNoteElementAsync(keyword);
                    if (noteElement == null)
                    {
                        return OperationResult<NoteDetail>.Fail(
                            $"未找到匹配关键词的笔记: {keyword}",
                            ErrorType.ElementNotFound,
                            "NOTE_NOT_FOUND");
                    }
                }

                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.Feed);
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.Comments);

                _logger.LogDebug("正在点击笔记元素以触发Feed API（尝试 {Attempt}/{Total}）...", attempt + 1, maxRetries + 1);
                await _humanizedInteraction.HumanClickAsync(noteElement);
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);

                feedApiReceived = await _universalApiMonitor.WaitForResponsesAsync(
                    ApiEndpointType.Feed, perAttemptTimeout, 1);
                if (feedApiReceived) break;

                attempt++;
                if (attempt > maxRetries)
                {
                    _logger.LogWarning("Feed 未命中端点且达到最大重试次数({MaxRetries})", maxRetries);
                    break;
                }

                _logger.LogWarning("Feed 未命中端点，准备重试（第 {Attempt} 次重试）...", attempt);
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);
            }

            if (!feedApiReceived)
            {
                return OperationResult<NoteDetail>.Fail(
                    "等待Feed API响应超时，重试已达上限",
                    ErrorType.NetworkError,
                    "FEED_API_TIMEOUT_RETRY_EXCEEDED");
            }

            // 6. 获取Feed API监听到的笔记详情
            var feedNoteDetails = _universalApiMonitor.GetMonitoredNoteDetails(ApiEndpointType.Feed);
            if (feedNoteDetails.Count == 0)
            {
                return OperationResult<NoteDetail>.Fail(
                    "无法从Feed API获取笔记详情",
                    ErrorType.ElementNotFound,
                    "NO_FEED_API_DATA");
            }

            var noteDetail = feedNoteDetails.First();

            // 7. 评论：等待并合并监听到的评论（如需要）
            if (includeComments)
            {
                _logger.LogDebug("等待评论API响应...");
                var gotComments = await _universalApiMonitor.WaitForResponsesAsync(
                    ApiEndpointType.Comments, perAttemptTimeout, 1);

                if (!gotComments)
                {
                    _logger.LogWarning("评论API等待超时，继续返回笔记详情（无评论）");
                }
                else
                {
                    var raws = _universalApiMonitor.GetRawResponses(ApiEndpointType.Comments);
                    var matched = raws
                        .Where(r => r.ProcessedData != null && r.ProcessedData.TryGetValue("NoteId", out var nid) &&
                                    (nid?.ToString() ?? string.Empty) == noteDetail.Id)
                        .ToList();

                    var merged = new List<CommentInfo>();
                    foreach (var r in matched)
                    {
                        try
                        {
                            if (r.ProcessedData != null && r.ProcessedData.TryGetValue("Comments", out var arr) && arr is List<CommentInfo> list)
                            {
                                merged.AddRange(list);
                            }
                        }
                        catch { }
                    }

                    if (merged.Count != 0)
                    {
                        noteDetail.Comments = merged;
                        _logger.LogInformation("已合并评论: {Count} 条", merged.Count);
                    }
                }
            }

            // 8. 拟人化延时后返回结果
            await _humanizedInteraction.HumanBetweenActionsDelayAsync();

            _logger.LogInformation("成功获取笔记详情: 关键词={Keyword}, 标题={Title}, 类型={Type}, 质量={Quality}",
                keyword, noteDetail.Title, noteDetail.Type, noteDetail.Quality);

            return OperationResult<NoteDetail>.Ok(noteDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取笔记详情失败: 关键词={Keyword}", keyword);

            return OperationResult<NoteDetail>.Fail(
                $"获取笔记详情失败: {ex.Message}",
                ErrorType.BrowserError,
                "GET_NOTE_DETAIL_ERROR");
        }
        finally
        {
            // 清理API监听器
            try
            {
                await _universalApiMonitor.StopMonitoringAsync();
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.Feed);
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.Comments);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理Feed API监听器失败");
            }
            _browserManager.EndOperation();
        }
    }

    /// <summary>
    /// 批量查找笔记详情
    /// 行为约定（破坏性变更）：若当前处于“笔记详情”页面，将先尝试关闭详情，避免在错误上下文下触发监听。
    /// </summary>
    /// <param name="keyword">关键词（单字符串，破坏性变更）</param>
    /// <param name="maxCount">最大查找数量</param>
    /// <param name="includeComments">是否包含评论数据</param>
    /// <param name="autoExport">是否自动导出到Excel</param>
    /// <param name="exportFileName">导出文件名（可选）</param>
    /// <returns>增强的批量笔记结果，包含统计分析和导出信息</returns>
    public async Task<OperationResult<BatchNoteResult>> BatchGetNoteDetailsAsync(
        string keyword,
        int maxCount = 10,
        bool includeComments = false,
        bool autoExport = true,
        string? exportFileName = null)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("开始批量获取笔记详情(纯监听): 关键词={Keyword}, 最大数量={MaxCount}, 自动导出={AutoExport}",
            keyword, maxCount, autoExport);

        try
        {
            // 1) 参数与登录校验
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return OperationResult<BatchNoteResult>.Fail(
                    "关键词不能为空",
                    ErrorType.ValidationError,
                    "EMPTY_KEYWORD");
            }

            if (!await _accountManager.IsLoggedInAsync())
            {
                return OperationResult<BatchNoteResult>.Fail(
                    "用户未登录，请先登录",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }

            var page = await _browserManager.GetPageAsync();

            var collected = new List<NoteDetail>();
            var failed = new List<(string, string)>();
            var seenIds = new HashSet<string>();
            var processingStats = new Dictionary<ProcessingMode, int>
            {
                [ProcessingMode.Fast] = 0,
                [ProcessingMode.Standard] = 0,
                [ProcessingMode.Careful] = 0
            };
            var individualProcessingTimes = new List<double>();

            // 2) 执行“纯监听”搜索（仅用于触发API，不从DOM取数）
            if (collected.Count >= maxCount) goto AfterSearchBlock;

            // 确保监听器是干净的
            try
            {
                await _universalApiMonitor.StopMonitoringAsync();
            }
            catch
            {
                /* ignore */
            }
            _universalApiMonitor.ClearMonitoredData(ApiEndpointType.SearchNotes);

            var endpointsToMonitor = new HashSet<ApiEndpointType> {ApiEndpointType.SearchNotes};
            var setupOk = _universalApiMonitor.SetupMonitor(page, endpointsToMonitor);
            if (!setupOk)
            {
                failed.Add((keyword, "无法设置SearchNotes API监听器"));
                goto AfterSearchBlock;
            }

            // 触发搜索以产生 SearchNotes API 请求（不读取DOM数据）
            var searchOp = await PerformHumanizedSearchAsync(page, keyword, "comprehensive", "all", "all");
            if (!searchOp.Success)
            {
                failed.Add((keyword, searchOp.ErrorMessage ?? "拟人化搜索失败"));
                await _universalApiMonitor.StopMonitoringAsync();
                goto AfterSearchBlock;
            }

            // 等待端点（可配置重试）
            var maxRetries = Math.Max(0, _endpointRetry.MaxRetries);
            var perAttemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1, _endpointRetry.AttemptTimeoutMs));
            var attempt = 0;
            var got = false;
            while (attempt <= maxRetries)
            {
                got = await _universalApiMonitor.WaitForResponsesAsync(ApiEndpointType.SearchNotes, perAttemptTimeout, 1);
                if (got) break;

                attempt++;
                if (attempt > maxRetries) break;

                _logger.LogWarning("Batch SearchNotes 未命中端点，准备重试（第 {Attempt} 次重试）...", attempt);
                // 最后一次重试前：先强制跳转到主页再执行搜索
                var isLastRetry = maxRetries > 0 && attempt == maxRetries;
                if (isLastRetry)
                {
                    _logger.LogInformation("最后一次重试：尝试切回发现/搜索入口上下文后再执行批量搜索");
                    var navOk = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(page);
                    if (!navOk)
                    {
                        _logger.LogWarning("强制跳转主页失败，继续按原路径重试");
                    }
                    await _pageLoadWaitService.WaitForPageLoadAsync(page);
                }
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.SearchNotes);
                var retryOp = await PerformHumanizedSearchAsync(page, keyword, "comprehensive", "all", "all", assumeOnDiscover: isLastRetry);
                if (!retryOp.Success)
                {
                    break;
                }
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);
            }
            if (!got)
            {
                failed.Add((keyword, "等待SearchNotes API响应超时，重试已达上限"));
                await _universalApiMonitor.StopMonitoringAsync();
                goto AfterSearchBlock;
            }

            var details = _universalApiMonitor.GetMonitoredNoteDetails(ApiEndpointType.SearchNotes);
            if (details.Count == 0)
            {
                failed.Add((keyword, "SearchNotes API无数据"));
                await _universalApiMonitor.StopMonitoringAsync();
                goto AfterSearchBlock;
            }

            // 合并去重并限量
            foreach (var d in details)
            {
                if (collected.Count >= maxCount) break;
                var addStart = DateTime.UtcNow;

                if (string.IsNullOrEmpty(d.Id) || !seenIds.Add(d.Id)) continue;

                // 统计处理模式（不做额外DOM操作）
                var mode = DetermineProcessingMode(collected.Count, d);
                processingStats[mode]++;

                // 若调用方要求包含评论，这里保持纯监听：不额外加载评论，仅占位
                if (includeComments && d.Comments == null)
                {
                    d.Comments = [];
                }

                collected.Add(d);

                var addCost = (DateTime.UtcNow - addStart).TotalMilliseconds;
                individualProcessingTimes.Add(addCost);
            }

            await _universalApiMonitor.StopMonitoringAsync();
            await _humanizedInteraction.HumanBetweenActionsDelayAsync();

            AfterSearchBlock:

            // 3) 统计与结果包装
            var totalDuration = DateTime.UtcNow - startTime;
            var stats = CalculateBatchStatisticsSync(collected, processingStats, individualProcessingTimes);
            var overall = DetermineOverallQuality(collected);

            var enhanced = new BatchNoteResult(
                SuccessfulNotes: collected,
                FailedNotes: failed,
                ProcessedCount: collected.Count,
                ProcessingTime: totalDuration,
                OverallQuality: overall,
                Statistics: stats,
                ExportInfo: null);

            var op = OperationResult<BatchNoteResult>.Ok(enhanced);

            // 4) 异步导出（如开启）
            if (autoExport && collected.Count > 0)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        var fileName = exportFileName ?? $"batch_notes_{DateTime.Now:yyyyMMdd_HHmmss}";
                        var noteInfoList = collected.Cast<NoteInfo>().ToList();
                        var export = ExportNotesSync(noteInfoList, fileName);
                        if (export.Success)
                        {
                            _logger.LogInformation("批量(纯监听)导出完成: {FilePath}", export.Data?.FilePath);
                        }
                        else
                        {
                            _logger.LogWarning("批量(纯监听)导出失败: {Error}", export.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "批量(纯监听)导出任务异常");
                    }
                });
            }

            _logger.LogInformation("批量(纯监听)完成: 成功={Success}, 失败={Failed}, 耗时={Duration}ms, 平均处理时间={Avg:F2}ms",
                collected.Count, failed.Count, totalDuration.TotalMilliseconds, stats.AverageProcessingTime);

            _browserManager.EndOperation();
            return op;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量(纯监听)获取笔记详情异常");
            _browserManager.EndOperation();
            return OperationResult<BatchNoteResult>.Fail(
                $"批量获取失败: {ex.Message}",
                ErrorType.BrowserError,
                "BATCH_GET_NOTES_FAILED");
        }
    }

    /// <summary>
    /// 同步计算批量处理统计数据（零额外成本）
    /// 基于内置的统计计算模式
    /// </summary>
    private BatchProcessingStatistics CalculateBatchStatisticsSync(
        List<NoteDetail> noteDetails,
        Dictionary<ProcessingMode, int> processingStats,
        List<double> individualProcessingTimes)
    {
        if (noteDetails.Count == 0)
        {
            return new BatchProcessingStatistics(
                CompleteDataCount: 0,
                PartialDataCount: 0,
                MinimalDataCount: 0,
                AverageProcessingTime: 0,
                AverageLikes: 0,
                AverageComments: 0,
                TypeDistribution: new Dictionary<NoteType, int>(),
                ProcessingModeStats: processingStats,
                CalculatedAt: DateTime.UtcNow
            );
        }

        // 数据质量统计
        var completeCount = noteDetails.Count(n => n.Quality == DataQuality.Complete);
        var partialCount = noteDetails.Count(n => n.Quality == DataQuality.Partial);
        var minimalCount = noteDetails.Count(n => n.Quality == DataQuality.Minimal);

        // 平均互动数据
        var likeCounts = noteDetails.Where(n => n.LikeCount.HasValue).Select(n => n.LikeCount!.Value).ToList();
        var commentCounts = noteDetails.Where(n => n.CommentCount.HasValue).Select(n => n.CommentCount!.Value).ToList();

        var avgLikes = likeCounts.Count != 0 ? likeCounts.Average() : 0;
        var avgComments = commentCounts.Count != 0 ? commentCounts.Average() : 0;

        // 笔记类型分布统计
        var typeDistribution = noteDetails
            .GroupBy(n => n.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        // 平均处理时间
        var avgProcessingTime = individualProcessingTimes.Count != 0 ? individualProcessingTimes.Average() : 0;

        return new BatchProcessingStatistics(
            CompleteDataCount: completeCount,
            PartialDataCount: partialCount,
            MinimalDataCount: minimalCount,
            AverageProcessingTime: avgProcessingTime,
            AverageLikes: avgLikes,
            AverageComments: avgComments,
            TypeDistribution: typeDistribution,
            ProcessingModeStats: processingStats,
            CalculatedAt: DateTime.UtcNow
        );
    }

    /// <summary>
    /// 同步导出笔记数据 - 批量处理专用
    /// 简化版本，不使用异步以便在Task.Run中调用
    /// </summary>
    private OperationResult<SimpleExportInfo> ExportNotesSync(List<NoteInfo> notes, string fileName)
    {
        try
        {
            if (notes.Count == 0)
            {
                return OperationResult<SimpleExportInfo>.Fail(
                    "没有数据可导出",
                    ErrorType.ValidationError,
                    "NO_DATA_TO_EXPORT");
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeFileName = Path.GetFileNameWithoutExtension(fileName);
            var fullFileName = $"{safeFileName}_{timestamp}.xlsx";
            var exportsDir = Path.Combine(AppContext.BaseDirectory, "exports");
            var filePath = Path.Combine(exportsDir, fullFileName);

            Directory.CreateDirectory(exportsDir);

            var exportSuccess = ExportToExcel(notes, filePath, new ExportOptions());

            if (exportSuccess && File.Exists(filePath))
            {
                var exportInfo = new SimpleExportInfo(
                    FilePath: filePath,
                    FileName: fullFileName,
                    ExportedAt: DateTime.UtcNow,
                    Success: true
                );

                return OperationResult<SimpleExportInfo>.Ok(exportInfo);
            }
            return OperationResult<SimpleExportInfo>.Fail(
                "导出文件创建失败",
                ErrorType.FileOperation,
                "EXPORT_FILE_FAILED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步导出异常");

            return OperationResult<SimpleExportInfo>.Fail(
                $"导出异常: {ex.Message}",
                ErrorType.FileOperation,
                "EXPORT_EXCEPTION");
        }
    }

    /// <summary>
    /// 导出笔记数据到 Excel
    /// 约束与约定（破坏性变更）：
    /// - 不导出评论（Comments）具体内容，仅导出评论数等统计字段；
    /// - 导出新增字段：ModelType、RawNoteType、IpLocation、LastUpdateTime、ShareDisabled、At用户列表、作者XsecToken；
    /// - 列顺序已调整，不向后兼容旧版本。
    /// </summary>
    private bool ExportToExcel(List<NoteInfo> notes, string filePath, ExportOptions options)
    {
        try
        {
            using var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("小红书笔记数据");

            // 创建标题行
            var headerRow = sheet.CreateRow(0);
            var headers = new[]
            {
                "标题", "描述", "作者", "作者ID", "作者头像", "是否认证",
                "链接", "类型", "类型置信度",
                // 新增：非兼容变更字段
                "ModelType", "原始笔记类型", "IP属地", "最后更新时间", "禁止分享", "@用户列表", "作者XsecToken",
                // 封面信息
                "封面", "封面宽", "封面高", "封面FileId",
                "点赞数", "点赞数(原始)", "评论数", "收藏数", "分享数",
                "是否点赞", "是否收藏",
                "视频时长(秒)", "视频时长(mm:ss)", "视频链接",
                "图片数", "首图",
                "发布时间", "数据质量", "缺失字段",
                "TrackId", "XsecToken", "SearchId", "PageToken",
                "抓取时间"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(headers[i]);
            }

            // 填充数据行
            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                var row = sheet.CreateRow(i + 1);

                int c = 0;

                row.CreateCell(c++).SetCellValue(note.Title);
                row.CreateCell(c++).SetCellValue(note.Description ?? string.Empty);
                row.CreateCell(c++).SetCellValue(note.Author);
                row.CreateCell(c++).SetCellValue(note.AuthorId);
                row.CreateCell(c++).SetCellValue(note.AuthorAvatar);
                row.CreateCell(c++).SetCellValue(note.UserInfo?.IsVerified == true ? "是" : "否");
                row.CreateCell(c++).SetCellValue(note.Url);
                row.CreateCell(c++).SetCellValue(note.Type.ToString());
                row.CreateCell(c++).SetCellValue(note.GetTypeConfidence().ToString());
                // 新增导出字段（破坏性变更）
                row.CreateCell(c++).SetCellValue(note.ModelType);
                row.CreateCell(c++).SetCellValue(note.RawNoteType ?? string.Empty);
                var asDetail = note as NoteDetail;
                row.CreateCell(c++).SetCellValue(asDetail?.IpLocation ?? string.Empty);
                row.CreateCell(c++).SetCellValue(asDetail?.LastUpdateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                row.CreateCell(c++).SetCellValue(asDetail?.ShareDisabled == true ? "是" : "否");
                string atUsersText = string.Empty;
                if (asDetail?.AtUsers is {Count: > 0})
                {
                    atUsersText = string.Join("; ", asDetail.AtUsers.Select(u => string.IsNullOrEmpty(u.UserId) ? u.Nickname : $"{u.Nickname}({u.UserId})"));
                }
                row.CreateCell(c++).SetCellValue(atUsersText);
                row.CreateCell(c++).SetCellValue(note.AuthorXsecToken ?? string.Empty);
                row.CreateCell(c++).SetCellValue(note.CoverImage);
                row.CreateCell(c++).SetCellValue(note.CoverInfo?.Width.ToString() ?? string.Empty);
                row.CreateCell(c++).SetCellValue(note.CoverInfo?.Height.ToString() ?? string.Empty);
                row.CreateCell(c++).SetCellValue(note.CoverInfo?.FileId ?? string.Empty);
                row.CreateCell(c++).SetCellValue(note.LikeCount?.ToString() ?? "N/A");
                row.CreateCell(c++).SetCellValue(note.InteractInfo?.LikedCountRaw ?? string.Empty);
                row.CreateCell(c++).SetCellValue(note.CommentCount?.ToString() ?? "N/A");
                row.CreateCell(c++).SetCellValue(note.FavoriteCount?.ToString() ?? "N/A");
                row.CreateCell(c++).SetCellValue(note.ShareCount?.ToString() ?? "N/A");
                row.CreateCell(c++).SetCellValue(note.IsLiked ? "是" : "否");
                row.CreateCell(c++).SetCellValue(note.IsCollected ? "是" : "否");
                row.CreateCell(c++).SetCellValue(note.VideoDuration?.ToString() ?? "");
                row.CreateCell(c++).SetCellValue(fmtDur(note.VideoDuration));
                row.CreateCell(c++).SetCellValue(note.VideoUrl ?? "");
                var imgCount = note.Images?.Count ?? 0;
                row.CreateCell(c++).SetCellValue(imgCount.ToString());
                row.CreateCell(c++).SetCellValue(imgCount > 0 ? note.Images![0] : string.Empty);
                row.CreateCell(c++).SetCellValue(note.PublishTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A");
                row.CreateCell(c++).SetCellValue(note.Quality.ToString());
                row.CreateCell(c++).SetCellValue(note.MissingFields is {Count: > 0} ? string.Join(",", note.MissingFields) : string.Empty);
                row.CreateCell(c++).SetCellValue(note.TrackId ?? string.Empty);
                row.CreateCell(c++).SetCellValue(note.XsecToken ?? string.Empty);
                row.CreateCell(c++).SetCellValue(note.SearchId ?? string.Empty);
                row.CreateCell(c++).SetCellValue(note.PageToken ?? string.Empty);
                row.CreateCell(c++).SetCellValue(note.ExtractedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                continue;

                string fmtDur(int? s)
                {
                    if (!s.HasValue || s.Value <= 0) return string.Empty;
                    var m = s.Value / 60;
                    var ss = s.Value % 60;
                    return $"{m}:{ss:D2}";
                }
            }

            // 自动调整列宽
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.AutoSizeColumn(i);
            }

            // 保存文件
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            workbook.Write(fileStream);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出Excel文件失败: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// 基于关键词发布评论（破坏性变更：单关键词）
    /// 直接就地/按需打开详情再发布（不使用通用封装）。
    /// </summary>
    /// <param name="keyword">搜索关键词（单字符串）</param>
    /// <param name="content">评论内容</param>
    /// <returns>评论发布操作结果</returns>
    public async Task<OperationResult<CommentResult>> PostCommentAsync(string keyword, string content)
    {
        _logger.LogInformation("开始基于关键词发布评论: 关键词={Keyword}, 内容长度={ContentLength}",
            keyword, content?.Length ?? 0);

        // 参数验证
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return OperationResult<CommentResult>.Fail(
                "关键词不能为空",
                ErrorType.ValidationError,
                "EMPTY_KEYWORD");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return OperationResult<CommentResult>.Fail(
                "评论内容不能为空",
                ErrorType.ValidationError,
                "EMPTY_CONTENT");
        }

        try
        {
            var page = await _browserManager.GetPageAsync();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // A. 已在详情页则就地处理
            var status = await GetCurrentPageStatusAsync(page, PageType.NoteDetail);
            if (status.PageType == PageType.NoteDetail)
            {
                if (!await DoesCurrentDetailMatchKeywords(page, keyword))
                {
                    return OperationResult<CommentResult>.Fail(
                        "当前详情页与关键词不匹配（不回退、不滚动）",
                        ErrorType.ElementNotFound,
                        "DETAIL_NOT_MATCHED");
                }

                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ModalWaiting);
                var activated = await ActivateCommentAreaAsync(page);
                if (!activated)
                {
                    return OperationResult<CommentResult>.Fail(
                        "无法激活评论区域，可能该笔记不支持评论",
                        ErrorType.ElementNotFound,
                        "COMMENT_AREA_NOT_ACTIVATED");
                }
                var inputReady = await WaitForCommentInputReadyAsync(page);
                if (!inputReady)
                {
                    return OperationResult<CommentResult>.Fail(
                        "评论输入框未就绪，请稍后重试",
                        ErrorType.ElementNotFound,
                        "COMMENT_INPUT_NOT_READY");
                }
                var endpoints = new HashSet<ApiEndpointType> {ApiEndpointType.CommentPost};
                _universalApiMonitor.SetupMonitor(page, endpoints);
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.CommentPost);
                await InputCommentWithEnhancedFeaturesAsync(page, content, useEmoji: false, emojiList: null);
                var submitResult = await WaitAndSubmitCommentAsync(page);
                if (!submitResult.Success)
                {
                    sw.Stop();
                    LogInteractionAudit("发表评论", keyword, domVerified: false, apiConfirmed: false, duration: sw.Elapsed, extra: submitResult.Message);
                    return OperationResult<CommentResult>.Fail(
                        submitResult.Message,
                        ErrorType.ElementNotFound,
                        "COMMENT_SUBMIT_FAILED");
                }
                var perAttemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1, _endpointRetry.AttemptTimeoutMs));
                var gotPost = await _universalApiMonitor.WaitForResponsesAsync(ApiEndpointType.CommentPost, perAttemptTimeout, 1);
                if (!gotPost)
                {
                    sw.Stop();
                    LogInteractionAudit("发表评论", keyword, domVerified: false, apiConfirmed: false, duration: sw.Elapsed, extra: "未捕获API响应");
                    return OperationResult<CommentResult>.Fail(
                        "评论失败：未捕获网络确认",
                        ErrorType.NetworkError,
                        "COMMENT_POST_API_NOT_CONFIRMED");
                }
                var raws = _universalApiMonitor.GetRawResponses(ApiEndpointType.CommentPost);
                var latest = raws.LastOrDefault();
                var commentId = latest?.ProcessedData != null && latest.ProcessedData.TryGetValue("CommentId", out var cid)
                    ? cid?.ToString() ?? string.Empty : string.Empty;
                var commentResult = new CommentResult(true, "评论发布成功", string.IsNullOrEmpty(commentId) ? Guid.NewGuid().ToString() : commentId, null);
                await _humanizedInteraction.HumanBetweenActionsDelayAsync();
                sw.Stop();
                LogInteractionAudit("发表评论", keyword, domVerified: false, apiConfirmed: true, duration: sw.Elapsed, extra: $"CommentId={commentResult.CommentId}");
                return OperationResult<CommentResult>.Ok(commentResult);
            }

            // B. 非详情：回到入口页→搜索→打开→复用A
            var ensured = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(page);
            if (!ensured)
            {
                return OperationResult<CommentResult>.Fail(
                    "无法导航至探索/发现/搜索页面",
                    ErrorType.NavigationError,
                    "ENTRY_PAGE_NOT_AVAILABLE");
            }
            var notes = await FindVisibleMatchingNotesAsync(keyword, 1);
            if (!notes.Success || notes.Data is {Count: 0})
            {
                return OperationResult<CommentResult>.Fail(
                    $"未找到匹配关键词的笔记: {keyword}",
                    ErrorType.ElementNotFound,
                    "NO_MATCHING_NOTES");
            }
            var target = notes.Data!.First();
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);
            await _humanizedInteraction.HumanClickAsync(target);
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);
            return await PostCommentAsync(keyword, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "评论发布操作异常: 关键词={Keyword}, 内容={Content}", keyword, content);
            return OperationResult<CommentResult>.Fail(
                $"发布评论异常: {ex.Message}",
                ErrorType.BrowserError,
                "POST_COMMENT_OPERATION_EXCEPTION");
        }
    }

    /// <summary>
    /// 激活评论区域 - 检测并点击激活评论输入框
    /// </summary>
    private async Task<bool> ActivateCommentAreaAsync(IPage page)
    {
        try
        {
            _logger.LogDebug("检测评论区域激活状态...");

            // 首先检测是否已经激活
            var engageBarActiveSelectors = _domElementManager.GetSelectors("EngageBarActive");
            foreach (var selector in engageBarActiveSelectors)
            {
                var activeElement = await page.QuerySelectorAsync(selector);
                if (activeElement != null)
                {
                    _logger.LogDebug("评论区域已激活: {Selector}", selector);
                    return true;
                }
            }

            // 如果未激活，尝试点击评论按钮激活
            _logger.LogDebug("评论区域未激活，尝试点击激活...");
            var commentButtonSelectors = _domElementManager.GetSelectors("DetailPageCommentButton");

            foreach (var selector in commentButtonSelectors)
            {
                try
                {
                    var commentButton = await page.QuerySelectorAsync(selector);
                    if (commentButton != null)
                    {
                        await _humanizedInteraction.HumanClickAsync(commentButton);
                        await Task.Delay(1500); // 等待激活动画完成

                        // 再次检测是否激活成功
                        var firstActiveSelector = engageBarActiveSelectors.FirstOrDefault();
                        if (firstActiveSelector != null)
                        {
                            var activeElement = await page.QuerySelectorAsync(firstActiveSelector);
                            if (activeElement != null)
                            {
                                _logger.LogDebug("评论区域激活成功: {Selector}", selector);
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("使用选择器 {Selector} 激活评论区域失败: {Error}", selector, ex.Message);
                }
            }

            _logger.LogWarning("无法激活评论区域");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "激活评论区域过程中出现异常");
            return false;
        }
    }

    /// <summary>
    /// 等待评论输入框就绪 - 检测contenteditable状态和tribute支持
    /// </summary>
    private async Task<bool> WaitForCommentInputReadyAsync(IPage page, int timeoutMs = 10000)
    {
        try
        {
            _logger.LogDebug("等待评论输入框就绪...");

            var commentInputReadySelectors = _domElementManager.GetSelectors("CommentInputReady");
            var startTime = DateTime.UtcNow;

            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                foreach (var selector in commentInputReadySelectors)
                {
                    try
                    {
                        var inputElement = await page.QuerySelectorAsync(selector);
                        if (inputElement != null)
                        {
                            // 检查输入框是否真正可编辑
                            var isContentEditable = await inputElement.GetAttributeAsync("contenteditable");
                            if (isContentEditable == "true")
                            {
                                _logger.LogDebug("评论输入框就绪: {Selector}", selector);
                                return true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("检查输入框就绪状态失败 {Selector}: {Error}", selector, ex.Message);
                    }
                }

                await Task.Delay(500); // 每500ms检查一次
            }

            _logger.LogWarning("评论输入框在 {Timeout}ms 内未就绪", timeoutMs);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "等待评论输入框就绪过程中出现异常");
            return false;
        }
    }

    /// <summary>
    /// 增强功能评论输入 - 支持表情符号和@提及
    /// </summary>
    private async Task InputCommentWithEnhancedFeaturesAsync(IPage page, string content,
        bool useEmoji = false, List<string>? emojiList = null)
    {
        try
        {
            _logger.LogDebug("开始输入评论内容: 长度={Length}, 使用表情={UseEmoji}", content.Length, useEmoji);

            // 1. 定位评论输入框
            var inputReadySelectors = _domElementManager.GetSelectors("CommentInputReady");
            IElementHandle? inputElement = null;

            foreach (var selector in inputReadySelectors)
            {
                inputElement = await page.QuerySelectorAsync(selector);
                if (inputElement != null) break;
            }

            if (inputElement == null)
            {
                throw new Exception("无法找到评论输入框");
            }

            // 2. 清空输入框并聚焦
            await inputElement.ClickAsync();
            await Task.Delay(500);
            await inputElement.FillAsync("");

            // 3. 如果使用表情符号，先添加表情
            if (useEmoji && emojiList?.Any() == true)
            {
                await AddEmojisToCommentAsync(page, emojiList);
                await Task.Delay(300);
            }

            // 4. 模拟真人逐字输入评论内容
            foreach (char c in content)
            {
                await page.Keyboard.TypeAsync(c.ToString());
                await Task.Delay(Random.Shared.Next(50, 150)); // 随机打字间隔
            }

            _logger.LogDebug("评论内容输入完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "输入评论内容失败");
            throw;
        }
    }

    /// <summary>
    /// 添加表情符号到评论 - 使用最近表情区域
    /// </summary>
    private async Task AddEmojisToCommentAsync(IPage page, List<string> emojiList)
    {
        try
        {
            _logger.LogDebug("添加表情符号: {Count}个", emojiList.Count);

            // 1. 点击表情触发按钮
            var emojiTriggerSelectors = _domElementManager.GetSelectors("EmojiTriggerButton");
            var triggerClicked = false;

            foreach (var selector in emojiTriggerSelectors)
            {
                try
                {
                    var triggerElement = await page.QuerySelectorAsync(selector);
                    if (triggerElement != null)
                    {
                        await _humanizedInteraction.HumanClickAsync(triggerElement);
                        await Task.Delay(1000);
                        triggerClicked = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("表情触发按钮 {Selector} 点击失败: {Error}", selector, ex.Message);
                }
            }

            if (!triggerClicked)
            {
                _logger.LogWarning("无法点击表情触发按钮，跳过表情添加");
                return;
            }

            // 2. 从最近使用的表情中选择
            var emojiClickAreaSelectors = _domElementManager.GetSelectors("EmojiClickArea");
            var availableEmojis = new List<IElementHandle>();

            foreach (var selector in emojiClickAreaSelectors)
            {
                var elements = await page.QuerySelectorAllAsync(selector);
                availableEmojis.AddRange(elements);
            }

            // 3. 随机选择几个表情点击
            var selectedCount = Math.Min(emojiList.Count, Math.Min(availableEmojis.Count, 3)); // 最多3个表情
            var selectedEmojis = availableEmojis.Take(selectedCount);

            foreach (var emoji in selectedEmojis)
            {
                await _humanizedInteraction.HumanClickAsync(emoji);
                await Task.Delay(Random.Shared.Next(300, 600));
            }

            _logger.LogDebug("表情符号添加完成: {Count}个", selectedCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "添加表情符号失败，继续输入文本");
        }
    }

    /// <summary>
    /// 等待并提交评论 - 智能检测按钮状态
    /// </summary>
    private async Task<(bool Success, string Message)> WaitAndSubmitCommentAsync(IPage page, int timeoutMs = 15000)
    {
        try
        {
            _logger.LogDebug("等待发送按钮启用...");

            var enabledSelectors = _domElementManager.GetSelectors("CommentSubmitEnabled");
            var startTime = DateTime.UtcNow;

            // 等待按钮从禁用变为启用
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                foreach (var selector in enabledSelectors)
                {
                    var enabledButton = await page.QuerySelectorAsync(selector);
                    if (enabledButton != null)
                    {
                        _logger.LogDebug("发送按钮已启用，点击提交: {Selector}", selector);
                        await _humanizedInteraction.HumanClickAsync(enabledButton);
                        await Task.Delay(1500);
                        // 不进行任何DOM成功校验，交由API确认
                        return (true, "已点击发送（等待API确认）");
                    }
                }

                await Task.Delay(500); // 每500ms检查一次
            }

            _logger.LogWarning("发送按钮在 {Timeout}ms 内未启用", timeoutMs);
            return (false, $"发送按钮在 {timeoutMs}ms 内未启用");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "等待并提交评论过程中出现异常");
            return (false, $"提交过程异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 检测评论区域动态状态 - 公共方法，供外部调用
    /// </summary>
    public async Task<(bool IsActive, bool InputReady, bool SubmitEnabled)> GetCommentAreaStateAsync()
    {
        try
        {
            var page = await _browserManager.GetPageAsync();

            // 检测激活状态
            var engageBarActiveSelectors = _domElementManager.GetSelectors("EngageBarActive");
            var isActive = false;
            foreach (var selector in engageBarActiveSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    isActive = true;
                    break;
                }
            }

            // 检测输入框就绪状态
            var commentInputReadySelectors = _domElementManager.GetSelectors("CommentInputReady");
            var inputReady = false;
            foreach (var selector in commentInputReadySelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var isContentEditable = await element.GetAttributeAsync("contenteditable");
                    if (isContentEditable == "true")
                    {
                        inputReady = true;
                        break;
                    }
                }
            }

            // 检测提交按钮启用状态
            var submitEnabledSelectors = _domElementManager.GetSelectors("CommentSubmitEnabled");
            var submitEnabled = false;
            foreach (var selector in submitEnabledSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    submitEnabled = true;
                    break;
                }
            }

            _logger.LogDebug("评论区域状态: 激活={IsActive}, 输入就绪={InputReady}, 提交启用={SubmitEnabled}",
                isActive, inputReady, submitEnabled);

            return (isActive, inputReady, submitEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检测评论区域状态失败");
            return (false, false, false);
        }
    }

    /// <summary>
    /// 暂存笔记并离开编辑页面 - 完全重构版
    /// 实现正确的小红书创作平台发布流程：必须先上传图片，然后进入文本编辑，最后暂存离开
    /// </summary>
    public async Task<OperationResult<DraftSaveResult>> TemporarySaveAndLeaveAsync(string title, string content, NoteType noteType, List<string>? imagePaths, string? videoPath, List<string>? tags)
    {
        _logger.LogInformation("开始保存笔记草稿: 标题={Title}, 类型={NoteType}, 图片数量={ImageCount}",
            title, noteType, imagePaths?.Count ?? 0);

        try
        {
            // 1. 前置检查
            if (!await _accountManager.IsLoggedInAsync())
            {
                return OperationResult<DraftSaveResult>.Fail("用户未登录，请先登录", ErrorType.LoginRequired, "NOT_LOGGED_IN");
            }

            // 验证发布前提条件：必须有图片或视频（根据笔记类型）
            if (!await ValidatePublishRequirementsAsync(noteType, imagePaths, videoPath))
            {
                return OperationResult<DraftSaveResult>.Fail("发布失败: 笔记类型与媒体文件不匹配", ErrorType.ValidationError, "TYPE_MEDIA_MISMATCH");
            }

            var page = await _browserManager.GetPageAsync();

            // 2. 导航到正确的发布页面（使用真实参数）
            const string publishUrl = "https://creator.xiaohongshu.com/publish/publish?source=official";
            _logger.LogInformation("导航到创作平台: {PublishUrl}", publishUrl);

            _browserManager.BeginOperation();
            try
            {
                try
                {
                    await page.GotoAsync(publishUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 30000
                    });
                }
                catch (PlaywrightException)
                {
                    _logger.LogWarning("页面在导航发布页时关闭，尝试重新获取页面并重试");
                    page = await _browserManager.GetPageAsync();
                    await page.GotoAsync(publishUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 30000
                    });
                }
                await _pageLoadWaitService.WaitForPageLoadAsync(page);
            }
            finally
            {
                _browserManager.EndOperation();
            }

            // 3. 等待发布页面完全加载（Vue.js应用特殊处理）
            await WaitForPublishPageReadyAsync(page);

            // 4. 执行图片上传（创作平台的强制第一步）
            var uploadResult = await UploadImageAsync(page, imagePaths, videoPath);
            if (!uploadResult.Success)
            {
                return OperationResult<DraftSaveResult>.Fail($"图片上传失败: {uploadResult.ErrorMessage}", ErrorType.BrowserError, "UPLOAD_FAILED");
            }

            // 5. 等待编辑器就绪（只有上传成功后才会出现）
            await WaitForEditorReadyAsync(page);

            // 6. 使用拟人化操作填写内容（模拟真实创作者行为）
            await SimulateCreatorWorkflowAsync(page, title, content, tags ?? []);

            // 7. 暂存离开（安全模式，不自动发布）
            var draftResult = await TemporarySaveAndLeaveInternalAsync(page);
            if (!draftResult.Success)
            {
                return OperationResult<DraftSaveResult>.Fail($"草稿保存失败: {draftResult.ErrorMessage}", ErrorType.BrowserError, "DRAFT_SAVE_FAILED");
            }

            _logger.LogInformation("笔记草稿保存成功: ID={DraftId}", draftResult.DraftId);
            var result = new DraftSaveResult(true, "笔记草稿保存成功", draftResult.DraftId ?? Guid.NewGuid().ToString());
            return OperationResult<DraftSaveResult>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存笔记草稿异常");
            return OperationResult<DraftSaveResult>.Fail($"保存草稿失败: {ex.Message}", ErrorType.BrowserError, "SAVE_DRAFT_EXCEPTION");
        }
    }

    // 注意：基于 DOM 的评论补充逻辑已移除，统一改为监听评论API（破坏性变更）。

    #region 支持方法 - UniversalApiMonitor
    /// <summary>
    /// 查找匹配关键词的单个笔记元素 - Feed API 重构版专用
    /// 为了触发真实 Feed API 请求而定位页面元素
    /// </summary>
    /// <param name="keyword">匹配关键词（单字符串）</param>
    /// <returns>匹配的笔记元素，如果没有找到则返回null</returns>
    private async Task<IElementHandle?> FindMatchingNoteElementAsync(string keyword)
    {
        try
        {
            var page = await _browserManager.GetPageAsync();
            // 修复：别名统一使用 DomElementManager 中存在的 "NoteItem"
            var noteItemSelectors = _domElementManager.GetSelectors("NoteItem");

            _logger.LogDebug("开始查找匹配关键词的笔记元素: {Keyword}", keyword);

            // 尝试不同的选择器找到笔记元素
            foreach (var selector in noteItemSelectors)
            {
                try
                {
                    var noteElements = await page.QuerySelectorAllAsync(selector);
                    if (!noteElements.Any()) continue;

                    _logger.LogDebug("使用选择器 {Selector} 找到 {Count} 个笔记元素", selector, noteElements.Count);

                    // 检查每个笔记元素是否匹配关键词
                    foreach (var noteElement in noteElements)
                    {
                        if (!await IsElementVisible(noteElement)) continue;

                        var noteText = await ExtractNoteTextForMatching(noteElement);
                        if (MatchesKeyword(noteText, keyword))
                        {
                            _logger.LogDebug("找到匹配的笔记元素: {Text}",
                                noteText.Substring(0, Math.Min(50, noteText.Length)));
                            return noteElement;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("选择器 {Selector} 查找失败: {Error}", selector, ex.Message);
                }
            }

            _logger.LogDebug("未找到匹配关键词的笔记元素: {Keyword}", keyword);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查找匹配笔记元素时发生异常: {Keyword}", keyword);
            return null;
        }
    }
    #endregion

    // 破坏性变更：删除 LocateAndOperateNoteAsync 通用封装，改为各功能独立实现

    #region 关键词匹配辅助（详情页）
    /// <summary>
    /// 判断当前打开的笔记详情页是否与关键词匹配
    /// </summary>
    private async Task<bool> DoesCurrentDetailMatchKeywords(IPage page, string keyword)
    {
        try
        {
            var options = new KeywordMatchOptions
            {
                UseFuzzy = _detailMatch.UseFuzzy,
                MaxDistanceCap = _detailMatch.MaxDistanceCap,
                TokenCoverageThreshold = _detailMatch.TokenCoverageThreshold,
                IgnoreSpaces = _detailMatch.IgnoreSpaces
            };

            // 提取字段
            var (title, author, content, hashtags, imageAlts) = await ExtractDetailFieldsAsync(page);

            double score = 0;
            double total = _detailMatch.TitleWeight + _detailMatch.AuthorWeight + _detailMatch.ContentWeight +
                           _detailMatch.HashtagWeight + _detailMatch.ImageAltWeight;

            if (TitleHit()) score += _detailMatch.TitleWeight;
            if (AuthorHit()) score += _detailMatch.AuthorWeight;
            if (ContentHit()) score += _detailMatch.ContentWeight;
            if (TagsHit()) score += _detailMatch.HashtagWeight;
            if (AltHit()) score += _detailMatch.ImageAltWeight;

            // 拼音首字母匹配（可选，主要针对 ASCII 关键字）
            if (_detailMatch.UsePinyin)
            {
                // 仅当关键词为 ASCII 字母/数字时启用拼音首字母对比
                var asciiKeyword = IsAsciiLettersOrDigits(keyword) ? keyword : null;
                if (!string.IsNullOrEmpty(asciiKeyword))
                {
                    var initialTitle = ToPinyinInitials(title);
                    var initialAuthor = ToPinyinInitials(author);
                    var initialContent = ToPinyinInitials(content);
                    var initialTags = ToPinyinInitials(hashtags);

                    bool PinyinMatch(string src) =>
                        !string.IsNullOrWhiteSpace(src) && src.Contains(NormalizeAscii(asciiKeyword));

                    if (!string.IsNullOrEmpty(title) && PinyinMatch(initialTitle)) score += _detailMatch.TitleWeight * 0.6;
                    if (!string.IsNullOrEmpty(author) && PinyinMatch(initialAuthor)) score += _detailMatch.AuthorWeight * 0.6;
                    if (!string.IsNullOrEmpty(content) && PinyinMatch(initialContent)) score += _detailMatch.ContentWeight * 0.5;
                    if (!string.IsNullOrEmpty(hashtags) && PinyinMatch(initialTags)) score += _detailMatch.HashtagWeight * 0.5;
                }
            }

            var ratio = total <= 0 ? 0 : score / total;
            _logger.LogDebug("详情页匹配评分: {Score}/{Total} ({Ratio:P1})", score, total, ratio);
            return ratio >= _detailMatch.WeightedThreshold;

            bool TitleHit() => !string.IsNullOrWhiteSpace(title) && KeywordMatcher.Matches(title, keyword, options);

            bool AuthorHit() => !string.IsNullOrWhiteSpace(author) && KeywordMatcher.Matches(author, keyword, options);

            bool ContentHit() => !string.IsNullOrWhiteSpace(content) && KeywordMatcher.Matches(content, keyword, options);

            bool TagsHit() => !string.IsNullOrWhiteSpace(hashtags) && KeywordMatcher.Matches(hashtags, keyword, options);

            bool AltHit() => !string.IsNullOrWhiteSpace(imageAlts) && KeywordMatcher.Matches(imageAlts, keyword, options);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "详情页关键词匹配检测异常");
            return false;
        }
    }

    /// <summary>
    /// 从详情页提取用于关键词匹配的文本（标题/作者/描述）
    /// </summary>
    private async Task<(string Title, string Author, string Content, string Hashtags, string ImageAlts)> ExtractDetailFieldsAsync(IPage page)
    {
        string content = string.Empty, tags = string.Empty, alts = string.Empty;

        string title = await FirstTextBySelectors("NoteDetailTitle");
        string author = await FirstTextBySelectors("NoteDetailAuthor");

        // 内容/描述（整块）
        foreach (var sel in _domElementManager.GetSelectors("NoteContent").Concat(["#detail-desc", ".desc", ".note-text", ".note-content", "#note-content", "article"]))
        {
            try
            {
                var el = await page.QuerySelectorAsync(sel);
                if (el == null) continue;
                var t = (await el.InnerTextAsync())?.Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;
                content = t;
                break;
            }
            catch { }
        }

        // 解析正文中的话题标签（#话题 或 #word#）
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                var matches = TagRegex().Matches(content);
                if (matches.Count > 0)
                {
                    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Match m in matches)
                    {
                        var val = m.Groups[1].Value?.Trim();
                        if (!string.IsNullOrWhiteSpace(val)) set.Add(val);
                    }
                    if (set.Count > 0) tags = string.Join(' ', set);
                }
            }
            catch { }
        }

        // 图片 alt 文本
        try
        {
            var imgs = await page.QuerySelectorAllAsync("img[alt]");
            if (imgs.Count > 0)
            {
                var list = new List<string>();
                foreach (var img in imgs)
                {
                    try
                    {
                        var alt = await img.GetAttributeAsync("alt");
                        if (!string.IsNullOrWhiteSpace(alt)) list.Add(alt.Trim());
                    }
                    catch { }
                }
                if (list.Count > 0) alts = string.Join(' ', list);
            }
        }
        catch { }

        return (title, author, content, tags, alts);

        async Task<string> FirstTextBySelectors(string alias)
        {
            foreach (var sel in _domElementManager.GetSelectors(alias))
            {
                try
                {
                    var el = await page.QuerySelectorAsync(sel);
                    if (el == null) continue;
                    var t = (await el.InnerTextAsync())?.Trim();
                    if (!string.IsNullOrWhiteSpace(t)) return t;
                }
                catch { }
            }
            return string.Empty;
        }
    }

    private static bool IsAsciiLettersOrDigits(string s) => !string.IsNullOrWhiteSpace(s) && s.All(ch => ch <= 127 && (char.IsLetterOrDigit(ch)));

    private static string NormalizeAscii(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    // 简易拼音首字母映射：基于 GB2312 分段（启发式，非严格 全量）。
    private static string ToPinyinInitials(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        Encoding? gb2312 = null;
        try { gb2312 = Encoding.GetEncoding("GB2312"); }
        catch
        {
            /* 平台不支持则跳过 */
        }

        foreach (var ch in text)
        {
            if (ch <= 127)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
                continue;
            }

            if (gb2312 == null)
            {
                // 无 GB2312 支持时，跳过该字符
                continue;
            }

            try
            {
                var bytes = gb2312.GetBytes([ch]);
                if (bytes.Length < 2) continue;
                int code = bytes[0] << 8 | bytes[1];
                sb.Append(MapGb2312CodeToInitial(code));
            }
            catch { }
        }

        return sb.ToString();
    }

    private static char MapGb2312CodeToInitial(int code)
    {
        return code switch
        {
            >= 0xB0A1 and <= 0xB0C4 => 'a',
            >= 0xB0C5 and <= 0xB2C0 => 'b',
            >= 0xB2C1 and <= 0xB4ED => 'c',
            >= 0xB4EE and <= 0xB6E9 => 'd',
            >= 0xB6EA and <= 0xB7A1 => 'e',
            >= 0xB7A2 and <= 0xB8C0 => 'f',
            >= 0xB8C1 and <= 0xB9FD => 'g',
            >= 0xB9FE and <= 0xBBF6 => 'h',
            >= 0xBBF7 and <= 0xBFA5 => 'j',
            >= 0xBFA6 and <= 0xC0AB => 'k',
            >= 0xC0AC and <= 0xC2E7 => 'l',
            >= 0xC2E8 and <= 0xC4C2 => 'm',
            >= 0xC4C3 and <= 0xC5B5 => 'n',
            >= 0xC5B6 and <= 0xC5BD => 'o',
            >= 0xC5BE and <= 0xC6D9 => 'p',
            >= 0xC6DA and <= 0xC8BA => 'q',
            >= 0xC8BB and <= 0xC8F5 => 'r',
            >= 0xC8F6 and <= 0xCBF9 => 's',
            >= 0xCBFA and <= 0xCDD9 => 't',
            >= 0xCDDA and <= 0xCEF3 => 'w',
            >= 0xCEF4 and <= 0xD188 => 'x',
            >= 0xD1B9 and <= 0xD4D0 => 'y',
            >= 0xD4D1 and <= 0xD7F9 => 'z',
            _ => ' '
        };
    }
    #endregion

    /// <summary>
    /// 确定处理模式 - 基于笔记类型的智能处理模式选择
    /// </summary>
    private ProcessingMode DetermineProcessingMode(int index, NoteDetail? note = null)
    {
        // 如果有笔记类型信息，优先基于类型决策
        if (note is {Type: not NoteType.Unknown})
        {
            return note.Type switch
            {
                NoteType.Image => ProcessingMode.Fast,     // 图文：快速处理（数量最多，包含长文）
                NoteType.Video => ProcessingMode.Standard, // 视频：标准处理（需要加载视频）
                _ => ProcessingMode.Standard
            };
        }

        // 回退到原有的基于索引的算法
        if (index % 5 == 0) return ProcessingMode.Careful;  // 每5个谨慎处理
        if (index % 3 == 0) return ProcessingMode.Standard; // 每3个标准处理
        return ProcessingMode.Fast;                         // 其余快速处理
    }

    /// <summary>
    /// 确定整体质量 - 新增方法
    /// </summary>
    private DataQuality DetermineOverallQuality(List<NoteDetail> notes)
    {
        if (notes.Count == 0) return DataQuality.Minimal;

        if (notes.All(n => n.Quality == DataQuality.Complete))
            return DataQuality.Complete;
        if (notes.Any(n => n.Quality is DataQuality.Complete or DataQuality.Partial))
            return DataQuality.Partial;

        return DataQuality.Minimal;
    }

    /// <summary>
    /// 验证发布前提条件：根据笔记类型验证媒体文件要求
    /// </summary>
    private Task<bool> ValidatePublishRequirementsAsync(NoteType noteType, List<string>? imagePaths, string? videoPath)
    {
        var hasImages = imagePaths?.Any(File.Exists) == true;
        var hasVideo = !string.IsNullOrEmpty(videoPath) && File.Exists(videoPath);

        var ok = noteType switch
        {
            NoteType.Image => hasImages && !hasVideo,
            NoteType.Video => hasVideo && !hasImages,
            NoteType.Unknown => false, // 未知类型不允许发布
            _ => false
        };

        return Task.FromResult(ok);
    }

    /// <summary>
    /// 等待发布页面准备就绪（Vue.js应用特殊处理）
    /// </summary>
    private async Task WaitForPublishPageReadyAsync(IPage page)
    {
        _logger.LogInformation("等待Vue.js发布页面加载完成...");

        try
        {
            // 1. 等待基础容器出现
            var publishContainer = await _humanizedInteraction.FindElementAsync(page, "PublishContainer", timeout: 15000);
            if (publishContainer == null)
            {
                throw new Exception("发布页面容器未加载");
            }

            // 2. 等待Vue.js组件初始化完成
            await page.WaitForFunctionAsync("""

                                                            () => {
                                                                // 检查Vue应用是否已挂载
                                                                const vueApp = document.querySelector('[data-v-]') || document.querySelector('.vue-publish-container');
                                                                if (!vueApp) return false;
                                                                
                                                                // 检查上传区域是否可见
                                                                const uploadArea = document.querySelector('.upload-area, .image-uploader, .file-drop-zone');
                                                                return uploadArea && uploadArea.offsetHeight > 0;
                                                            }
                                                        
                                            """, new PageWaitForFunctionOptions {Timeout = 15000});

            // 3. 额外的人性化等待（模拟用户观察页面的时间）
            await Task.Delay(Random.Shared.Next(2000, 4000));

            _logger.LogInformation("发布页面加载完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "等待发布页面就绪失败");
            throw new Exception($"发布页面加载超时: {ex.Message}");
        }
    }

    /// <summary>
    /// 专门的图片上传方法 - 小红书创作平台的强制第一步
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage)> UploadImageAsync(IPage page, List<string>? imagePaths, string? videoPath)
    {
        _logger.LogInformation("开始执行媒体文件上传...");

        try
        {
            // 检查是否已登录
            if (!await _accountManager.IsLoggedInAsync())
            {
                return (false, "用户未登录，请先登录");
            }

            // 1. 寻找上传区域（多重备选策略）
            IElementHandle? uploadArea = null;
            var uploadSelectors = _domElementManager.GetSelectors("ImageUploadArea");

            foreach (var selector in uploadSelectors)
            {
                uploadArea = await page.QuerySelectorAsync(selector);
                if (uploadArea != null)
                {
                    _logger.LogInformation("找到上传区域: {Selector}", selector);
                    break;
                }
            }

            if (uploadArea == null)
            {
                return (false, "未找到图片上传区域");
            }

            // 2. 模拟创作者的思考和选择过程
            await _humanizedInteraction.HumanHoverAsync(uploadArea);
            await Task.Delay(Random.Shared.Next(1000, 2000)); // 思考时间

            // 3. 查找文件输入元素
            var fileInputSelectors = _domElementManager.GetSelectors("FileInput");
            IElementHandle? fileInput = null;

            foreach (var selector in fileInputSelectors)
            {
                fileInput = await page.QuerySelectorAsync(selector);
                if (fileInput != null) break;
            }

            if (fileInput == null)
            {
                // 尝试点击上传按钮激活文件选择
                var selectButton = await _humanizedInteraction.FindElementAsync(page, "ImageSelectButton");
                if (selectButton != null)
                {
                    await _humanizedInteraction.HumanClickAsync(selectButton);
                    await Task.Delay(1000);

                    // 重新查找文件输入
                    foreach (var selector in fileInputSelectors)
                    {
                        fileInput = await page.QuerySelectorAsync(selector);
                        if (fileInput != null) break;
                    }
                }
            }

            if (fileInput == null)
            {
                return (false, "未找到文件输入元素");
            }

            // 4. 执行文件上传（优先级：视频 > 图片）
            List<string> filesToUpload = [];

            if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
            {
                filesToUpload.Add(videoPath);
                _logger.LogInformation("准备上传视频: {VideoPath}", videoPath);
            }
            else if (imagePaths != null)
            {
                var validImages = imagePaths.Where(File.Exists).Take(9).ToList(); // 小红书最多9张图片
                filesToUpload.AddRange(validImages);
                _logger.LogInformation("准备上传图片: {ImageCount}张", validImages.Count);
            }

            if (filesToUpload.Count == 0)
            {
                return (false, "没有有效的媒体文件可上传");
            }

            // 5. 执行上传
            await fileInput.SetInputFilesAsync(filesToUpload.ToArray());
            _logger.LogInformation("文件上传指令已发送");

            // 6. 等待上传完成（带进度监控）
            await WaitForUploadCompleteAsync(page);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图片上传异常");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 等待上传完成（带智能进度检测）
    /// </summary>
    private async Task WaitForUploadCompleteAsync(IPage page)
    {
        _logger.LogInformation("等待文件上传完成...");

        try
        {
            // 1. 等待上传进度指示器出现
            var progressSelectors = _domElementManager.GetSelectors("UploadProgress");
            bool progressFound = false;

            for (int i = 0; i < 10; i++) // 最多等待10秒
            {
                foreach (var selector in progressSelectors)
                {
                    var progressElement = await page.QuerySelectorAsync(selector);
                    if (progressElement != null)
                    {
                        progressFound = true;
                        _logger.LogInformation("检测到上传进度指示器");
                        break;
                    }
                }

                if (progressFound) break;
                await Task.Delay(1000);
            }

            // 2. 等待进度完成（进度指示器消失）
            if (progressFound)
            {
                await page.WaitForFunctionAsync("""

                                                                    () => {
                                                                        const progressElements = document.querySelectorAll('.upload-progress, .progress-bar, .uploading');
                                                                        return progressElements.length === 0 || 
                                                                               Array.from(progressElements).every(el => el.style.display === 'none');
                                                                    }
                                                                
                                                """, new PageWaitForFunctionOptions {Timeout = 30000});
            }

            // 3. 验证上传成功（图片预览出现）
            await page.WaitForFunctionAsync("""

                                                            () => {
                                                                const previewElements = document.querySelectorAll('.image-preview, .preview-container, .uploaded-images');
                                                                return previewElements.length > 0 && 
                                                                       Array.from(previewElements).some(el => el.offsetHeight > 0);
                                                            }
                                                        
                                            """, new PageWaitForFunctionOptions {Timeout = 15000});

            _logger.LogInformation("文件上传完成，预览已显示");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "上传完成检测超时，继续执行后续步骤");
            // 不抛出异常，允许继续执行
        }
    }

    /// <summary>
    /// 等待编辑器准备就绪（只有上传成功后编辑器才会出现）
    /// </summary>
    private async Task WaitForEditorReadyAsync(IPage page)
    {
        _logger.LogInformation("等待文本编辑器就绪...");

        try
        {
            // 1. 等待编辑器容器出现
            var editorContainer = await _humanizedInteraction.FindElementAsync(page, "EditorContainer", timeout: 15000);
            if (editorContainer == null)
            {
                throw new Exception("编辑器容器未出现");
            }

            // 2. 等待编辑器完全初始化（基于真实HTML结构）
            await page.WaitForFunctionAsync("""

                                                            () => {
                                                                // 检查标题输入框（使用真实placeholder）
                                                                const titleInput = document.querySelector('input[placeholder*="填写标题会有更多赞哦"]') ||
                                                                                 document.querySelector('.d-text') ||
                                                                                 document.querySelector('input[placeholder*="标题"]');
                                                                if (!titleInput || titleInput.disabled) return false;
                                                                
                                                                // 检查TipTap富文本编辑器
                                                                const tipTapEditor = document.querySelector('.tiptap.ProseMirror');
                                                                if (!tipTapEditor) return false;
                                                                
                                                                // 确保TipTap编辑器可编辑
                                                                if (tipTapEditor.getAttribute('contenteditable') !== 'true') return false;
                                                                
                                                                // 检查编辑器是否已经初始化完成（不再显示加载状态）
                                                                const isLoading = document.querySelector('.editor-loading, .loading-editor');
                                                                if (isLoading) return false;
                                                                
                                                                // 确保编辑器有正确的tabindex（表示可聚焦）
                                                                const tabIndex = tipTapEditor.getAttribute('tabindex');
                                                                if (tabIndex === null || parseInt(tabIndex) < 0) return false;
                                                                
                                                                return true;
                                                            }
                                                        
                                            """, new PageWaitForFunctionOptions {Timeout = 15000});

            // 3. 人性化的观察时间
            await Task.Delay(Random.Shared.Next(1500, 3000));

            _logger.LogInformation("TipTap富文本编辑器准备完成，可以开始输入内容");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "等待编辑器就绪失败");
            throw new Exception($"编辑器初始化超时: {ex.Message}");
        }
    }

    /// <summary>
    /// 模拟真实创作者的工作流程
    /// </summary>
    private async Task SimulateCreatorWorkflowAsync(IPage page, string title, string content, List<string> tags)
    {
        _logger.LogInformation("模拟创作者工作流程: 输入内容和标签");

        try
        {
            // 1. 首先聚焦到标题输入框（创作者通常先写标题）
            var titleElement = await _humanizedInteraction.FindElementAsync(page, "PublishTitleInput");
            if (titleElement != null)
            {
                await _humanizedInteraction.HumanClickAsync(titleElement);
                await Task.Delay(Random.Shared.Next(500, 1000)); // 思考时间

                // 模拟打字的节奏
                await _humanizedInteraction.HumanTypeAsync(page, "PublishTitleInput", title);
                _logger.LogInformation("标题输入完成");
            }

            // 2. 短暂停顿，模拟从标题思考到内容的过程
            await Task.Delay(Random.Shared.Next(1000, 2000));

            // 3. 输入正文内容
            var contentElement = await _humanizedInteraction.FindElementAsync(page, "PublishContentInput");
            if (contentElement != null)
            {
                await _humanizedInteraction.HumanClickAsync(contentElement);
                await Task.Delay(Random.Shared.Next(800, 1500)); // 准备写内容的思考时间

                // 分段输入内容（模拟创作者边写边思考）
                await SimulateContentInputAsync(page, content);
                _logger.LogInformation("内容输入完成");
            }

            // 4. 添加标签（创作者通常最后考虑标签）
            if (tags.Count != 0)
            {
                await Task.Delay(Random.Shared.Next(1500, 2500)); // 思考标签的时间
                await AddTagsWithHumanBehaviorAsync(page, tags);
            }

            _logger.LogInformation("创作者工作流程模拟完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模拟创作者工作流程失败");
            throw;
        }
    }

    /// <summary>
    /// 模拟内容分段输入（更自然的写作行为）
    /// </summary>
    private async Task SimulateContentInputAsync(IPage page, string content)
    {
        if (string.IsNullOrEmpty(content)) return;

        try
        {
            // 将内容分段（按句号、换行等分割）
            var segments = content.Split(['。', '\n', '！', '？'], StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (segments.Count == 0)
            {
                // 如果分段失败，直接输入完整内容
                await _humanizedInteraction.HumanTypeAsync(page, "PublishContentInput", content);
                return;
            }

            // 逐段输入，模拟边写边思考的过程
            var contentInput = await _humanizedInteraction.FindElementAsync(page, "PublishContentInput");
            if (contentInput == null) return;

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i].Trim();
                if (string.IsNullOrEmpty(segment)) continue;

                // 输入这一段（使用键盘输入替代过时的 ElementHandle.TypeAsync）
                await contentInput.FocusAsync();
                await page.Keyboard.TypeAsync(segment, new KeyboardTypeOptions
                {
                    Delay = Random.Shared.Next(50, 150)
                });

                // 补充标点符号（如果原文有的话）
                if (i < segments.Count - 1)
                {
                    var punctuation = content.Contains(segment + "。") ? "。" :
                        content.Contains(segment + "！") ? "！" :
                        content.Contains(segment + "？") ? "？" : "";
                    if (!string.IsNullOrEmpty(punctuation))
                    {
                        await contentInput.FocusAsync();
                        await page.Keyboard.TypeAsync(punctuation);
                    }

                    // 如果原文有换行，保留换行
                    if (content.Contains(segment + "\n") || content.Contains("\n" + segment))
                    {
                        await contentInput.PressAsync("Enter");
                    }
                }

                // 段落间的思考停顿
                if (i < segments.Count - 1)
                {
                    await Task.Delay(Random.Shared.Next(800, 2000));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "分段输入失败，使用直接输入");
            await _humanizedInteraction.HumanTypeAsync(page, "PublishContentInput", content);
        }
    }

    /// <summary>
    /// 人性化添加标签
    /// </summary>
    private async Task AddTagsWithHumanBehaviorAsync(IPage page, List<string> tags)
    {
        if (tags.Count == 0) return;

        try
        {
            var tagInput = await _humanizedInteraction.FindElementAsync(page, "PublishTagInput");
            if (tagInput == null)
            {
                _logger.LogWarning("未找到标签输入框，跳过标签添加");
                return;
            }

            // 最多添加10个标签（小红书限制）
            var tagsToAdd = tags.Take(10).ToList();

            for (int i = 0; i < tagsToAdd.Count; i++)
            {
                var tag = tagsToAdd[i].Trim().TrimStart('#'); // 移除可能存在的#

                if (string.IsNullOrEmpty(tag)) continue;

                // 点击标签输入框
                await _humanizedInteraction.HumanClickAsync(tagInput);
                await Task.Delay(Random.Shared.Next(300, 600));

                // 输入标签（有些平台需要#，有些不需要）
                var tagText = tag.StartsWith("#") ? tag : $"#{tag}";
                await tagInput.FocusAsync();
                await page.Keyboard.TypeAsync(tagText, new KeyboardTypeOptions
                {
                    Delay = Random.Shared.Next(80, 150)
                });

                // 确认标签（回车或空格）
                await Task.Delay(Random.Shared.Next(200, 400));
                await tagInput.PressAsync("Enter");

                // 标签间的停顿
                if (i < tagsToAdd.Count - 1)
                {
                    await Task.Delay(Random.Shared.Next(500, 1200));
                }
            }

            _logger.LogInformation("标签添加完成: {TagCount}个", tagsToAdd.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "添加标签失败，继续执行后续步骤");
        }
    }

    /// <summary>
    /// 暂存离开（安全模式）- 点击暂存离开按钮，不重试
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage, string? DraftId)> TemporarySaveAndLeaveInternalAsync(IPage page)
    {
        _logger.LogInformation("点击暂存离开按钮（安全模式）");

        try
        {
            // 1. 查找暂存离开按钮
            var tempSaveButton = await _humanizedInteraction.FindElementAsync(page, "TemporarySaveButton", timeout: 10000);
            if (tempSaveButton == null)
            {
                return (false, "未找到暂存离开按钮", null);
            }

            // 2. 模拟创作者的最后检查过程
            await Task.Delay(Random.Shared.Next(2000, 4000)); // 检查内容的时间

            // 3. 点击暂存离开
            await _humanizedInteraction.HumanClickAsync(tempSaveButton);
            _logger.LogInformation("已点击暂存离开按钮");

            // 4. 等待编辑器消失（暂存成功的标志）
            await WaitForEditorDisappearAsync(page);

            // 5. 生成草稿ID（实际应用中可能从页面提取）
            var draftId = Guid.NewGuid().ToString();

            return (true, null, draftId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "暂存离开操作异常");
            return (false, $"暂存离开失败: {ex.Message}，请手动检查页面状态", null);
        }
    }

    /// <summary>
    /// 等待编辑器消失（暂存成功的标志）- 增强版双重确认
    /// 不仅检测编辑器消失，还确认回到上传状态
    /// </summary>
    private async Task WaitForEditorDisappearAsync(IPage page)
    {
        try
        {
            _logger.LogInformation("等待编辑器消失并确认回到上传状态（确认暂存成功）...");

            // 等待编辑器消失 + 上传区域重新出现（双重确认暂存成功）
            await page.WaitForFunctionAsync("""

                                                            () => {
                                                                // 1. 检查编辑器是否消失（使用真实选择器）
                                                                const titleInput = document.querySelector('input[placeholder*="填写标题会有更多赞哦"]') ||
                                                                                 document.querySelector('.d-text');
                                                                
                                                                // 检查TipTap富文本编辑器是否消失
                                                                const tipTapEditor = document.querySelector('.tiptap.ProseMirror');
                                                                
                                                                // 检查编辑器容器是否消失
                                                                const editorContainer = document.querySelector('.editor-container');
                                                                
                                                                // 编辑器相关元素都消失或不可见
                                                                const editorGone = (!titleInput || titleInput.style.display === 'none' || !titleInput.offsetParent) &&
                                                                                  (!tipTapEditor || tipTapEditor.style.display === 'none' || !tipTapEditor.offsetParent) &&
                                                                                  (!editorContainer || editorContainer.style.display === 'none' || !editorContainer.offsetParent);
                                                                
                                                                // 2. 检查上传区域是否重新出现（回到初始状态的确认）
                                                                const uploadWrapper = document.querySelector('.upload-wrapper');
                                                                const dragArea = document.querySelector('.drag-over');
                                                                const uploadButton = document.querySelector('.el-button.upload-button') || 
                                                                                   document.querySelector('.upload-button');
                                                                const dragText = document.querySelector('p')?.innerText?.includes('拖拽图片到此或点击上传');
                                                                
                                                                // 上传状态特征元素重新可见
                                                                const uploadVisible = (uploadWrapper && uploadWrapper.offsetHeight > 0) ||
                                                                                     (dragArea && dragArea.offsetHeight > 0) ||
                                                                                     (uploadButton && uploadButton.offsetHeight > 0) ||
                                                                                     dragText;
                                                                
                                                                // 双重确认：编辑器消失 && 上传区域重现
                                                                const backToUploadState = editorGone && uploadVisible;
                                                                
                                                                if (backToUploadState) {
                                                                    console.log('暂存成功确认：编辑器已消失，已回到上传状态');
                                                                }
                                                                
                                                                return backToUploadState;
                                                            }
                                                        
                                            """, new PageWaitForFunctionOptions {Timeout = 15000});

            _logger.LogInformation("暂存操作确认成功：编辑器已消失，已回到上传状态");

            // 额外等待，确保页面状态完全稳定
            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "等待编辑器消失超时，但暂存操作可能已成功");

            // 尝试简单检测确认
            try
            {
                var uploadWrapper = await page.QuerySelectorAsync(".upload-wrapper");
                if (uploadWrapper != null)
                {
                    _logger.LogInformation("检测到上传区域重现，暂存操作可能已成功");
                }
                else
                {
                    _logger.LogWarning("未检测到上传区域，暂存状态不明确");
                }
            }
            catch
            {
                _logger.LogWarning("无法确认页面状态，请手动检查暂存结果");
            }

            // 不抛出异常，因为操作可能已经成功
        }
    }

    /// <summary>
    /// 查找匹配笔记 - 支持虚拟化列表的智能滚动搜索
    /// 通过渐进式搜索和智能滚动处理动态加载的内容
    /// </summary>
    private async Task<OperationResult<List<IElementHandle>>> FindVisibleMatchingNotesAsync(string keyword, int maxCount, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await _browserManager.GetPageAsync();
            var foundNotes = new List<IElementHandle>();
            var processedIds = new HashSet<string>(); // 去重机制
            var scrollAttempts = 0;

            string stopReason = "未知";

            _logger.LogInformation("开始虚拟化列表滚动搜索，目标: {MaxCount} 个笔记", maxCount);

            while (foundNotes.Count < maxCount)
            {
                // 检查取消请求
                cancellationToken.ThrowIfCancellationRequested();

                // 1. 搜索当前可见区域的匹配笔记
                var currentMatches = await SearchCurrentVisibleAreaAsync(keyword, maxCount - foundNotes.Count);

                var newNotesCount = 0;

                // 2. 去重并添加新找到的笔记
                foreach (var match in currentMatches.Data!)
                {
                    try
                    {
                        var noteId = await ExtractNoteIdFromElementAsync(match);
                        if (!string.IsNullOrEmpty(noteId) && !processedIds.Contains(noteId))
                        {
                            foundNotes.Add(match);
                            processedIds.Add(noteId);
                            newNotesCount++;

                            if (foundNotes.Count >= maxCount) break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("提取笔记ID时出错: {Error}", ex.Message);
                        // 对于无法提取ID的元素，仍然添加但使用随机ID避免重复
                        var fallbackId = Guid.NewGuid().ToString();
                        if (!processedIds.Contains(fallbackId))
                        {
                            foundNotes.Add(match);
                            processedIds.Add(fallbackId);
                            newNotesCount++;
                        }
                    }
                }

                _logger.LogDebug("第 {Attempt} 轮搜索找到 {NewCount} 个新笔记，总计: {Total}/{Target}",
                    scrollAttempts + 1, newNotesCount, foundNotes.Count, maxCount);

                // 4. 执行拟人化滚动加载更多内容
                _logger.LogDebug("执行第 {Attempt} 次滚动加载更多内容", scrollAttempts + 1);

                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ScrollPreparation, cancellationToken: cancellationToken);
                await _humanizedInteraction.HumanScrollAsync(page, targetDistance: 0, cancellationToken: cancellationToken);
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.VirtualListUpdate, cancellationToken: cancellationToken);

                scrollAttempts++;
            }

            if (string.IsNullOrEmpty(stopReason))
            {
                stopReason = "循环条件结束";
            }

            _logger.LogInformation("虚拟化列表搜索完成，共找到 {Count} 个匹配笔记，执行了 {ScrollAttempts} 次滚动，原因: {Reason}",
                foundNotes.Count, scrollAttempts, stopReason);

            return OperationResult<List<IElementHandle>>.Ok(foundNotes);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("虚拟化列表滚动搜索被取消");
            return OperationResult<List<IElementHandle>>.Fail(
                "虚拟化列表搜索被取消",
                ErrorType.OperationCancelled,
                "VIRTUALIZED_SEARCH_CANCELLED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "虚拟化列表滚动搜索失败");
            return OperationResult<List<IElementHandle>>.Fail(
                $"虚拟化列表搜索失败: {ex.Message}",
                ErrorType.BrowserError,
                "VIRTUALIZED_SEARCH_FAILED");
        }
    }

    /// <summary>
    /// 搜索当前可见区域的匹配笔记
    /// 专门用于虚拟化列表的分阶段搜索
    /// </summary>
    private async Task<OperationResult<List<IElementHandle>>> SearchCurrentVisibleAreaAsync(string keyword, int maxCount)
    {
        try
        {
            var page = await _browserManager.GetPageAsync();
            var noteItemSelectors = _domElementManager.GetSelectors("NoteItem");
            var allNoteElements = new List<IElementHandle>();

            // 尝试不同的选择器找到当前DOM中的笔记元素
            foreach (var selector in noteItemSelectors)
            {
                try
                {
                    var elements = await page.QuerySelectorAllAsync(selector);
                    if (elements.Any())
                    {
                        allNoteElements.AddRange(elements);
                        _logger.LogDebug("当前区域使用选择器 {Selector} 找到 {Count} 个笔记元素", selector, elements.Count);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("当前区域选择器 {Selector} 查找失败: {Error}", selector, ex.Message);
                }
            }

            if (allNoteElements.Count == 0)
            {
                return OperationResult<List<IElementHandle>>.Ok([]);
            }

            // 如果没有关键词，返回所有可见元素
            if (string.IsNullOrWhiteSpace(keyword))
            {
                var visibleNotes = await FilterVisibleElements(allNoteElements);
                return OperationResult<List<IElementHandle>>.Ok(visibleNotes.Take(maxCount).ToList());
            }

            // 基于关键词匹配过滤笔记
            var matchingNotes = new List<IElementHandle>();

            foreach (var noteElement in allNoteElements)
            {
                if (matchingNotes.Count >= maxCount) break;

                try
                {
                    // 检查元素是否在视窗内可见
                    if (!await IsElementVisible(noteElement)) continue;

                    // 提取笔记文本进行匹配
                    var noteText = await ExtractNoteTextForMatching(noteElement);
                    if (string.IsNullOrEmpty(noteText)) continue;

                    // 关键词匹配
                    if (MatchesKeyword(noteText, keyword))
                    {
                        matchingNotes.Add(noteElement);
                        _logger.LogDebug("当前区域找到匹配笔记: {Text}", noteText.Substring(0, Math.Min(50, noteText.Length)));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("处理当前区域笔记元素时出错: {Error}", ex.Message);
                }
            }

            return OperationResult<List<IElementHandle>>.Ok(matchingNotes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索当前可见区域失败");
            return OperationResult<List<IElementHandle>>.Fail(
                $"搜索当前可见区域失败: {ex.Message}",
                ErrorType.BrowserError,
                "SEARCH_CURRENT_AREA_FAILED");
        }
    }

    /// <summary>
    /// 从笔记元素中提取唯一ID用于去重
    /// 尝试多种方式获取笔记的唯一标识
    /// </summary>
    private async Task<string> ExtractNoteIdFromElementAsync(IElementHandle noteElement)
    {
        try
        {
            // 方式1: 尝试从href属性中提取note ID
            var linkElement = await noteElement.QuerySelectorAsync("a[href*='/explore/']")
                              ?? await noteElement.QuerySelectorAsync("a[href*='/discovery/item/']")
                              ?? await noteElement.QuerySelectorAsync("a");

            if (linkElement != null)
            {
                var href = await linkElement.GetAttributeAsync("href");
                if (!string.IsNullOrEmpty(href))
                {
                    // 从URL中提取note ID
                    var matches = Regex.Match(href, @"(?:explore|discovery/item)/([a-f0-9]{24})")
                                  ?? Regex.Match(href, @"/([a-f0-9]{24})(?:\?|$)");
                    if (matches.Success)
                    {
                        return matches.Groups[1].Value;
                    }

                    // 如果没有标准格式，使用完整href作为ID
                    return href;
                }
            }

            // 方式2: 尝试从data-*属性中获取
            var dataId = await noteElement.GetAttributeAsync("data-id")
                         ?? await noteElement.GetAttributeAsync("data-note-id")
                         ?? await noteElement.GetAttributeAsync("data-item-id");
            if (!string.IsNullOrEmpty(dataId))
            {
                return dataId;
            }

            // 方式3: 尝试从子元素的属性中获取
            var imgElement = await noteElement.QuerySelectorAsync("img");
            if (imgElement != null)
            {
                var imgSrc = await imgElement.GetAttributeAsync("src");
                if (!string.IsNullOrEmpty(imgSrc) && imgSrc.Contains("/"))
                {
                    // 使用图片URL的一部分作为标识
                    var parts = imgSrc.Split('/');
                    for (int i = parts.Length - 1; i >= 0; i--)
                    {
                        if (parts[i].Length > 10) // 寻找足够长的标识符
                        {
                            return parts[i].Split('?')[0]; // 去掉query参数
                        }
                    }
                }
            }

            // 方式4: 生成基于内容的哈希ID
            var textContent = await noteElement.InnerTextAsync();
            if (!string.IsNullOrEmpty(textContent))
            {
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(textContent.Trim()));
                return Convert.ToHexString(hashBytes)[..16]; // 取前16个字符
            }

            // 最后兜底: 返回空字符串，调用方会处理
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("提取笔记ID失败: {Error}", ex.Message);
            return string.Empty;
        }
    }

    /// <summary>
    /// 过滤可见的元素
    /// </summary>
    private async Task<List<IElementHandle>> FilterVisibleElements(List<IElementHandle> elements)
    {
        var visibleElements = new List<IElementHandle>();

        foreach (var element in elements)
        {
            try
            {
                if (await IsElementVisible(element))
                {
                    visibleElements.Add(element);
                }
            }
            catch
            {
                // 忽略检查错误的元素
            }
        }

        return visibleElements;
    }

    /// <summary>
    /// 检查元素是否可见
    /// </summary>
    private async Task<bool> IsElementVisible(IElementHandle element)
    {
        try
        {
            var boundingBox = await element.BoundingBoxAsync();
            return boundingBox is {Height: > 0, Width: > 0};
        }
        catch
        {
            return false;
        }
    }

    #region 辅助方法
    /// <summary>
    /// 提取笔记文本用于关键词匹配
    /// </summary>
    private async Task<string> ExtractNoteTextForMatching(IElementHandle noteElement)
    {
        try
        {
            var textParts = new List<string>();

            // 提取标题
            var titleSelectors = new[] {".title", ".note-title", ".content", "[title]"};
            foreach (var selector in titleSelectors)
            {
                try
                {
                    var titleElement = await noteElement.QuerySelectorAsync(selector);
                    if (titleElement != null)
                    {
                        var titleText = await titleElement.InnerTextAsync();
                        if (!string.IsNullOrWhiteSpace(titleText))
                        {
                            textParts.Add(titleText.Trim());
                        }
                    }
                }
                catch { }
            }

            // 提取作者
            var authorSelectors = new[] {".author", ".username", ".user-name"};
            foreach (var selector in authorSelectors)
            {
                try
                {
                    var authorElement = await noteElement.QuerySelectorAsync(selector);
                    if (authorElement != null)
                    {
                        var authorText = await authorElement.InnerTextAsync();
                        if (!string.IsNullOrWhiteSpace(authorText))
                        {
                            textParts.Add(authorText.Trim());
                        }
                    }
                }
                catch { }
            }

            return string.Join(" ", textParts);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 关键词匹配逻辑（增强版包装，已切换到“单字符串关键词”）：
    /// 复用 <see cref="KeywordMatcher"/> 的鲁棒匹配能力。
    /// </summary>
    private bool MatchesKeyword(string text, string keyword)
    {
        // 与详情页匹配保持一致：透传 _detailMatch 配置到 KeywordMatcher
        var options = new KeywordMatchOptions
        {
            UseFuzzy = _detailMatch.UseFuzzy,
            MaxDistanceCap = _detailMatch.MaxDistanceCap,
            TokenCoverageThreshold = _detailMatch.TokenCoverageThreshold,
            IgnoreSpaces = _detailMatch.IgnoreSpaces
        };
        return KeywordMatcher.Matches(text, keyword, options);
    }
    #endregion

    #region 笔记互动功能
    /// <summary>
    /// 基于关键词定位并点赞笔记（封装：转由 InteractNoteAsync 统一实现）。
    /// </summary>
    public async Task<OperationResult<InteractionResult>> LikeNoteAsync(string keyword)
    {
        var combo = await InteractNoteAsync(keyword, doLike: true, doFavorite: false);
        if (!combo.Success)
        {
            // 失败时将组合错误映射为单交互错误
            return OperationResult<InteractionResult>.Fail(combo.ErrorMessage ?? combo.ErrorCode ?? "LIKE_NOTE_FAILED", ErrorType.Unknown, combo.ErrorCode ?? "LIKE_NOTE_FAILED");
        }
        return OperationResult<InteractionResult>.Ok(combo.Data!.Like ?? new InteractionResult(false, "点赞", "未知", "未知", "无点赞结果", "LIKE_RESULT_MISSING"));
    }
    /// <summary>
    /// 基于关键词定位并收藏笔记（破坏性变更：单关键词）
    /// </summary>
    /// <param name="keyword">关键词（单字符串）</param>
    /// <returns>收藏操作结果</returns>
    public async Task<OperationResult<InteractionResult>> FavoriteNoteAsync(string keyword)
    {
        var combo = await InteractNoteAsync(keyword, doLike: false, doFavorite: true);
        if (!combo.Success)
        {
            return OperationResult<InteractionResult>.Fail(combo.ErrorMessage ?? combo.ErrorCode ?? "FAVORITE_NOTE_FAILED", ErrorType.Unknown, combo.ErrorCode ?? "FAVORITE_NOTE_FAILED");
        }
        return OperationResult<InteractionResult>.Ok(combo.Data!.Favorite ?? new InteractionResult(false, "收藏", "未知", "未知", "无收藏结果", "FAVORITE_RESULT_MISSING"));
    }

    /// <summary>
    /// 统一交互：基于关键字定位目标笔记，并按需执行点赞/收藏（可组合）。
    /// 满足“若启动前已处于详情弹窗则先关闭”的新行为约定，以确保 API 监听可靠。
    /// </summary>
    public async Task<OperationResult<InteractionBundleResult>> InteractNoteAsync(string keyword, bool doLike, bool doFavorite)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("开始交互（点赞/收藏）：关键词={Keyword}，Like={Like}，Favorite={Favorite}", keyword, doLike, doFavorite);
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return OperationResult<InteractionBundleResult>.Fail("关键词不能为空", ErrorType.ValidationError, "EMPTY_KEYWORD");
            if (!doLike && !doFavorite)
                return OperationResult<InteractionBundleResult>.Fail("未选择任何交互（like/favorite）", ErrorType.ValidationError, "NO_ACTION_SELECTED");
            if (!await _accountManager.IsLoggedInAsync())
                return OperationResult<InteractionBundleResult>.Fail("用户未登录，请先登录", ErrorType.LoginRequired, "NOT_LOGGED_IN");

            var page = await _browserManager.GetPageAsync();

            // 导航至入口页
            var ensured = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(page);
            if (!ensured)
                return OperationResult<InteractionBundleResult>.Fail("无法导航至探索/发现/搜索页面", ErrorType.NavigationError, "ENTRY_PAGE_NOT_AVAILABLE");

            // 查找并打开目标
            var search = await FindVisibleMatchingNotesAsync(keyword, 1);
            if (!search.Success || search.Data is {Count: 0})
                return OperationResult<InteractionBundleResult>.Fail($"未找到匹配关键词的笔记: {keyword}", ErrorType.ElementNotFound, "NO_MATCHING_NOTES");

            var target = search.Data!.First();
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);
            await _humanizedInteraction.HumanClickAsync(target);
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);

            InteractionResult? likeRes = null;
            InteractionResult? favRes = null;

            // 点赞
            if (doLike)
            {
                var endpoints = new HashSet<ApiEndpointType> {ApiEndpointType.LikeNote, ApiEndpointType.DislikeNote};
                _universalApiMonitor.SetupMonitor(page, endpoints);
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.LikeNote);
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.DislikeNote);

                var likeResult = await _humanizedInteraction.HumanLikeAsync();
                likeRes = likeResult;
                if (likeResult.Success && !string.Equals(likeResult.ErrorCode, "ALREADY_LIKED", StringComparison.OrdinalIgnoreCase))
                {
                    var perAttemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1, _endpointRetry.AttemptTimeoutMs));
                    var gotLike = await _universalApiMonitor.WaitForResponsesAsync(ApiEndpointType.LikeNote, perAttemptTimeout, 1);
                    if (!gotLike)
                    {
                        var dislikeRaws = _universalApiMonitor.GetRawResponses(ApiEndpointType.DislikeNote);
                        if (dislikeRaws.Count > 0)
                        {
                            likeRes = new InteractionResult(false, "点赞", likeResult.PreviousState, likeResult.CurrentState,
                                "点赞失败：捕获到取消点赞请求（可能初始已点赞或状态识别偏差）", "UNEXPECTED_DISLIKE_CAPTURED");
                        }
                        else
                        {
                            likeRes = new InteractionResult(false, "点赞", likeResult.PreviousState, likeResult.CurrentState,
                                "点赞失败：未捕获网络确认", "LIKE_API_NOT_CONFIRMED");
                        }
                    }
                }

                await _humanizedInteraction.HumanBetweenActionsDelayAsync();
            }

            // 收藏
            if (doFavorite)
            {
                var endpoints = new HashSet<ApiEndpointType> {ApiEndpointType.CollectNote, ApiEndpointType.UncollectNote};
                _universalApiMonitor.SetupMonitor(page, endpoints);
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.CollectNote);
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.UncollectNote);

                var favoriteResult = await _humanizedInteraction.HumanFavoriteAsync(page);
                favRes = favoriteResult;
                if (favoriteResult.Success && !string.Equals(favoriteResult.ErrorCode, "ALREADY_FAVORITED", StringComparison.OrdinalIgnoreCase))
                {
                    var perAttemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1, _endpointRetry.AttemptTimeoutMs));
                    var gotCollect = await _universalApiMonitor.WaitForResponsesAsync(ApiEndpointType.CollectNote, perAttemptTimeout, 1);
                    if (!gotCollect)
                    {
                        var uncollectRaws = _universalApiMonitor.GetRawResponses(ApiEndpointType.UncollectNote);
                        if (uncollectRaws.Count > 0)
                        {
                            favRes = new InteractionResult(false, "收藏", favoriteResult.PreviousState, favoriteResult.CurrentState,
                                "收藏失败：捕获到取消收藏请求（可能初始已收藏或状态识别偏差）", "UNEXPECTED_UNCOLLECT_CAPTURED");
                        }
                        else
                        {
                            favRes = new InteractionResult(false, "收藏", favoriteResult.PreviousState, favoriteResult.CurrentState,
                                "收藏失败：未捕获网络确认", "COLLECT_API_NOT_CONFIRMED");
                        }
                    }
                }

                await _humanizedInteraction.HumanBetweenActionsDelayAsync();
            }

            sw.Stop();
            var ok = (!doLike || (likeRes?.Success ?? false)) && (!doFavorite || (favRes?.Success ?? false));
            var msg = doLike && doFavorite
                ? $"点赞：{(likeRes?.Success == true ? "成功" : "失败")}；收藏：{(favRes?.Success == true ? "成功" : "失败")}"
                : doLike
                    ? (likeRes?.Message ?? "完成")
                    : (favRes?.Message ?? "完成");
            var bundle = new InteractionBundleResult(ok, likeRes, favRes, msg);
            return OperationResult<InteractionBundleResult>.Ok(bundle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "交互（点赞/收藏）操作异常: 关键词={Keyword}", keyword);
            return OperationResult<InteractionBundleResult>.Fail($"交互异常: {ex.Message}", ErrorType.Unknown, "INTERACT_NOTE_EXCEPTION");
        }
    }
    /// </summary>
    private void LogInteractionAudit(string action, string keyword, bool domVerified, bool apiConfirmed, TimeSpan duration, string? extra)
    {
        _logger.LogInformation("[Audit] 动作={Action} | 关键词={Keyword} | DOM校验={Dom} | API确认={Api} | 耗时={Elapsed}ms | 备注={Extra}",
            action, keyword, domVerified, apiConfirmed, (long)duration.TotalMilliseconds, extra ?? "-");
    }

    /// <summary>
    /// 基于关键词定位并取消点赞（新）
    /// </summary>
    public async Task<OperationResult<InteractionResult>> UnlikeNoteAsync(string keyword)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("开始基于关键词取消点赞: 关键词={Keyword}", keyword);
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return OperationResult<InteractionResult>.Fail("关键词不能为空", ErrorType.ValidationError, "EMPTY_KEYWORD");
            if (!await _accountManager.IsLoggedInAsync())
                return OperationResult<InteractionResult>.Fail("用户未登录，请先登录", ErrorType.LoginRequired, "NOT_LOGGED_IN");

            var page = await _browserManager.GetPageAsync();
            var status = await GetCurrentPageStatusAsync(page, PageType.NoteDetail);
            if (status.PageType == PageType.NoteDetail)
            {
                if (!await DoesCurrentDetailMatchKeywords(page, keyword))
                    return OperationResult<InteractionResult>.Fail("当前详情页与关键词不匹配（不回退、不滚动）", ErrorType.ElementNotFound, "DETAIL_NOT_MATCHED");

                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);
                var endpoints = new HashSet<ApiEndpointType> {ApiEndpointType.DislikeNote};
                _universalApiMonitor.SetupMonitor(page, endpoints);
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.DislikeNote);

                var result = await _humanizedInteraction.HumanUnlikeAsync(page);
                if (!result.Success)
                    return OperationResult<InteractionResult>.Fail(result.Message, ErrorType.BrowserError, result.ErrorCode ?? "UNLIKE_FAILED");

                var perAttemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1, _endpointRetry.AttemptTimeoutMs));
                var got = await _universalApiMonitor.WaitForResponsesAsync(ApiEndpointType.DislikeNote, perAttemptTimeout, 1);
                if (!got)
                {
                    sw.Stop();
                    LogInteractionAudit("取消点赞", keyword, domVerified: true, apiConfirmed: false, duration: sw.Elapsed, extra: null);
                    return OperationResult<InteractionResult>.Fail("取消点赞失败：未捕获网络确认", ErrorType.NetworkError, "UNLIKE_API_NOT_CONFIRMED");
                }
                sw.Stop();
                LogInteractionAudit("取消点赞", keyword, domVerified: true, apiConfirmed: true, duration: sw.Elapsed, extra: null);
                await _humanizedInteraction.HumanBetweenActionsDelayAsync();
                return OperationResult<InteractionResult>.Ok(result);
            }

            var ensured = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(page);
            if (!ensured)
                return OperationResult<InteractionResult>.Fail("无法导航至探索/发现/搜索页面", ErrorType.NavigationError, "ENTRY_PAGE_NOT_AVAILABLE");
            var search = await FindVisibleMatchingNotesAsync(keyword, 1);
            if (!search.Success || search.Data is {Count: 0})
                return OperationResult<InteractionResult>.Fail($"未找到匹配关键词的笔记: {keyword}", ErrorType.ElementNotFound, "NO_MATCHING_NOTES");
            var target = search.Data!.First();
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);
            await _humanizedInteraction.HumanClickAsync(target);
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);
            return await UnlikeNoteAsync(keyword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消点赞操作异常: 关键词={Keyword}", keyword);
            var errorResult = new InteractionResult(false, "取消点赞", "未知", "未知", $"取消点赞异常: {ex.Message}", "UNLIKE_OPERATION_EXCEPTION");
            return OperationResult<InteractionResult>.Ok(errorResult);
        }
    }

    /// <summary>
    /// 基于关键词定位并取消收藏（新）
    /// </summary>
    public async Task<OperationResult<InteractionResult>> UncollectNoteAsync(string keyword)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("开始基于关键词取消收藏: 关键词={Keyword}", keyword);
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return OperationResult<InteractionResult>.Fail("关键词不能为空", ErrorType.ValidationError, "EMPTY_KEYWORD");
            if (!await _accountManager.IsLoggedInAsync())
                return OperationResult<InteractionResult>.Fail("用户未登录，请先登录", ErrorType.LoginRequired, "NOT_LOGGED_IN");

            var page = await _browserManager.GetPageAsync();
            var status = await GetCurrentPageStatusAsync(page, PageType.NoteDetail);
            if (status.PageType == PageType.NoteDetail)
            {
                if (!await DoesCurrentDetailMatchKeywords(page, keyword))
                    return OperationResult<InteractionResult>.Fail("当前详情页与关键词不匹配（不回退、不滚动）", ErrorType.ElementNotFound, "DETAIL_NOT_MATCHED");

                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);
                var endpoints = new HashSet<ApiEndpointType> {ApiEndpointType.UncollectNote};
                _universalApiMonitor.SetupMonitor(page, endpoints);
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.UncollectNote);

                var result = await _humanizedInteraction.HumanUnfavoriteAsync(page);
                if (!result.Success)
                    return OperationResult<InteractionResult>.Fail(result.Message, ErrorType.BrowserError, result.ErrorCode ?? "UNFAVORITE_FAILED");

                var perAttemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1, _endpointRetry.AttemptTimeoutMs));
                var got = await _universalApiMonitor.WaitForResponsesAsync(ApiEndpointType.UncollectNote, perAttemptTimeout, 1);
                if (!got)
                {
                    sw.Stop();
                    LogInteractionAudit("取消收藏", keyword, domVerified: true, apiConfirmed: false, duration: sw.Elapsed, extra: null);
                    return OperationResult<InteractionResult>.Fail("取消收藏失败：未捕获网络确认", ErrorType.NetworkError, "UNFAVORITE_API_NOT_CONFIRMED");
                }
                sw.Stop();
                LogInteractionAudit("取消收藏", keyword, domVerified: true, apiConfirmed: true, duration: sw.Elapsed, extra: null);
                await _humanizedInteraction.HumanBetweenActionsDelayAsync();
                return OperationResult<InteractionResult>.Ok(result);
            }

            var ensured = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(page);
            if (!ensured)
                return OperationResult<InteractionResult>.Fail("无法导航至探索/发现/搜索页面", ErrorType.NavigationError, "ENTRY_PAGE_NOT_AVAILABLE");
            var search = await FindVisibleMatchingNotesAsync(keyword, 1);
            if (!search.Success || search.Data is {Count: 0})
                return OperationResult<InteractionResult>.Fail($"未找到匹配关键词的笔记: {keyword}", ErrorType.ElementNotFound, "NO_MATCHING_NOTES");
            var target = search.Data!.First();
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);
            await _humanizedInteraction.HumanClickAsync(target);
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);
            return await UncollectNoteAsync(keyword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消收藏操作异常: 关键词={Keyword}", keyword);
            var errorResult = new InteractionResult(false, "取消收藏", "未知", "未知", $"取消收藏异常: {ex.Message}", "UNFAVORITE_OPERATION_EXCEPTION");
            return OperationResult<InteractionResult>.Ok(errorResult);
        }
    }
    #endregion

    #region 搜索功能 - SearchNotes
    /// <summary>
    /// 搜索笔记（API监听+拟人化操作）。
    /// 行为约定（破坏性变更）：若当前处于“笔记详情”页面，将先尝试关闭详情，确保处于可搜索的列表/搜索页上下文。
    /// </summary>
    public async Task<OperationResult<SearchResult>> SearchNotesAsync(
        string keyword,
        int maxResults = 20,
        string sortBy = "comprehensive",
        string noteType = "all",
        string publishTime = "all",
        bool includeAnalytics = true,
        bool autoExport = true,
        string? exportFileName = null)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("开始基于UniversalApiMonitor的智能搜索: 关键词={Keyword}, 最大结果={MaxResults}",
                keyword, maxResults);

            // 1. 检查登录状态
            if (!await _browserManager.IsLoggedInAsync())
            {
                return OperationResult<SearchResult>.Fail(
                    "用户未登录，无法执行搜索",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }

            // 2. 获取浏览器上下文和页面
            var page = await _browserManager.GetPageAsync();

            // 2.1 若当前在笔记详情页，先尝试退出（避免搜索输入与监听在错误上下文）
            try
            {
                var status = await GetCurrentPageStatusAsync(page);
                if (status.PageType == PageType.NoteDetail)
                {
                    _logger.LogInformation("检测到处于笔记详情页，先尝试关闭详情再执行搜索");
                    var closed = await _pageStateGuard.EnsureExitNoteDetailIfPresentAsync(page);
                    if (!closed)
                    {
                        return OperationResult<SearchResult>.Fail(
                            "无法退出当前笔记详情页，无法进行搜索",
                            ErrorType.NavigationError,
                            "EXIT_DETAIL_FAILED");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "退出详情页预处理失败，继续尝试后续流程");
            }

            // 3. 设置SearchNotes API监听器（统一架构模式）
            _browserManager.BeginOperation();
            var endpointsToMonitor = new HashSet<ApiEndpointType>
            {
                ApiEndpointType.SearchNotes
            };

            var setupSuccess = _universalApiMonitor.SetupMonitor(page, endpointsToMonitor);
            if (!setupSuccess)
            {
                return OperationResult<SearchResult>.Fail(
                    "无法设置SearchNotes API监听器",
                    ErrorType.NetworkError,
                    "SEARCH_API_MONITOR_SETUP_FAILED");
            }

            _logger.LogDebug("SearchNotes API监听器设置完成，开始执行拟人化搜索");

            // 4. 执行拟人化搜索 + 端点等待（可配置：单次等待 + 重试次数）
            var maxRetries = Math.Max(0, _endpointRetry.MaxRetries); // 重试次数（不含首次尝试）
            var perAttemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1, _endpointRetry.AttemptTimeoutMs));
            var attempt = 0;
            var searchApiReceived = false;
            while (attempt <= maxRetries)
            {
                var isLastRetry = maxRetries > 0 && attempt == maxRetries;
                // 最后一次重试前：先强制跳转到主页再执行搜索
                if (isLastRetry)
                {
                    _logger.LogInformation("最后一次重试：尝试切回发现/搜索入口上下文后再执行搜索");
                    var navOk = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(page);
                    if (!navOk)
                    {
                        _logger.LogWarning("强制跳转主页失败，继续按原路径重试");
                    }
                    await _pageLoadWaitService.WaitForPageLoadAsync(page);
                }
                // 清理上一次监听数据，避免旧数据干扰判断
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.SearchNotes);

                // 执行拟人化搜索操作
                var searchResult = await PerformHumanizedSearchAsync(page, keyword, sortBy, noteType, publishTime, assumeOnDiscover: isLastRetry);
                if (!searchResult.Success)
                {
                    return OperationResult<SearchResult>.Fail(
                        searchResult.ErrorMessage ?? "拟人化搜索失败",
                        ErrorType.BrowserError,
                        "HUMANIZED_SEARCH_FAILED");
                }

                // 等待端点命中（本次限定为2分钟）
                _logger.LogDebug("拟人化搜索完成，等待SearchNotes API响应（尝试 {Attempt}/{Total}，单次超时 {Timeout}ms）...",
                    attempt + 1, maxRetries + 1, perAttemptTimeout.TotalMilliseconds);

                searchApiReceived = await _universalApiMonitor.WaitForResponsesAsync(
                    ApiEndpointType.SearchNotes, perAttemptTimeout, 1);

                if (searchApiReceived) break;

                attempt++;
                if (attempt > maxRetries)
                {
                    _logger.LogWarning("SearchNotes 未命中端点且达到最大重试次数({MaxRetries})", maxRetries);
                    break;
                }

                _logger.LogWarning("SearchNotes 未命中端点，准备重试（第 {Attempt} 次重试）...", attempt);
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);
            }

            if (!searchApiReceived)
            {
                return OperationResult<SearchResult>.Fail(
                    "等待SearchNotes API响应超时，重试已达上限",
                    ErrorType.NetworkError,
                    "SEARCH_API_TIMEOUT_RETRY_EXCEEDED");
            }

            // 6. 获取监听到的API数据
            var searchNoteDetails = _universalApiMonitor.GetMonitoredNoteDetails(ApiEndpointType.SearchNotes);
            if (searchNoteDetails.Count == 0)
            {
                return OperationResult<SearchResult>.Fail(
                    "无法从SearchNotes API获取搜索数据",
                    ErrorType.ElementNotFound,
                    "NO_SEARCH_API_DATA");
            }

            // 7. 转换为标准格式
            var recommendedNotes = ConvertNoteDetailsToRecommendedNotes(searchNoteDetails, keyword);
            _logger.LogInformation("成功转换 {Count} 个搜索结果", recommendedNotes.Count);

            // 8. 计算统计信息
            var statistics = includeAnalytics
                ? CalculateEnhancedSearchStatistics(recommendedNotes)
                : CreateEmptyEnhancedStatistics();

            var duration = DateTime.UtcNow - startTime;

            // 9. 转换为NoteInfo格式用于导出
            var noteInfoList = ConvertRecommendedNotesToNoteInfo(recommendedNotes);

            // 10. 执行导出功能（如果启用）
            SimpleExportInfo? exportInfo = null;
            if (autoExport && noteInfoList.Count > 0)
            {
                try
                {
                    var fileName = exportFileName ??
                                   $"search_{keyword}_{DateTime.Now:yyyyMMdd_HHmmss}";

                    var exportOptions = new ExportOptions(IncludeImages: true, IncludeComments: includeAnalytics);
                    var exportResult = ExportNotesToExcel(noteInfoList, fileName, exportOptions);

                    if (exportResult.Success)
                    {
                        exportInfo = exportResult.Data;
                        var exportedPath = exportResult.Data?.FilePath ?? string.Empty;
                        _logger.LogInformation("搜索数据导出成功: {FilePath}, 记录数: {Count}",
                            exportedPath, noteInfoList.Count);
                    }
                    else
                    {
                        _logger.LogWarning("搜索数据导出失败: {Error}", exportResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "导出任务异常，不影响主功能");
                }
            }

            // 11. 构建搜索结果
            var searchResultData = new SearchResult(
                Notes: noteInfoList,
                TotalCount: recommendedNotes.Count,
                SearchKeyword: keyword,
                Duration: duration,
                Statistics: statistics,
                ExportInfo: exportInfo,
                ApiRequests: 1, // 统一架构下不再记录多个请求
                InterceptedResponses: 1,
                SearchParameters: new SearchParametersInfo(
                    Keyword: keyword,
                    SortBy: sortBy,
                    NoteType: noteType,
                    PublishTime: publishTime,
                    MaxResults: maxResults,
                    RequestedAt: startTime
                )
            );

            _logger.LogInformation(
                "UniversalApiMonitor搜索完成: 关键词={Keyword}, 结果={Count}条, 耗时={Duration}ms",
                keyword, recommendedNotes.Count, duration.TotalMilliseconds);

            return OperationResult<SearchResult>.Ok(searchResultData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UniversalApiMonitor搜索失败: 关键词={Keyword}", keyword);
            return OperationResult<SearchResult>.Fail(
                $"搜索失败: {ex.Message}",
                ErrorType.NetworkError,
                "SEARCH_EXCEPTION");
        }
        finally
        {
            // 清理API监听器
            try
            {
                await _universalApiMonitor.StopMonitoringAsync();
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.SearchNotes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理SearchNotes API监听器失败");
            }
            _browserManager.EndOperation();
        }
    }

    /// <summary>
    /// 将RecommendedNote列表转换为NoteInfo列表
    /// 用于兼容现有的导出和处理功能
    /// </summary>
    private List<NoteInfo> ConvertRecommendedNotesToNoteInfo(List<RecommendedNote> recommendedNotes)
    {
        var noteInfoList = new List<NoteInfo>();

        foreach (var recommendedNote in recommendedNotes)
        {
            try
            {
                var noteInfo = new NoteInfo
                {
                    Id = recommendedNote.Id,
                    Title = recommendedNote.Title,
                    Author = recommendedNote.Author,
                    AuthorId = recommendedNote.AuthorId,
                    AuthorAvatar = recommendedNote.AuthorAvatar,
                    Url = recommendedNote.Url,
                    CoverImage = recommendedNote.CoverImage,
                    LikeCount = recommendedNote.LikeCount,
                    CommentCount = recommendedNote.CommentCount,
                    FavoriteCount = recommendedNote.FavoriteCount,
                    ShareCount = recommendedNote.ShareCount,
                    Quality = recommendedNote.Quality,
                    MissingFields = recommendedNote.MissingFields,
                    Type = recommendedNote.Type,
                    ExtractedAt = recommendedNote.ExtractedAt,
                    Description = recommendedNote.Description,
                    VideoUrl = recommendedNote.VideoUrl,
                    VideoDuration = recommendedNote.VideoDuration,
                    IsLiked = recommendedNote.IsLiked,
                    IsCollected = recommendedNote.IsCollected,
                    TrackId = recommendedNote.TrackId,
                    XsecToken = recommendedNote.XsecToken,
                    PageToken = recommendedNote.PageToken,
                    SearchId = recommendedNote.SearchId,
                    CoverInfo = recommendedNote.CoverInfo,
                    InteractInfo = recommendedNote.InteractInfo,
                    UserInfo = recommendedNote.UserInfo,
                    Images = recommendedNote.Images.Select(img => img.Url).Where(u => !string.IsNullOrEmpty(u)).ToList()
                };

                noteInfoList.Add(noteInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "转换推荐笔记到NoteInfo失败: ID={Id}", recommendedNote.Id);
            }
        }

        return noteInfoList;
    }

    /// <summary>
    /// 将NoteDetail列表转换为RecommendedNote列表
    /// </summary>
    private List<RecommendedNote> ConvertNoteDetailsToRecommendedNotes(List<NoteDetail> noteDetails, string searchKeyword)
    {
        return noteDetails.Select(noteDetail =>
        {
            var rn = new RecommendedNote
            {
                Id = noteDetail.Id,
                Title = noteDetail.Title,
                Author = noteDetail.Author,
                AuthorId = noteDetail.AuthorId,
                AuthorAvatar = noteDetail.AuthorAvatar,
                CoverImage = noteDetail.CoverImage,
                Url = noteDetail.Url,
                Type = noteDetail.Type,
                LikeCount = noteDetail.LikeCount,
                CommentCount = noteDetail.CommentCount,
                FavoriteCount = noteDetail.FavoriteCount,
                ShareCount = noteDetail.ShareCount,
                ExtractedAt = noteDetail.ExtractedAt,
                Quality = noteDetail.Quality,
                MissingFields = noteDetail.MissingFields ?? [],
                Description = string.IsNullOrEmpty(noteDetail.Description) ? noteDetail.Content : noteDetail.Description,
                VideoUrl = noteDetail.VideoUrl,
                VideoDuration = noteDetail.VideoDuration,
                IsLiked = noteDetail.IsLiked,
                IsCollected = noteDetail.IsCollected,
                TrackId = noteDetail.TrackId,
                XsecToken = noteDetail.XsecToken,
                Images = noteDetail.Images.Select(u => new RecommendedImageInfo {Url = u}).ToList()
            };
            return rn;
        }).ToList();
    }

    /// <summary>
    /// 执行拟人化搜索操作
    /// 导航到搜索页面，输入关键词，设置筛选条件，模拟真实用户行为
    /// </summary>
    private async Task<OperationResult<bool>> PerformHumanizedSearchAsync(
        IPage page,
        string keyword,
        string sortBy,
        string noteType,
        string publishTime,
        bool assumeOnDiscover = false)
    {
        try
        {
            _logger.LogDebug("开始执行拟人化搜索操作");
            if (!assumeOnDiscover)
            {
                try
                {
                    // 1. 导航到发现页面
                    _browserManager.BeginOperation();
                    var navigationResult = await NavigateToDiscoverPageAsync(page, TimeSpan.FromSeconds(30));
                    if (!navigationResult.Success)
                    {
                        _logger.LogError("导航到发现页面失败：{Error}", navigationResult.ErrorMessage);
                        return OperationResult<bool>.Fail(
                            $"导航到发现页面失败：{navigationResult.ErrorMessage}",
                            ErrorType.NavigationError,
                            "NAVIGATION_FAILED");
                    }
                }
                catch (PlaywrightException)
                {
                    // 页面/上下文可能被重连打断，尝试一次自愈：重新获取页面并重试导航
                    _logger.LogWarning("页面在导航时已关闭，尝试重新获取页面并重试导航");
                    var freshPage = await _browserManager.GetPageAsync();
                    page = freshPage;
                    var navigationResult = await NavigateToDiscoverPageAsync(page, TimeSpan.FromSeconds(30));
                    if (!navigationResult.Success)
                    {
                        _logger.LogError("导航到发现页面失败：{Error}", navigationResult.ErrorMessage);
                        return OperationResult<bool>.Fail(
                            $"导航到发现页面失败：{navigationResult.ErrorMessage}",
                            ErrorType.NavigationError,
                            "NAVIGATION_FAILED");
                    }
                }
                finally
                {
                    _browserManager.EndOperation();
                }
            }
            else
            {
                _logger.LogDebug("已在主页/发现页环境，跳过导航步骤");
            }

            // 2. 等待页面加载（使用统一等待服务，具备降级与重试能力）
            await _pageLoadWaitService.WaitForPageLoadAsync(page);

            // 3. 模拟用户思考过程
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);

            // 4. 查找搜索输入框（使用统一的别名选择器，容错更强）
            var searchInput = await _humanizedInteraction.FindElementAsync(page, "SearchInput", retries: 3, timeout: _timeouts.UiWaitMs);
            if (searchInput != null)
            {
                _logger.LogDebug("搜索输入框定位成功：使用别名选择器SearchInput");
            }
            if (searchInput == null)
            {
                // 兜底：再尝试几组直接选择器（包含用户提供DOM）
                var fallbacks = new[]
                {
                    "#search-input",
                    ".search-input",
                    "input[placeholder='搜索小红书']",
                    ".input-box input",
                    "input[placeholder*='搜索']",
                };
                foreach (var sel in fallbacks)
                {
                    try
                    {
                        await page.WaitForSelectorAsync(sel, new() {Timeout = _timeouts.UiWaitMs});
                        searchInput = await page.QuerySelectorAsync(sel);
                        if (searchInput != null)
                        {
                            _logger.LogDebug("搜索输入框定位成功：fallback选择器 {Selector}", sel);
                            break;
                        }
                    }
                    catch
                    {
                        /* ignore and try next */
                    }
                }
            }

            if (searchInput == null)
            {
                return OperationResult<bool>.Fail(
                    "未找到搜索输入框",
                    ErrorType.ElementNotFound,
                    "SEARCH_INPUT_NOT_FOUND");
            }

            _logger.LogDebug(searchInput.ToString());
            // 5. 清空输入框并输入关键词（尽量保持焦点在输入框上）
            await _humanizedInteraction.HumanClickAsync(searchInput);
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);

            // 尝试快捷键全选删除 + 兜底Fill清空，保证触发输入事件
            try { await page.Keyboard.PressAsync("Control+A"); }
            catch { }
            try { await page.Keyboard.PressAsync("Meta+A"); }
            catch { }
            try { await page.Keyboard.PressAsync("Backspace"); }
            catch { }
            await searchInput.FillAsync("");
            await Task.Delay(Random.Shared.Next(300, 800));

            // 模拟打字（非过时API）：聚焦后用键盘输入
            await searchInput.FocusAsync();
            await page.Keyboard.TypeAsync(keyword, new KeyboardTypeOptions
            {
                Delay = Random.Shared.Next(80, 160)
            });

            // 6. 提交搜索：优先回车，失败则回退点击按钮
            bool submitted = false;
            try
            {
                await searchInput.PressAsync("Enter");
                submitted = true;
                _logger.LogDebug("搜索提交：通过Enter触发");
            }
            catch { }

            // 等待结果页迹象（URL或布局容器）
            bool navigated = false;
            if (submitted)
            {
                try
                {
                    await page.WaitForURLAsync("**/search_result**", new() {Timeout = _timeouts.UiWaitMs});
                    navigated = true;
                    _logger.LogDebug("检测到搜索结果页URL变化");
                }
                catch { }
                if (!navigated)
                {
                    try
                    {
                        await page.WaitForSelectorAsync(".search-layout", new() {Timeout = _timeouts.UiWaitMs});
                        navigated = true;
                        _logger.LogDebug("检测到搜索结果页布局容器");
                    }
                    catch { }
                }
            }

            if (!navigated)
            {
                // 回退点击搜索按钮
                var searchBtn = await _humanizedInteraction.FindElementAsync(page, "SearchButton", retries: 2, timeout: 3000);
                if (searchBtn != null)
                {
                    await _humanizedInteraction.HumanClickAsync(searchBtn);
                    _logger.LogDebug("搜索提交：通过点击SearchButton触发");
                    // 再次等待结果页迹象
                    try
                    {
                        await page.WaitForURLAsync("**/search_result**", new() {Timeout = _timeouts.UiWaitMs});
                        navigated = true;
                        _logger.LogDebug("检测到搜索结果页URL变化");
                    }
                    catch { }
                    if (!navigated)
                    {
                        try
                        {
                            await page.WaitForSelectorAsync(".search-layout", new() {Timeout = _timeouts.UiWaitMs});
                            navigated = true;
                            _logger.LogDebug("检测到搜索结果页布局容器");
                        }
                        catch { }
                    }
                }
            }

            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);

            // 7. 应用筛选条件（如果不是默认值）
            if (sortBy != "comprehensive")
            {
                await ApplySearchFilterAsync(page, "排序", GetSortDisplayName(sortBy));
            }

            if (noteType != "all")
            {
                await ApplySearchFilterAsync(page, "类型", GetNoteTypeDisplayName(noteType));
            }

            if (publishTime != "all")
            {
                await ApplySearchFilterAsync(page, "时间", GetPublishTimeDisplayName(publishTime));
            }

            // 8. 等待搜索结果加载
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.NetworkResponse);

            _logger.LogDebug("拟人化搜索操作完成");
            return OperationResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "拟人化搜索操作失败");
            return OperationResult<bool>.Fail(
                $"搜索操作失败: {ex.Message}",
                ErrorType.BrowserError,
                "HUMANIZED_SEARCH_ERROR");
        }
    }

    /// <summary>
    /// 应用搜索筛选条件
    /// 模拟用户点击筛选选项的行为
    /// </summary>
    private async Task ApplySearchFilterAsync(IPage page, string filterType, string filterValue)
    {
        try
        {
            _logger.LogDebug("应用搜索筛选: {FilterType} = {FilterValue}", filterType, filterValue);

            // 等待筛选面板加载
            await Task.Delay(Random.Shared.Next(800, 1500));

            // 查找筛选按钮（基于常见的筛选面板结构）
            var filterSelectors = new[]
            {
                $"[data-filter='{filterType}']",
                $".filter-{filterType.ToLower()}",
                $".filter-item:has-text('{filterType}')",
                $"button:has-text('{filterType}')"
            };

            foreach (var selector in filterSelectors)
            {
                try
                {
                    // 等待筛选按钮出现并可见，最长来自配置（统一 UI 等待）
                    try { await page.WaitForSelectorAsync(selector, new() {Timeout = _timeouts.UiWaitMs, State = WaitForSelectorState.Visible}); }
                    catch { }
                    var filterButton = await page.QuerySelectorAsync(selector);
                    if (filterButton != null)
                    {
                        await _humanizedInteraction.HumanClickAsync(filterButton);
                        await Task.Delay(Random.Shared.Next(500, 1000));

                        // 可选：等待筛选面板出现（使用别名以提升鲁棒性）
                        try { await _humanizedInteraction.FindElementAsync(page, "FilterPanel", retries: 2, timeout: _timeouts.UiWaitMs); }
                        catch { }

                        // 查找具体的筛选值
                        var valueSelector = $"[data-value='{filterValue}'], button:has-text('{filterValue}')";
                        // 等待筛选值出现并可见，最长来自配置（统一 UI 等待）
                        try { await page.WaitForSelectorAsync(valueSelector, new() {Timeout = _timeouts.UiWaitMs, State = WaitForSelectorState.Visible}); }
                        catch { }
                        var valueButton = await page.QuerySelectorAsync(valueSelector);
                        if (valueButton != null)
                        {
                            await _humanizedInteraction.HumanClickAsync(valueButton);
                            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.NetworkResponse);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("筛选选择器 {Selector} 失败: {Error}", selector, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "应用搜索筛选失败: {FilterType} = {FilterValue}", filterType, filterValue);
            // 筛选失败不阻止主流程
        }
    }

    /// <summary>
    /// 计算增强搜索统计数据
    /// 基于转换后的推荐笔记计算详细统计
    /// </summary>
    private SearchStatistics CalculateEnhancedSearchStatistics(List<RecommendedNote> notes)
    {
        if (notes.Count == 0)
        {
            return CreateEmptyEnhancedStatistics();
        }

        // 数据质量统计
        var qualityDistribution = notes
            .GroupBy(n => n.Quality)
            .ToDictionary(g => g.Key, g => g.Count());

        var completeCount = qualityDistribution.GetValueOrDefault(DataQuality.Complete, 0);
        var partialCount = qualityDistribution.GetValueOrDefault(DataQuality.Partial, 0);
        var minimalCount = qualityDistribution.GetValueOrDefault(DataQuality.Minimal, 0);

        // 类型分布统计
        var typeDistribution = notes
            .GroupBy(n => n.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        var videoCount = typeDistribution.GetValueOrDefault(NoteType.Video, 0);
        var imageCount = typeDistribution.GetValueOrDefault(NoteType.Image, 0);

        // 互动数据统计
        var likeCounts = notes.Where(n => n.LikeCount.HasValue).Select(n => n.LikeCount!.Value).ToList();
        var commentCounts = notes.Where(n => n.CommentCount.HasValue).Select(n => n.CommentCount!.Value).ToList();
        var collectCounts = notes.Where(n => n.FavoriteCount.HasValue).Select(n => n.FavoriteCount!.Value).ToList();

        var avgLikes = likeCounts.Count != 0 ? likeCounts.Average() : 0;
        var avgComments = commentCounts.Count != 0 ? commentCounts.Average() : 0;
        var avgCollects = collectCounts.Count != 0 ? collectCounts.Average() : 0;

        // 作者分布统计
        var authorDistribution = notes
            .Where(n => !string.IsNullOrEmpty(n.Author))
            .GroupBy(n => n.Author)
            .ToDictionary(g => g.Key, g => g.Count());

        return new SearchStatistics(
            CompleteDataCount: completeCount,
            PartialDataCount: partialCount,
            MinimalDataCount: minimalCount,
            AverageLikes: avgLikes,
            AverageComments: avgComments,
            CalculatedAt: DateTime.UtcNow,
            VideoNotesCount: videoCount,
            ImageNotesCount: imageCount,
            AverageCollects: avgCollects,
            AuthorDistribution: authorDistribution,
            TypeDistribution: typeDistribution,
            DataQualityDistribution: qualityDistribution
        );
    }
    /// <summary>
    /// 创建空的增强统计数据
    /// </summary>
    private SearchStatistics CreateEmptyEnhancedStatistics()
    {
        return new SearchStatistics(
            CompleteDataCount: 0,
            PartialDataCount: 0,
            MinimalDataCount: 0,
            AverageLikes: 0,
            AverageComments: 0,
            CalculatedAt: DateTime.UtcNow,
            VideoNotesCount: 0,
            ImageNotesCount: 0,
            AverageCollects: 0,
            AuthorDistribution: new Dictionary<string, int>(),
            TypeDistribution: new Dictionary<NoteType, int>(),
            DataQualityDistribution: new Dictionary<DataQuality, int>()
        );
    }

    /// <summary>
    /// 导出笔记数据到Excel文件 - 内置导出功能
    /// </summary>
    private OperationResult<SimpleExportInfo> ExportNotesToExcel(List<NoteInfo> notes, string fileName, ExportOptions? options = null)
    {
        try
        {
            if (notes.Count == 0)
            {
                return OperationResult<SimpleExportInfo>.Fail("没有数据可导出", ErrorType.ValidationError, "NO_DATA_TO_EXPORT");
            }

            options ??= new ExportOptions();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeFileName = Path.GetFileNameWithoutExtension(fileName);
            var fullFileName = $"{safeFileName}_{timestamp}.xlsx";
            var exportsDir = Path.Combine(AppContext.BaseDirectory, "exports");
            var filePath = Path.Combine(exportsDir, fullFileName);

            Directory.CreateDirectory(exportsDir);

            var exportSuccess = ExportNotesToExcelInternal(notes, filePath, options);

            if (exportSuccess && File.Exists(filePath))
            {
                var exportInfo = new SimpleExportInfo(
                    FilePath: filePath,
                    FileName: fullFileName,
                    ExportedAt: DateTime.UtcNow,
                    Success: true
                );

                _logger.LogInformation("数据导出成功: {FilePath}, 记录数: {Count}",
                    filePath, notes.Count);

                return OperationResult<SimpleExportInfo>.Ok(exportInfo);
            }
            return OperationResult<SimpleExportInfo>.Fail("导出文件创建失败", ErrorType.FileOperation, "EXPORT_FILE_FAILED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据导出异常");

            return OperationResult<SimpleExportInfo>.Fail($"导出异常: {ex.Message}", ErrorType.FileOperation, "EXPORT_EXCEPTION");
        }
    }

    /// <summary>
    /// 导出笔记到Excel文件的内部实现
    /// </summary>
    private bool ExportNotesToExcelInternal(List<NoteInfo> notes, string filePath, ExportOptions options)
    {
        try
        {
            using var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("小红书笔记数据");

            // 创建标题行
            var headerRow = sheet.CreateRow(0);
            var headers = new[] {"标题", "作者", "链接", "点赞数", "评论数", "发布时间", "数据质量"};

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(headers[i]);
            }

            // 填充数据行
            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                var row = sheet.CreateRow(i + 1);

                row.CreateCell(0).SetCellValue(note.Title);
                row.CreateCell(1).SetCellValue(note.Author);
                row.CreateCell(2).SetCellValue(note.Url);
                row.CreateCell(3).SetCellValue(note.LikeCount?.ToString() ?? "N/A");
                row.CreateCell(4).SetCellValue(note.CommentCount?.ToString() ?? "N/A");
                row.CreateCell(5).SetCellValue(note.PublishTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A");
                row.CreateCell(6).SetCellValue(note.Quality.ToString());
            }

            // 自动调整列宽
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.AutoSizeColumn(i);
            }

            // 保存文件
            using var fileStream = File.Create(filePath);
            workbook.Write(fileStream);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel导出失败: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// 获取排序方式的显示名称
    /// </summary>
    private string GetSortDisplayName(string sortBy)
    {
        return sortBy switch
        {
            "comprehensive" => "综合",
            "latest" => "最新",
            "most_liked" => "最多点赞",
            "most_commented" => "最多评论",
            "most_favorited" => "最多收藏",
            _ => "综合"
        };
    }

    /// <summary>
    /// 获取笔记类型的显示名称
    /// </summary>
    private string GetNoteTypeDisplayName(string noteType)
    {
        return noteType switch
        {
            "all" => "不限",
            "video" => "视频",
            "image" => "图文",
            _ => "不限"
        };
    }

    /// <summary>
    /// 获取发布时间的显示名称
    /// </summary>
    private string GetPublishTimeDisplayName(string publishTime)
    {
        return publishTime switch
        {
            "all" => "不限",
            "day" => "一天内",
            "week" => "一周内",
            "half_year" => "半年内",
            _ => "不限"
        };
    }
    #endregion

    #region 推荐服务功能 (原 RecommendService)
    /// <summary>
    /// 获取推荐笔记，确保API被正确触发
    /// 合并自 RecommendService.GetRecommendedNotesAsync
    /// 行为约定（破坏性变更）：若当前处于“笔记详情”页面，将先尝试关闭详情并回到列表/发现页，再继续流程。
    /// </summary>
    /// <param name="limit">获取数量限制</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>推荐结果</returns>
    public async Task<OperationResult<RecommendListResult>> GetRecommendedNotesAsync(int limit = 20, TimeSpan? timeout = null)
    {
        // 统一 MCP 等待超时（默认 10 分钟）；若未指定则取 McpSettings.WaitTimeoutMs
        var cfgMs = _mcpSettings.WaitTimeoutMs;
        timeout ??= TimeSpan.FromMilliseconds(cfgMs > 0 ? cfgMs : 600_000);
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("开始获取推荐笔记，数量：{Limit}，超时：{Timeout}ms", limit, timeout.Value.TotalMilliseconds);

            // 检查登录状态
            if (!await _accountManager.IsLoggedInAsync())
            {
                return OperationResult<RecommendListResult>.Fail(
                    "用户未登录，请先登录",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }

            var page = await _browserManager.GetPageAsync();

            // 若当前在笔记详情页，则先尝试关闭（避免监听器在详情页上下文下无触发）
            try
            {
                var status = await GetCurrentPageStatusAsync(page);
                if (status.PageType == PageType.NoteDetail)
                {
                    _logger.LogInformation("检测到处于笔记详情页，先尝试关闭详情");
                    var closed = await _pageStateGuard.EnsureExitNoteDetailIfPresentAsync(page);
                    if (!closed)
                    {
                        return OperationResult<RecommendListResult>.Fail(
                            "无法退出当前笔记详情页",
                            ErrorType.NavigationError,
                            "EXIT_DETAIL_FAILED");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "退出详情页预处理失败，继续尝试后续流程");
            }

            // 设置Homefeed API监听器
            _browserManager.BeginOperation();
            var endpointsToMonitor = new HashSet<ApiEndpointType>
            {
                ApiEndpointType.Homefeed
            };

            var setupSuccess = _universalApiMonitor.SetupMonitor(page, endpointsToMonitor);
            if (!setupSuccess)
            {
                return OperationResult<RecommendListResult>.Fail(
                    "无法设置Homefeed API监听器",
                    ErrorType.NetworkError,
                    "HOMEFEED_API_MONITOR_SETUP_FAILED");
            }

            _logger.LogDebug("Homefeed API监听器设置完成，开始导航到发现页面");

            // 导航 + 等待端点（可配置：单次等待 + 重试次数）
            var maxRetries = Math.Max(0, _endpointRetry.MaxRetries); // 重试次数（不含首次尝试）
            var perAttemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1, _endpointRetry.AttemptTimeoutMs));
            var attempt = 0;
            var waitOk = false;
            DiscoverNavigationResult navigationResult = new() {Success = false};
            while (attempt <= maxRetries)
            {
                var isLastRetry = maxRetries > 0 && attempt == maxRetries;
                // 每轮先清空监听数据，保证捕捉当轮请求
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.Homefeed);

                if (isLastRetry)
                {
                    _logger.LogInformation("最后一次重试：尝试切回发现/搜索入口上下文后再等待 Homefeed");
                    var navOk = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(page);
                    if (!navOk)
                    {
                        _logger.LogWarning("强制跳转主页失败，继续按原路径重试");
                    }
                    await _pageLoadWaitService.WaitForPageLoadAsync(page);
                }
                else
                {
                    navigationResult = await NavigateToDiscoverPageAsync(page, TimeSpan.FromSeconds(30));
                    if (!navigationResult.Success)
                    {
                        _logger.LogError("导航到发现页面失败：{Error}", navigationResult.ErrorMessage);
                        return OperationResult<RecommendListResult>.Fail(
                            $"导航到发现页面失败：{navigationResult.ErrorMessage}",
                            ErrorType.NavigationError,
                            "NAVIGATION_FAILED");
                    }
                }

                waitOk = await _universalApiMonitor.WaitForResponsesAsync(ApiEndpointType.Homefeed, perAttemptTimeout, 1);
                if (waitOk) break;

                attempt++;
                if (attempt > maxRetries)
                {
                    _logger.LogWarning("Homefeed 未命中端点且达到最大重试次数({MaxRetries})", maxRetries);
                    break;
                }

                _logger.LogWarning("Homefeed 未命中端点，准备重试（第 {Attempt} 次重试）...", attempt);
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);
            }

            if (!waitOk)
            {
                return OperationResult<RecommendListResult>.Fail(
                    "等待Homefeed API响应超时，重试已达上限",
                    ErrorType.NetworkError,
                    "HOMEFEED_API_TIMEOUT_RETRY_EXCEEDED");
            }

            // 使用监听到的 API 数据
            var homefeedNoteDetails = _universalApiMonitor.GetMonitoredNoteDetails(ApiEndpointType.Homefeed);
            _logger.LogDebug("从Homefeed API获取到 {ApiNoteCount} 条笔记详情", homefeedNoteDetails.Count);

            if (homefeedNoteDetails.Count == 0)
            {
                return OperationResult<RecommendListResult>.Fail(
                    "无法从Homefeed API获取推荐数据",
                    ErrorType.NetworkError,
                    "HOMEFEED_API_NO_DATA");
            }

            var noteInfos = homefeedNoteDetails
                .Select(ConvertNoteDetailToNoteInfo)
                .Take(limit)
                .ToList();

            var duration = DateTime.UtcNow - startTime;
            var rawResponses = _universalApiMonitor.GetRawResponses(ApiEndpointType.Homefeed);
            var perf = new CollectionPerformanceMetrics(
                successfulRequests: rawResponses.Count,
                failedRequests: 0,
                scrollCount: 0,
                duration: duration);

            var collectionResult = SmartCollectionResult.CreateSuccess(
                noteInfos,
                limit,
                perf.RequestCount,
                duration,
                perf);

            // 6. 转换为推荐结果格式（API-only）
            var recommendResult = ConvertToRecommendResult(collectionResult, navigationResult);

            _logger.LogInformation("推荐获取完成（API-only），收集{Count}/{Target}条笔记，耗时{Duration}ms",
                recommendResult.Notes.Count, limit, duration.TotalMilliseconds);

            return OperationResult<RecommendListResult>.Ok(recommendResult);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "获取推荐笔记时发生异常，耗时{Duration}ms", duration.TotalMilliseconds);
            return OperationResult<RecommendListResult>.Fail(
                $"获取推荐失败：{ex.Message}",
                ErrorType.Unknown,
                "GET_RECOMMENDATIONS_EXCEPTION");
        }
        finally
        {
            // 清理API监听器
            try
            {
                await _universalApiMonitor.StopMonitoringAsync();
                _universalApiMonitor.ClearMonitoredData(ApiEndpointType.Homefeed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理Homefeed API监听器失败");
            }
            _browserManager.EndOperation();
        }
    }

    /// <summary>
    /// 将NoteDetail转换为NoteInfo - 修复后版本
    /// </summary>
    /// <param name="noteDetail">笔记详情</param>
    /// <returns>笔记信息</returns>
    private NoteInfo ConvertNoteDetailToNoteInfo(NoteDetail noteDetail)
    {
        return new NoteInfo
        {
            Id = noteDetail.Id,
            Title = noteDetail.Title,
            Author = noteDetail.Author,
            AuthorId = noteDetail.AuthorId,
            AuthorAvatar = noteDetail.AuthorAvatar,
            CoverImage = noteDetail.CoverImage,
            LikeCount = noteDetail.LikeCount,
            CommentCount = noteDetail.CommentCount,
            FavoriteCount = noteDetail.FavoriteCount,
            PublishTime = noteDetail.PublishTime,
            Url = noteDetail.Url,
            Content = noteDetail.Content,
            Type = noteDetail.Type,
            ExtractedAt = noteDetail.ExtractedAt,
            Quality = noteDetail.Quality,
            MissingFields = noteDetail.MissingFields
        };
    }

    /// <summary>
    /// 转换收集结果为推荐结果格式
    /// 合并自 RecommendService.ConvertToRecommendResult
    /// </summary>
    private RecommendListResult ConvertToRecommendResult(
        SmartCollectionResult collectionResult,
        DiscoverNavigationResult navigationResult)
    {
        // 直接使用 NoteInfo，不需要转换为 RecommendNote
        var notes = collectionResult.CollectedNotes;

        _logger.LogDebug("开始转换收集结果，笔记总数：{Count}", notes.Count);

        // 创建统计数据，使用安全的平均值计算
        var notesWithLikes = notes.Where(n => n.LikeCount is > 0).ToList();
        var notesWithComments = notes.Where(n => n.CommentCount is >= 0).ToList();
        var notesWithCollects = notes.Where(n => n.FavoriteCount is >= 0).ToList();

        _logger.LogDebug("统计数据过滤结果 - 有点赞数据：{LikeCount}，有评论数据：{CommentCount}，有收藏数据：{CollectCount}",
            notesWithLikes.Count, notesWithComments.Count, notesWithCollects.Count);

        // 安全计算平均值，避免空序列异常
        var avgLikes = notesWithLikes.Count > 0 ? notesWithLikes.Average(n => n.LikeCount ?? 0) : 0;
        var avgComments = notesWithComments.Count > 0 ? notesWithComments.Average(n => n.CommentCount ?? 0) : 0;
        var avgCollects = notesWithCollects.Count > 0 ? notesWithCollects.Average(n => n.FavoriteCount ?? 0) : 0;

        var statistics = new RecommendStatistics(
            VideoNotesCount: notes.Count(n => n.Type == NoteType.Video),
            ImageNotesCount: notes.Count(n => n.Type == NoteType.Image),
            AverageLikes: avgLikes,
            AverageComments: avgComments,
            AverageCollects: avgCollects,
            TopCategories: new Dictionary<string, int>(),
            AuthorDistribution: notes.Count != 0 ?
                notes.Where(n => !string.IsNullOrEmpty(n.Author))
                    .GroupBy(n => n.Author)
                    .ToDictionary(g => g.Key, g => g.Count()) :
                new Dictionary<string, int>(),
            CalculatedAt: DateTime.UtcNow
        );

        _logger.LogDebug("统计数据计算完成 - 平均点赞：{AvgLikes:F1}，平均评论：{AvgComments:F1}，平均收藏：{AvgCollects:F1}",
            avgLikes, avgComments, avgCollects);

        // 创建收集详情
        var collectionDetails = new RecommendCollectionDetails(
            InterceptedRequests: collectionResult.RequestCount,
            SuccessfulRequests: collectionResult.PerformanceMetrics.SuccessfulRequests,
            FailedRequests: collectionResult.PerformanceMetrics.FailedRequests,
            ScrollOperations: collectionResult.PerformanceMetrics.ScrollCount,
            AverageScrollDelay: 1500, // 默认滚动延时
            DataQuality: DataQuality.Complete,
            CollectionMode: RecommendCollectionMode.Standard
        );

        // 使用 record 构造函数创建实例
        return new RecommendListResult(
            Notes: notes,
            TotalCollected: notes.Count,
            RequestCount: collectionResult.RequestCount,
            Duration: collectionResult.Duration,
            Statistics: statistics,
            ExportInfo: null,
            CollectionDetails: collectionDetails
        );
    }
    #endregion

    #region 发现页面导航功能 (原 DiscoverPageNavigationService)
    /// <summary>
    /// 导航到发现页面并确保API被正确触发
    /// 合并自 DiscoverPageNavigationService.NavigateToDiscoverPageAsync
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>导航结果</returns>
    public async Task<DiscoverNavigationResult> NavigateToDiscoverPageAsync(IPage page, TimeSpan? timeout = null)
    {
        var startTime = DateTime.UtcNow;
        timeout ??= TimeSpan.FromSeconds(30);

        var result = new DiscoverNavigationResult
        {
            NavigationLog = []
        };

        try
        {
            result.NavigationLog.Add($"开始导航到发现页面（PageStateGuard），超时时间：{timeout.Value.TotalSeconds}秒");

            var ensured = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(page);
            if (ensured)
            {
                result.Success = true;
                // 无法从 Guard 判定点击还是直达，为保持兼容性，标记为 DirectUrl（语义：经导航成功到达入口页）
                result.Method = DiscoverNavigationMethod.DirectUrl;
                result.FinalUrl = page.Url;
                result.NavigationLog.Add("通过 PageStateGuard 确保入口上下文成功");
                _logger.LogInformation("已通过 PageStateGuard 成功到达发现/搜索入口页");
                return result;
            }

            // 失败路径
            result.Success = false;
            result.Method = DiscoverNavigationMethod.Failed;
            result.ErrorMessage = "PageStateGuard 未能确保入口上下文";
            result.NavigationLog.Add("PageStateGuard 未能确保入口上下文");
            _logger.LogError("导航到发现页面失败（PageStateGuard失败）");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Method = DiscoverNavigationMethod.Failed;
            result.ErrorMessage = ex.Message;
            result.NavigationLog.Add($"导航过程中发生异常：{ex.Message}");

            _logger.LogError(ex, "导航到发现页面时发生异常");
        }
        finally
        {
            result.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// 获取当前页面状态
    /// 支持多种页面类型的检测和状态分析
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="expectedPageType">期望的页面类型（可选，用于优化检测）</param>
    /// <returns>通用页面状态信息</returns>
    public async Task<PageStatusInfo> GetCurrentPageStatusAsync(IPage page, PageType? expectedPageType = null)
    {
        var status = new PageStatusInfo
        {
            CurrentUrl = page.Url,
            DetectedAt = DateTime.UtcNow
        };

        status.AddDetectionLog($"开始页面状态检测，当前URL: {status.CurrentUrl}");
        if (expectedPageType.HasValue)
        {
            status.AddDetectionLog($"期望页面类型: {expectedPageType.Value}");
        }

        try
        {
            // 1. 基于URL的页面类型识别
            status.PageType = DeterminePageTypeFromUrl(status.CurrentUrl);
            status.AddDetectionLog($"URL识别结果: {status.PageType}");

            // 2. 如果有期望类型且与URL识别结果不匹配，进行更详细的检测
            if (expectedPageType.HasValue && expectedPageType.Value != status.PageType)
            {
                status.AddDetectionLog($"URL识别与期望类型不匹配，进行详细检测");
                status.PageType = await DetectPageTypeFromDom(page, expectedPageType.Value);
            }

            // 3. 传统页面状态检测（兼容性）
            status.PageState = await _domElementManager.DetectPageStateAsync(page);
            status.AddDetectionLog($"传统页面状态: {status.PageState}");

            // 4. 页面类型特定的元素检测
            await DetectPageSpecificElements(page, status);

            // 5. API特征检测
            await DetectApiFeatures(page, status);

            // 6. 确定页面就绪状态
            status.IsPageReady = DeterminePageReadiness(status);

            status.AddDetectionLog($"最终页面类型: {status.PageType}, 就绪状态: {status.IsPageReady}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取页面状态时发生异常");
            status.AddDetectionLog($"检测异常: {ex.Message}");
        }

        return status;
    }

    // 兼容方法 GetDiscoverPageStatusAsync 已删除，请使用 GetCurrentPageStatusAsync(page, PageType.Recommend)

    /// <summary>
    /// 基于URL确定页面类型
    /// </summary>
    /// <param name="url">页面URL</param>
    /// <returns>页面类型</returns>
    private PageType DeterminePageTypeFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return PageType.Unknown;

        var uri = new Uri(url);
        var path = uri.AbsolutePath.ToLowerInvariant();
        var query = uri.Query.ToLowerInvariant();

        // 发现页面检测
        if (path.Contains("/explore?") && query.Contains("channel_id=homefeed_recommend"))
        {
            return PageType.Recommend;
        }

        // 搜索页面检测
        if (path.Contains("/search_result") || path.Contains("/search"))
        {
            return PageType.Search;
        }

        // 个人页面检测
        if (path.Contains("/user/profile") || path.StartsWith("/user/"))
        {
            return PageType.Profile;
        }

        // 笔记详情页面检测
        if (path.Contains("/explore/") && path.Length > 10)
        {
            return PageType.NoteDetail;
        }

        // 探索页面检测
        if (path.Contains("/explore"))
        {
            return PageType.Home;
        }

        // 首页检测
        if (path is "/" or "")
        {
            return PageType.Home;
        }

        return PageType.Unknown;
    }

    /// <summary>
    /// 基于DOM元素检测页面类型
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="expectedType">期望的页面类型</param>
    /// <returns>页面类型</returns>
    private async Task<PageType> DetectPageTypeFromDom(IPage page, PageType expectedType)
    {
        try
        {
            switch (expectedType)
            {
                case PageType.Recommend:
                    var discoverElements = await CountElements(page, [
                        "#exploreFeeds",
                        "[data-testid='explore-page']",
                        ".channel-container",
                        ".note-item"
                    ]);
                    return discoverElements > 0 ? PageType.Recommend : PageType.Unknown;

                case PageType.Search:
                    var searchElements = await CountElements(page, [
                        "[data-testid='search-result']",
                        ".search-container",
                        ".search-result-item"
                    ]);
                    return searchElements > 0 ? PageType.Search : PageType.Unknown;

                case PageType.Profile:
                    var profileElements = await CountElements(page, [
                        ".user-profile",
                        ".profile-info",
                        ".user-stats"
                    ]);
                    return profileElements > 0 ? PageType.Profile : PageType.Unknown;

                case PageType.NoteDetail:
                    var noteElements = await CountElements(page, [
                        ".note-detail",
                        ".note-content",
                        ".note-interaction"
                    ]);
                    return noteElements > 0 ? PageType.NoteDetail : PageType.Unknown;

                default:
                    return PageType.Unknown;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DOM检测页面类型时发生异常: {ExpectedType}", expectedType);
            return PageType.Unknown;
        }
    }

    /// <summary>
    /// 页面类型特定的元素检测
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="status">页面状态信息</param>
    private async Task DetectPageSpecificElements(IPage page, PageStatusInfo status)
    {
        switch (status.PageType)
        {
            case PageType.Recommend:
                var discoverElements = await CountElements(page, [
                    "#exploreFeeds",
                    "[data-testid='explore-page']",
                    ".channel-container",
                    ".note-item"
                ]);
                status.ElementsDetected["discover_elements"] = discoverElements;
                status.ElementsDetected["note_items"] = await CountElements(page, [".note-item"]);
                break;

            case PageType.Search:
                status.ElementsDetected["search_results"] = await CountElements(page, [
                    ".search-result-item",
                    "[data-testid='search-result']"
                ]);
                status.ElementsDetected["search_filters"] = await CountElements(page, [".search-filter"]);
                break;

            case PageType.Profile:
                status.ElementsDetected["profile_info"] = await CountElements(page, [".profile-info"]);
                status.ElementsDetected["user_notes"] = await CountElements(page, [".user-note-item"]);
                break;

            case PageType.NoteDetail:
                status.ElementsDetected["note_content"] = await CountElements(page, [".note-content"]);
                status.ElementsDetected["note_comments"] = await CountElements(page, [".comment-item"]);
                break;

            default:
                status.ElementsDetected["general_elements"] = await CountElements(page, ["[class*='note']", "[class*='item']", "[class*='card']"]);
                break;
        }
    }

    /// <summary>
    /// API特征检测
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="status">页面状态信息</param>
    private async Task DetectApiFeatures(IPage page, PageStatusInfo status)
    {
        try
        {
            // 检测网络请求特征
            var networkRequests = await page.EvaluateAsync<string[]>(
                """

                                Array.from(performance.getEntriesByType('resource'))
                                    .map(entry => entry.name)
                                    .filter(name => 
                                        name.includes('homefeed') || 
                                        name.includes('explore') ||
                                        name.includes('search/notes') ||
                                        name.includes('user/profile') ||
                                        name.includes('note/detail')
                                    )
                            
                """);

            status.ApiFeatures.AddRange(networkRequests);
            status.AddDetectionLog($"检测到 {networkRequests.Length} 个相关API请求");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API特征检测失败");
            status.AddDetectionLog("API特征检测失败");
        }
    }

    /// <summary>
    /// 计算页面元素数量
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="selectors">选择器数组</param>
    /// <returns>元素总数</returns>
    private async Task<int> CountElements(IPage page, string[] selectors)
    {
        var totalCount = 0;

        foreach (var selector in selectors)
        {
            try
            {
                var elements = await page.QuerySelectorAllAsync(selector);
                totalCount += elements.Count;
            }
            catch
            {
                // 忽略选择器错误
            }
        }

        return totalCount;
    }

    /// <summary>
    /// 确定页面就绪状态
    /// </summary>
    /// <param name="status">页面状态信息</param>
    /// <returns>是否就绪</returns>
    private bool DeterminePageReadiness(PageStatusInfo status)
    {
        // 基于页面类型和检测到的元素数量确定就绪状态
        switch (status.PageType)
        {
            case PageType.Recommend:
                return status.GetElementCount("discover_elements") > 0 ||
                       status.GetElementCount("note_items") > 0;

            case PageType.Search:
                return status.GetElementCount("search_results") > 0;

            case PageType.Profile:
                return status.GetElementCount("profile_info") > 0;

            case PageType.NoteDetail:
                return status.GetElementCount("note_content") > 0;

            case PageType.Home:
                return status.GetElementCount("general_elements") > 0;

            case PageType.Unknown:
            default:
                // 对于未知页面，如果有任何相关元素就认为就绪
                return status.ElementsDetected.Values.Any(count => count > 0) ||
                       status.ApiFeatures.Count > 0;
        }
    }

    [GeneratedRegex(@"#([\p{L}\p{N}_\-]+)#?")]
    private static partial Regex TagRegex();
    #endregion
}
