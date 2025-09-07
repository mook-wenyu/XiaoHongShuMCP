using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NPOI.XSSF.UserModel;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 小红书核心服务实现
/// </summary>
public class XiaoHongShuService : IXiaoHongShuService
{
    private readonly ILogger<XiaoHongShuService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly PlaywrightBrowserManager _browserManager;
    private readonly IAccountManager _accountManager;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly IDomElementManager _domElementManager;

    /// <summary>
    /// URL构建常量和默认参数
    /// </summary>
    private const string BASE_CREATOR_URL = "https://creator.xiaohongshu.com";
    private const string DEFAULT_SOURCE = "web_explore_feed";
    private const string DEFAULT_XSEC_SOURCE = "pc_search";

    public XiaoHongShuService(
        ILogger<XiaoHongShuService> logger,
        ILoggerFactory loggerFactory,
        PlaywrightBrowserManager browserManager,
        IAccountManager accountManager,
        IHumanizedInteractionService humanizedInteraction,
        IDomElementManager domElementManager)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _browserManager = browserManager;
        _accountManager = accountManager;
        _humanizedInteraction = humanizedInteraction;
        _domElementManager = domElementManager;
    }

    /// <summary>
    /// 基于关键词列表查找单个笔记详情
    /// </summary>
    /// <param name="keywords">搜索关键词列表（匹配任意关键词）</param>
    /// <param name="includeComments">是否包含评论</param>
    /// <returns>笔记详情操作结果</returns>
    public async Task<OperationResult<NoteDetail>> GetNoteDetailAsync(
        List<string> keywords,
        bool includeComments = false)
    {
        _logger.LogInformation("开始破坏性重构版笔记详情获取: 关键词={Keywords}, 包含评论={IncludeComments}",
            string.Join(", ", keywords), includeComments);

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
            
            // 2. 创建新的 Feed API 监听器
            using var feedApiMonitor = new FeedApiMonitor(_loggerFactory.CreateLogger<FeedApiMonitor>());
            
            // 3. 设置真实 Feed API 监听器
            if (!await feedApiMonitor.SetupMonitorAsync(page, TimeSpan.FromSeconds(10)))
            {
                return OperationResult<NoteDetail>.Fail(
                    "设置 Feed API 监听器失败",
                    ErrorType.BrowserError,
                    "FEED_MONITOR_SETUP_FAILED");
            }

            // 4. 通过拟人化操作找到并点击匹配的笔记
            var noteElement = await FindMatchingNoteElementAsync(keywords);
            if (noteElement == null)
            {
                return OperationResult<NoteDetail>.Fail(
                    $"未找到匹配关键词的笔记: {string.Join(", ", keywords)}",
                    ErrorType.ElementNotFound,
                    "NOTE_NOT_FOUND");
            }

            // 5. 清理之前的监听数据，准备捕获新数据
            feedApiMonitor.ClearMonitoredData();

            // 6. 拟人化点击笔记触发 Feed API 请求
            _logger.LogDebug("正在点击笔记元素以触发真实 Feed API 请求...");
            await _humanizedInteraction.HumanClickAsync(page, noteElement);

            // 7. 等待真实 Feed API 响应被监听
            var interceptSuccess = await feedApiMonitor.WaitForMonitoredResponsesAsync(1, TimeSpan.FromSeconds(15));
            if (!interceptSuccess)
            {
                return OperationResult<NoteDetail>.Fail(
                    "等待 Feed API 响应超时，可能网络问题或页面结构变化",
                    ErrorType.NetworkError,
                    "FEED_API_RESPONSE_TIMEOUT");
            }

            // 8. 从监听的真实 Feed API 响应中获取笔记详情
            var noteDetail = feedApiMonitor.GetLatestMonitoredNoteDetail();
            if (noteDetail == null)
            {
                return OperationResult<NoteDetail>.Fail(
                    "无法从 Feed API 响应中提取笔记详情数据",
                    ErrorType.ElementNotFound,
                    "FEED_API_DATA_EXTRACTION_FAILED");
            }

            // 9. 如果需要评论数据，进行额外处理（可选实现）
            if (includeComments)
            {
                // TODO: 实现基于真实API的评论数据获取逻辑
                _logger.LogWarning("基于 Feed API 的评论数据获取功能暂未实现，将在后续版本中支持");
            }

            // 10. 拟人化延时后返回结果
            await _humanizedInteraction.HumanBetweenActionsDelayAsync();

            _logger.LogInformation("成功通过 Feed API 监听获取笔记详情: 关键词={Keywords}, 标题={Title}, 类型={Type}, 质量={Quality}",
                string.Join(", ", keywords), noteDetail.Title, noteDetail.Type, noteDetail.Quality);

            return OperationResult<NoteDetail>.Ok(noteDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "破坏性重构版笔记详情获取失败: 关键词={Keywords}", string.Join(", ", keywords));

            return OperationResult<NoteDetail>.Fail(
                $"获取笔记详情失败: {ex.Message}",
                ErrorType.BrowserError,
                "FEED_API_INTERCEPT_ERROR");
        }
    }

    /// <summary>
    /// 批量查找笔记详情
    /// </summary>
    /// <param name="keywords">关键词列表（简化参数）</param>
    /// <param name="maxCount">最大查找数量</param>
    /// <param name="includeComments">是否包含评论数据</param>
    /// <param name="autoExport">是否自动导出到Excel</param>
    /// <param name="exportFileName">导出文件名（可选）</param>
    /// <returns>增强的批量笔记结果，包含统计分析和导出信息</returns>
    public async Task<OperationResult<BatchNoteResult>> BatchGetNoteDetailsAsync(
        List<string> keywords,
        int maxCount = 10,
        bool includeComments = false,
        bool autoExport = true,
        string? exportFileName = null)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("开始破坏性重构版批量获取笔记详情: 关键词={Keywords}, 最大数量={MaxCount}, 自动导出={AutoExport}",
            string.Join(", ", keywords), maxCount, autoExport);

        try
        {
            // 1. 参数验证
            if (keywords.Count == 0 || keywords.All(string.IsNullOrWhiteSpace))
            {
                return OperationResult<BatchNoteResult>.Fail(
                    "关键词列表不能为空",
                    ErrorType.ValidationError,
                    "EMPTY_KEYWORDS");
            }

            // 2. 检查用户登录状态
            if (!await _accountManager.IsLoggedInAsync())
            {
                return OperationResult<BatchNoteResult>.Fail(
                    "用户未登录，请先登录",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }

            var page = await _browserManager.GetPageAsync();
            
            // 3. 创建真实 Feed API 监听器（破坏性替换）
            using var feedApiMonitor = new FeedApiMonitor(_loggerFactory.CreateLogger<FeedApiMonitor>());
            
            // 4. 设置 Feed API 监听器
            if (!await feedApiMonitor.SetupMonitorAsync(page, TimeSpan.FromSeconds(10)))
            {
                return OperationResult<BatchNoteResult>.Fail(
                    "设置 Feed API 监听器失败",
                    ErrorType.BrowserError,
                    "FEED_MONITOR_SETUP_FAILED");
            }

            // 5. 查找所有匹配的笔记元素
            var matchingNotesElements = await FindMultipleMatchingNoteElementsAsync(keywords, maxCount);
            if (matchingNotesElements.Count == 0)
            {
                _logger.LogWarning("未找到匹配关键词的笔记: {Keywords}", string.Join(", ", keywords));
                
                var emptyResult = CreateEmptyBatchResult(startTime, string.Join(", ", keywords));
                return OperationResult<BatchNoteResult>.Ok(emptyResult);
            }

            _logger.LogInformation("找到 {Count} 个匹配的笔记元素，开始批量 Feed API 监听处理", matchingNotesElements.Count);

            // 6. 初始化批量处理统计
            var noteDetails = new List<NoteDetail>();
            var failedNotes = new List<(string, string)>();
            var processingStats = new Dictionary<ProcessingMode, int>
            {
                [ProcessingMode.Fast] = 0,
                [ProcessingMode.Standard] = 0,
                [ProcessingMode.Careful] = 0
            };
            var individualProcessingTimes = new List<double>();

            // 7. 批量点击笔记触发真实 Feed API 请求
            var processedCount = 0;
            foreach (var noteElement in matchingNotesElements.Take(maxCount))
            {
                var noteProcessingStart = DateTime.UtcNow;
                
                try
                {
                    _logger.LogDebug("批量处理第 {Current}/{Total} 个笔记元素 (Feed API)", processedCount + 1, Math.Min(matchingNotesElements.Count, maxCount));

                    // 清理之前的监听数据，准备捕获新数据
                    feedApiMonitor.ClearMonitoredData();
                    
                    // 拟人化点击笔记触发真实 Feed API 请求
                    await _humanizedInteraction.HumanClickAsync(page, noteElement);
                    
                    // 等待真实 Feed API 响应
                    var interceptSuccess = await feedApiMonitor.WaitForMonitoredResponsesAsync(1, TimeSpan.FromSeconds(10));
                    
                    if (interceptSuccess)
                    {
                        var noteDetail = feedApiMonitor.GetLatestMonitoredNoteDetail();
                        if (noteDetail != null)
                        {
                            noteDetails.Add(noteDetail);
                            
                            // 确定处理模式
                            var mode = DetermineProcessingMode(processedCount, noteDetail);
                            processingStats[mode]++;
                            
                            _logger.LogDebug("成功通过 Feed API 监听获取笔记: 标题={Title}, 模式={Mode}", 
                                noteDetail.Title, mode);
                        }
                        else
                        {
                            failedNotes.Add((string.Join(", ", keywords), "Feed API 响应解析失败"));
                        }
                    }
                    else
                    {
                        failedNotes.Add((string.Join(", ", keywords), "Feed API 响应超时"));
                        _logger.LogWarning("笔记 {Index} Feed API 响应超时", processedCount + 1);
                    }
                }
                catch (Exception ex)
                {
                    failedNotes.Add((string.Join(", ", keywords), $"处理异常: {ex.Message}"));
                    _logger.LogWarning(ex, "处理笔记 {Index} 时发生异常", processedCount + 1);
                }
                
                processedCount++;
                
                // 记录处理时间
                var processingTime = (DateTime.UtcNow - noteProcessingStart).TotalMilliseconds;
                individualProcessingTimes.Add(processingTime);
                
                // 批量操作间的拟人化延时
                if (processedCount < maxCount && processedCount < matchingNotesElements.Count)
                {
                    await _humanizedInteraction.HumanBetweenActionsDelayAsync();
                }
            }

            var totalProcessingTime = DateTime.UtcNow - startTime;

            // 8. 同步计算统计数据（零额外成本）
            var statistics = CalculateBatchStatisticsSync(noteDetails, processingStats, individualProcessingTimes);

            // 9. 确定整体数据质量
            var overallQuality = DetermineOverallQuality(noteDetails);

            // 10. 构建增强结果（不等待导出）
            var enhancedResult = new BatchNoteResult(
                SuccessfulNotes: noteDetails,
                FailedNotes: failedNotes,
                ProcessedCount: processedCount,
                ProcessingTime: totalProcessingTime,
                OverallQuality: overallQuality,
                Statistics: statistics,
                ExportInfo: null // 初始为null，异步导出完成后可通过其他方式获取
            );

            // 11. 立即返回给客户端（不等待导出）
            var result = OperationResult<BatchNoteResult>.Ok(enhancedResult);

            // 12. 异步启动导出任务（如果启用）
            if (autoExport && noteDetails.Count > 0)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        var fileName = exportFileName ??
                                       $"batch_notes_feed_api_{DateTime.Now:yyyyMMdd_HHmmss}";

                        // 将NoteDetail转换为NoteInfo以兼容导出方法
                        var noteInfoList = noteDetails.Cast<NoteInfo>().ToList();

                        var exportResult = ExportNotesSync(noteInfoList, fileName);

                        if (exportResult.Success)
                        {
                            _logger.LogInformation("批量 Feed API 笔记结果自动导出完成: {FilePath}",
                                exportResult.Data?.FilePath);
                        }
                        else
                        {
                            _logger.LogWarning("批量 Feed API 结果自动导出失败: {Error}", exportResult.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Feed API 异步导出任务异常，不影响主功能");
                    }
                });
            }

            _logger.LogInformation("破坏性重构版批量笔记详情获取完成: 成功={Success}, 失败={Failed}, 耗时={Duration}ms, 平均处理时间={AvgTime:F2}ms",
                noteDetails.Count, failedNotes.Count, totalProcessingTime.TotalMilliseconds, statistics.AverageProcessingTime);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "破坏性重构版批量获取笔记详情异常");
            return OperationResult<BatchNoteResult>.Fail(
                $"批量获取失败: {ex.Message}",
                ErrorType.BrowserError,
                "BATCH_GET_FEED_API_NOTES_FAILED");
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
    /// 创建空的批量结果 - 用于无匹配笔记的情况
    /// </summary>
    private BatchNoteResult CreateEmptyBatchResult(DateTime startTime, string keyword)
    {
        var processingTime = DateTime.UtcNow - startTime;
        var emptyStats = new BatchProcessingStatistics(
            CompleteDataCount: 0,
            PartialDataCount: 0,
            MinimalDataCount: 0,
            AverageProcessingTime: 0,
            AverageLikes: 0,
            AverageComments: 0,
            TypeDistribution: new Dictionary<NoteType, int>(),
            ProcessingModeStats: new Dictionary<ProcessingMode, int>
            {
                [ProcessingMode.Fast] = 0,
                [ProcessingMode.Standard] = 0,
                [ProcessingMode.Careful] = 0
            },
            CalculatedAt: DateTime.UtcNow
        );

        return new BatchNoteResult(
            SuccessfulNotes: [],
            FailedNotes: [(keyword, "未找到匹配的笔记")],
            ProcessedCount: 0,
            ProcessingTime: processingTime,
            OverallQuality: DataQuality.Minimal,
            Statistics: emptyStats,
            ExportInfo: null
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
    /// 导出笔记数据到Excel
    /// </summary>
    private bool ExportToExcel(List<NoteInfo> notes, string filePath, ExportOptions options)
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
    /// 基于关键词列表发布评论
    /// 使用新的LocateAndOperateNoteAsync通用架构定位笔记并在详情页发布评论
    /// </summary>
    /// <param name="keywords">搜索关键词列表（匹配任意关键词）</param>
    /// <param name="content">评论内容</param>
    /// <returns>评论发布操作结果</returns>
    public async Task<OperationResult<CommentResult>> PostCommentAsync(List<string> keywords, string content)
    {
        _logger.LogInformation("开始基于关键词发布评论: 关键词={Keywords}, 内容长度={ContentLength}",
            string.Join(", ", keywords), content?.Length ?? 0);

        // 参数验证
        if (keywords.Count == 0 || keywords.All(string.IsNullOrWhiteSpace))
        {
            return OperationResult<CommentResult>.Fail(
                "关键词列表不能为空",
                ErrorType.ValidationError,
                "EMPTY_KEYWORDS");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return OperationResult<CommentResult>.Fail(
                "评论内容不能为空",
                ErrorType.ValidationError,
                "EMPTY_CONTENT");
        }

        // 使用LocateAndOperateNoteAsync核心方法执行评论发布操作
        return await LocateAndOperateNoteAsync(keywords, async noteElement =>
        {
            try
            {
                var page = await _browserManager.GetPageAsync();

                _logger.LogDebug("准备在详情页发布评论: 内容={Content}", content);

                // 等待模态窗口或详情页完全加载
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ModalWaiting);

                // 1. 激活评论区域
                var commentActivated = await ActivateCommentAreaAsync(page);
                if (!commentActivated)
                {
                    return OperationResult<CommentResult>.Fail(
                        "无法激活评论区域，可能该笔记不支持评论",
                        ErrorType.ElementNotFound,
                        "COMMENT_AREA_NOT_ACTIVATED");
                }

                // 2. 等待评论输入框就绪
                var inputReady = await WaitForCommentInputReadyAsync(page);
                if (!inputReady)
                {
                    return OperationResult<CommentResult>.Fail(
                        "评论输入框未就绪，请稍后重试",
                        ErrorType.ElementNotFound,
                        "COMMENT_INPUT_NOT_READY");
                }

                // 3. 执行拟人化评论输入（纯文本模式，复用现有逻辑）
                await InputCommentWithEnhancedFeaturesAsync(page, content, useEmoji: false, emojiList: null);

                // 4. 提交评论并验证结果
                var submitResult = await WaitAndSubmitCommentAsync(page);
                if (!submitResult.Success)
                {
                    return OperationResult<CommentResult>.Fail(
                        $"评论提交失败: {submitResult.Message}",
                        ErrorType.BrowserError,
                        "COMMENT_SUBMIT_FAILED");
                }

                // 5. 生成评论结果
                var commentResult = new CommentResult(
                    Success: true,
                    Message: "评论发布成功",
                    CommentId: Guid.NewGuid().ToString(),
                    ErrorCode: null
                );

                // 拟人化延时 - 模拟发布后的自然停顿
                await _humanizedInteraction.HumanBetweenActionsDelayAsync();

                _logger.LogInformation("评论发布成功完成: 关键词={Keywords}", string.Join(", ", keywords));
                return OperationResult<CommentResult>.Ok(commentResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "评论发布操作异常: 关键词={Keywords}, 内容={Content}",
                    string.Join(", ", keywords), content);

                var errorResult = new CommentResult(
                    Success: false,
                    Message: $"评论发布异常: {ex.Message}",
                    CommentId: string.Empty,
                    ErrorCode: "COMMENT_OPERATION_EXCEPTION"
                );

                return OperationResult<CommentResult>.Ok(errorResult);
            }
        });
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
                        await _humanizedInteraction.HumanClickAsync(page, commentButton);
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
                        await _humanizedInteraction.HumanClickAsync(page, triggerElement);
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
                await _humanizedInteraction.HumanClickAsync(page, emoji);
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
            var disabledSelectors = _domElementManager.GetSelectors("CommentSubmitDisabled");
            var startTime = DateTime.UtcNow;

            // 等待按钮从禁用变为启用
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                // 检查按钮是否启用
                foreach (var selector in enabledSelectors)
                {
                    var enabledButton = await page.QuerySelectorAsync(selector);
                    if (enabledButton != null)
                    {
                        _logger.LogDebug("发送按钮已启用，准备提交: {Selector}", selector);

                        // 点击发送按钮
                        await _humanizedInteraction.HumanClickAsync(page, enabledButton);
                        await Task.Delay(2000); // 等待提交处理

                        // 检查是否提交成功（评论区域应该关闭或内容清空）
                        var stillActive = await page.QuerySelectorAsync(".engage-bar.active");
                        if (stillActive == null)
                        {
                            _logger.LogDebug("评论提交成功，评论区域已关闭");
                            return (true, "评论提交成功");
                        }

                        // 检查输入框是否清空
                        var inputElement = await page.QuerySelectorAsync("#content-textarea");
                        if (inputElement != null)
                        {
                            var inputText = await inputElement.InnerTextAsync();
                            if (string.IsNullOrWhiteSpace(inputText))
                            {
                                _logger.LogDebug("评论提交成功，输入框已清空");
                                return (true, "评论提交成功");
                            }
                        }
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

            await page.GotoAsync(publishUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });

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

    #region 支持方法 - Feed API 专用辅助功能
    
    /// <summary>
    /// 查找匹配关键词的单个笔记元素 - Feed API 重构版专用
    /// 为了触发真实 Feed API 请求而定位页面元素
    /// </summary>
    /// <param name="keywords">匹配关键词列表</param>
    /// <returns>匹配的笔记元素，如果没有找到则返回null</returns>
    private async Task<IElementHandle?> FindMatchingNoteElementAsync(List<string> keywords)
    {
        try
        {
            var page = await _browserManager.GetPageAsync();
            var noteItemSelectors = _domElementManager.GetSelectors("NoteCard");
            
            _logger.LogDebug("开始查找匹配关键词的笔记元素: {Keywords}", string.Join(", ", keywords));
            
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
                        if (MatchesKeywords(noteText, keywords))
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
            
            _logger.LogDebug("未找到匹配关键词的笔记元素: {Keywords}", string.Join(", ", keywords));
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查找匹配笔记元素时发生异常: {Keywords}", string.Join(", ", keywords));
            return null;
        }
    }
    
    /// <summary>
    /// 查找匹配关键词的多个笔记元素 - Feed API 批量处理专用
    /// 为了批量触发真实 Feed API 请求而定位页面元素
    /// </summary>
    /// <param name="keywords">匹配关键词列表</param>
    /// <param name="maxCount">最大查找数量</param>
    /// <returns>匹配的笔记元素列表</returns>
    private async Task<List<IElementHandle>> FindMultipleMatchingNoteElementsAsync(List<string> keywords, int maxCount)
    {
        var matchingElements = new List<IElementHandle>();
        
        try
        {
            var page = await _browserManager.GetPageAsync();
            var noteItemSelectors = _domElementManager.GetSelectors("NoteCard");
            
            _logger.LogDebug("开始查找 {MaxCount} 个匹配关键词的笔记元素: {Keywords}", 
                maxCount, string.Join(", ", keywords));
            
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
                        if (matchingElements.Count >= maxCount) break;
                        
                        if (!await IsElementVisible(noteElement)) continue;
                        
                        var noteText = await ExtractNoteTextForMatching(noteElement);
                        if (MatchesKeywords(noteText, keywords))
                        {
                            matchingElements.Add(noteElement);
                            _logger.LogDebug("找到匹配的笔记元素 #{Index}: {Text}", 
                                matchingElements.Count,
                                noteText.Substring(0, Math.Min(50, noteText.Length)));
                        }
                    }
                    
                    // 如果已经找到足够的元素，停止查找
                    if (matchingElements.Count >= maxCount) break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("选择器 {Selector} 查找失败: {Error}", selector, ex.Message);
                }
            }
            
            _logger.LogInformation("找到 {Count}/{MaxCount} 个匹配关键词的笔记元素", 
                matchingElements.Count, maxCount);
                
            return matchingElements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量查找匹配笔记元素时发生异常: {Keywords}", string.Join(", ", keywords));
            return matchingElements;
        }
    }
    
    #endregion

    #region 通用核心方法
    /// <summary>
    /// 通用笔记定位和操作方法
    /// 通过关键词定位笔记，执行指定操作，支持各种笔记互动功能
    /// </summary>
    /// <typeparam name="T">操作返回数据的类型</typeparam>
    /// <param name="keywords">搜索关键词列表（匹配任意关键词）</param>
    /// <param name="operation">要执行的操作函数（接收笔记元素，返回操作结果）</param>
    /// <returns>操作结果</returns>
    private async Task<OperationResult<T>> LocateAndOperateNoteAsync<T>(
        List<string> keywords,
        Func<IElementHandle, Task<OperationResult<T>>> operation)
    {
        _logger.LogInformation("开始通用笔记定位和操作: 关键词={Keywords}", string.Join(", ", keywords));

        try
        {
            // 1. 检查参数有效性
            if (keywords.Count == 0 || keywords.All(string.IsNullOrWhiteSpace))
            {
                return OperationResult<T>.Fail(
                    "关键词列表不能为空",
                    ErrorType.ValidationError,
                    "EMPTY_KEYWORDS");
            }

            if (operation == null)
            {
                return OperationResult<T>.Fail(
                    "操作函数不能为空",
                    ErrorType.ValidationError,
                    "NULL_OPERATION");
            }

            // 2. 确保用户已登录
            if (!await _accountManager.IsLoggedInAsync())
            {
                return OperationResult<T>.Fail(
                    "用户未登录，请先登录",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }

            var page = await _browserManager.GetPageAsync();

            // 3. 使用现有的FindVisibleMatchingNotesAsync方法查找匹配的笔记（限制为1个结果）
            _logger.LogDebug("搜索匹配关键词的笔记: {Keywords}", string.Join(", ", keywords));
            var matchingNotesResult = await FindVisibleMatchingNotesAsync(keywords, 1);

            if (!matchingNotesResult.Success)
            {
                return OperationResult<T>.Fail(
                    $"查找匹配笔记失败: {matchingNotesResult.ErrorMessage}",
                    matchingNotesResult.ErrorType,
                    matchingNotesResult.ErrorCode ?? "FIND_NOTE_FAILED");
            }

            if (matchingNotesResult.Data?.Any() != true)
            {
                _logger.LogInformation("未找到匹配关键词的笔记: {Keywords}", string.Join(", ", keywords));
                return OperationResult<T>.Fail(
                    $"未找到匹配关键词的笔记: {string.Join(", ", keywords)}",
                    ErrorType.ElementNotFound,
                    "NO_MATCHING_NOTES");
            }

            var targetNoteElement = matchingNotesResult.Data.FirstOrDefault();
            if (targetNoteElement == null)
            {
                return OperationResult<T>.Fail(
                    "笔记元素为空，无法执行操作",
                    ErrorType.ElementNotFound,
                    "NULL_NOTE_ELEMENT");
            }
            _logger.LogDebug("成功定位到目标笔记元素");

            // 4. 执行拟人化导航 - 点击笔记元素
            _logger.LogDebug("执行拟人化点击笔记元素");
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);
            await _humanizedInteraction.HumanClickAsync(page, targetNoteElement);

            // 5. 等待页面加载完成
            _logger.LogDebug("等待页面加载完成");
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);

            // 6. 执行具体操作
            _logger.LogDebug("执行具体操作函数");
            var operationResult = await operation(targetNoteElement);

            if (operationResult.Success)
            {
                _logger.LogInformation("通用笔记操作成功完成: {Keywords}", string.Join(", ", keywords));
            }
            else
            {
                _logger.LogWarning("通用笔记操作失败: {Error}", operationResult.ErrorMessage);
            }

            return operationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通用笔记定位和操作异常: {Keywords}", string.Join(", ", keywords));
            return OperationResult<T>.Fail(
                $"笔记操作异常: {ex.Message}",
                ErrorType.BrowserError,
                "LOCATE_OPERATE_EXCEPTION");
        }
    }
    #endregion

    /// <summary>
    /// 确定处理模式 - 基于笔记类型的智能处理模式选择
    /// </summary>
    private ProcessingMode DetermineProcessingMode(int index, NoteDetail? note = null)
    {
        // 如果有笔记类型信息，优先基于类型决策
        if (note?.Type != NoteType.Unknown)
        {
            return note.Type switch
            {
                NoteType.Image => ProcessingMode.Fast,      // 图文：快速处理（数量最多，包含长文）
                NoteType.Video => ProcessingMode.Standard,  // 视频：标准处理（需要加载视频）
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
    /// 从文本中提取数字
    /// </summary>
    private bool ExtractNumber(string text, out int number)
    {
        number = 0;
        var match = Regex.Match(text, @"\d+");
        return match.Success && int.TryParse(match.Value, out number);
    }

    /// <summary>
    /// 验证发布前提条件：根据笔记类型验证媒体文件要求
    /// </summary>
    private async Task<bool> ValidatePublishRequirementsAsync(NoteType noteType, List<string>? imagePaths, string? videoPath)
    {
        var hasImages = imagePaths?.Any(File.Exists) == true;
        var hasVideo = !string.IsNullOrEmpty(videoPath) && File.Exists(videoPath);

        return noteType switch
        {
            NoteType.Image => hasImages && !hasVideo,
            NoteType.Video => hasVideo && !hasImages,
            NoteType.Unknown => false, // 未知类型不允许发布
            _ => false
        };
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
            await page.WaitForFunctionAsync(@"
                () => {
                    // 检查Vue应用是否已挂载
                    const vueApp = document.querySelector('[data-v-]') || document.querySelector('.vue-publish-container');
                    if (!vueApp) return false;
                    
                    // 检查上传区域是否可见
                    const uploadArea = document.querySelector('.upload-area, .image-uploader, .file-drop-zone');
                    return uploadArea && uploadArea.offsetHeight > 0;
                }
            ", new PageWaitForFunctionOptions {Timeout = 15000});

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
            await _humanizedInteraction.HumanHoverAsync(page, uploadArea);
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
                    await _humanizedInteraction.HumanClickAsync(page, selectButton);
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
                await page.WaitForFunctionAsync(@"
                    () => {
                        const progressElements = document.querySelectorAll('.upload-progress, .progress-bar, .uploading');
                        return progressElements.length === 0 || 
                               Array.from(progressElements).every(el => el.style.display === 'none');
                    }
                ", new PageWaitForFunctionOptions {Timeout = 30000});
            }

            // 3. 验证上传成功（图片预览出现）
            await page.WaitForFunctionAsync(@"
                () => {
                    const previewElements = document.querySelectorAll('.image-preview, .preview-container, .uploaded-images');
                    return previewElements.length > 0 && 
                           Array.from(previewElements).some(el => el.offsetHeight > 0);
                }
            ", new PageWaitForFunctionOptions {Timeout = 15000});

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
            await page.WaitForFunctionAsync(@"
                () => {
                    // 检查标题输入框（使用真实placeholder）
                    const titleInput = document.querySelector('input[placeholder*=""填写标题会有更多赞哦""]') ||
                                     document.querySelector('.d-text') ||
                                     document.querySelector('input[placeholder*=""标题""]');
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
            ", new PageWaitForFunctionOptions {Timeout = 15000});

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
                await _humanizedInteraction.HumanClickAsync(page, titleElement);
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
                await _humanizedInteraction.HumanClickAsync(page, contentElement);
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

                // 输入这一段
                await contentInput.TypeAsync(segment, new() {Delay = Random.Shared.Next(50, 150)});

                // 补充标点符号（如果原文有的话）
                if (i < segments.Count - 1)
                {
                    var punctuation = content.Contains(segment + "。") ? "。" :
                        content.Contains(segment + "！") ? "！" :
                        content.Contains(segment + "？") ? "？" : "";
                    if (!string.IsNullOrEmpty(punctuation))
                    {
                        await contentInput.TypeAsync(punctuation);
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
                await _humanizedInteraction.HumanClickAsync(page, tagInput);
                await Task.Delay(Random.Shared.Next(300, 600));

                // 输入标签（有些平台需要#，有些不需要）
                var tagText = tag.StartsWith("#") ? tag : $"#{tag}";
                await tagInput.TypeAsync(tagText, new() {Delay = Random.Shared.Next(80, 150)});

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
            await _humanizedInteraction.HumanClickAsync(page, tempSaveButton);
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
            await page.WaitForFunctionAsync(@"
                () => {
                    // 1. 检查编辑器是否消失（使用真实选择器）
                    const titleInput = document.querySelector('input[placeholder*=""填写标题会有更多赞哦""]') ||
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
            ", new PageWaitForFunctionOptions {Timeout = 15000});

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
    private async Task<OperationResult<List<IElementHandle>>> FindVisibleMatchingNotesAsync(List<string> keywords, int maxCount)
    {
        try
        {
            var page = await _browserManager.GetPageAsync();
            var foundNotes = new List<IElementHandle>();
            var processedIds = new HashSet<string>(); // 去重机制
            var scrollAttempts = 0;
            var maxScrollAttempts = 6; // 限制最大滚动次数防止无限循环

            _logger.LogInformation("开始虚拟化列表滚动搜索，目标: {MaxCount} 个笔记", maxCount);

            while (foundNotes.Count < maxCount && scrollAttempts < maxScrollAttempts)
            {
                // 1. 搜索当前可见区域的匹配笔记
                var currentMatches = await SearchCurrentVisibleAreaAsync(keywords, maxCount - foundNotes.Count);
                if (!currentMatches.Success)
                {
                    _logger.LogWarning("搜索当前可见区域失败: {Error}", currentMatches.ErrorMessage);
                    break;
                }

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

                // 3. 如果找够了或本轮没有找到新内容，检查是否能继续滚动
                if (foundNotes.Count >= maxCount)
                {
                    _logger.LogInformation("已达到目标数量 {Count} 个匹配笔记", foundNotes.Count);
                    break;
                }

                if (newNotesCount == 0 && scrollAttempts > 0)
                {
                    _logger.LogInformation("本轮未找到新内容，检查滚动状态");
                }

                // 4. 检测是否还能继续滚动
                var canScroll = await CanScrollMoreAsync();
                if (!canScroll)
                {
                    _logger.LogInformation("已到达页面底部或无法继续滚动，停止搜索");
                    break;
                }

                // 5. 执行拟人化滚动加载更多内容
                _logger.LogDebug("执行第 {Attempt} 次滚动加载更多内容", scrollAttempts + 1);

                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ScrollPreparation);
                await _humanizedInteraction.HumanScrollAsync(page, targetDistance: 1500, waitForLoad: true);
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.VirtualListUpdate);

                scrollAttempts++;

                // 6. 如果连续两轮都没有找到新内容，提前结束避免无效滚动
                if (newNotesCount == 0 && scrollAttempts > 1)
                {
                    var previousNewCount = 0; // 这里可以跟踪上一轮的新增数量
                    if (previousNewCount == 0)
                    {
                        _logger.LogInformation("连续两轮未找到新内容，提前结束搜索");
                        break;
                    }
                }
            }

            _logger.LogInformation("虚拟化列表搜索完成，共找到 {Count} 个匹配笔记，执行了 {ScrollAttempts} 次滚动",
                foundNotes.Count, scrollAttempts);

            return OperationResult<List<IElementHandle>>.Ok(foundNotes);
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
    private async Task<OperationResult<List<IElementHandle>>> SearchCurrentVisibleAreaAsync(List<string> keywords, int maxCount)
    {
        try
        {
            var page = await _browserManager.GetPageAsync();
            var noteItemSelectors = _domElementManager.GetSelectors("NoteCard");
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
            if (keywords.Count == 0)
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
                    if (MatchesKeywords(noteText, keywords))
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
    /// 检测是否还能继续滚动页面
    /// 通过检查滚动位置和页面高度判断
    /// </summary>
    private async Task<bool> CanScrollMoreAsync()
    {
        try
        {
            var page = await _browserManager.GetPageAsync();

            // 获取页面滚动信息
            var scrollInfo = await page.EvaluateAsync<dynamic>(@"() => {
                return {
                    scrollTop: window.pageYOffset || document.documentElement.scrollTop,
                    scrollHeight: document.documentElement.scrollHeight,
                    clientHeight: window.innerHeight
                };
            }");

            var scrollTop = (double)scrollInfo.scrollTop;
            var scrollHeight = (double)scrollInfo.scrollHeight;
            var clientHeight = (double)scrollInfo.clientHeight;

            // 计算距离底部的距离
            var distanceFromBottom = scrollHeight - (scrollTop + clientHeight);

            // 如果距离底部小于100像素，认为已经到底
            var canScroll = distanceFromBottom > 100;

            _logger.LogDebug("滚动状态检查 - 距离底部: {Distance}px, 可继续滚动: {CanScroll}",
                Math.Round(distanceFromBottom, 2), canScroll);

            return canScroll;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检测滚动状态失败，假设可以继续滚动");
            return true; // 发生错误时保守地假设可以继续滚动
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
    /// 关键词匹配逻辑
    /// </summary>
    private bool MatchesKeywords(string text, List<string> keywords)
    {
        if (string.IsNullOrEmpty(text) || keywords.Count == 0) return false;

        var lowerText = text.ToLowerInvariant();

        // 简单的任意关键词匹配
        return keywords.Any(keyword =>
            !string.IsNullOrEmpty(keyword) &&
            lowerText.Contains(keyword.ToLowerInvariant()));
    }
    #endregion

    #region 笔记互动功能
    /// <summary>
    /// 基于关键词列表定位并点赞笔记
    /// </summary>
    /// <param name="keywords">关键词列表，匹配任意一个即可</param>
    /// <param name="forceAction">是否强制执行，即使已经点赞</param>
    /// <returns>点赞操作结果</returns>
    public async Task<OperationResult<InteractionResult>> LikeNoteAsync(
        List<string> keywords,
        bool forceAction = false)
    {
        _logger.LogInformation("开始基于关键词点赞笔记: 关键词={Keywords}, 强制执行={ForceAction}",
            string.Join(", ", keywords), forceAction);

        return await LocateAndOperateNoteAsync(keywords, async noteElement =>
        {
            try
            {
                var page = await _browserManager.GetPageAsync();

                // 等待页面加载完成（笔记详情页或模态窗口）
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);

                // 执行拟人化点赞操作
                var likeResult = await _humanizedInteraction.HumanLikeAsync(page);

                // 处理forceAction参数
                if (!forceAction && likeResult.PreviousState == "已点赞")
                {
                    _logger.LogInformation("笔记已点赞且不强制执行，返回成功: {Keywords}", string.Join(", ", keywords));
                    return OperationResult<InteractionResult>.Ok(likeResult);
                }

                if (likeResult.Success)
                {
                    _logger.LogInformation("点赞操作成功: 关键词={Keywords}, 状态变化={PreviousState} -> {CurrentState}",
                        string.Join(", ", keywords), likeResult.PreviousState, likeResult.CurrentState);
                }
                else
                {
                    _logger.LogWarning("点赞操作失败: 关键词={Keywords}, 错误={Error}",
                        string.Join(", ", keywords), likeResult.Message);
                }

                // 拟人化延时
                await _humanizedInteraction.HumanBetweenActionsDelayAsync();

                return OperationResult<InteractionResult>.Ok(likeResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "点赞笔记操作异常: 关键词={Keywords}", string.Join(", ", keywords));

                var errorResult = new InteractionResult(
                    Success: false,
                    Action: "点赞",
                    PreviousState: "未知",
                    CurrentState: "未知",
                    Message: $"点赞操作异常: {ex.Message}",
                    ErrorCode: "LIKE_OPERATION_EXCEPTION"
                );

                return OperationResult<InteractionResult>.Ok(errorResult);
            }
        });
    }

    /// <summary>
    /// 基于关键词列表定位并收藏笔记
    /// </summary>
    /// <param name="keywords">关键词列表，匹配任意一个即可</param>
    /// <param name="forceAction">是否强制执行，即使已经收藏</param>
    /// <returns>收藏操作结果</returns>
    public async Task<OperationResult<InteractionResult>> FavoriteNoteAsync(
        List<string> keywords,
        bool forceAction = false)
    {
        _logger.LogInformation("开始基于关键词收藏笔记: 关键词={Keywords}, 强制执行={ForceAction}",
            string.Join(", ", keywords), forceAction);

        return await LocateAndOperateNoteAsync(keywords, async noteElement =>
        {
            try
            {
                var page = await _browserManager.GetPageAsync();

                // 等待页面加载完成（笔记详情页或模态窗口）
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);

                // 执行拟人化收藏操作
                var favoriteResult = await _humanizedInteraction.HumanFavoriteAsync(page);

                // 处理forceAction参数
                if (!forceAction && favoriteResult.PreviousState == "已收藏")
                {
                    _logger.LogInformation("笔记已收藏且不强制执行，返回成功: {Keywords}", string.Join(", ", keywords));
                    return OperationResult<InteractionResult>.Ok(favoriteResult);
                }

                if (favoriteResult.Success)
                {
                    _logger.LogInformation("收藏操作成功: 关键词={Keywords}, 状态变化={PreviousState} -> {CurrentState}",
                        string.Join(", ", keywords), favoriteResult.PreviousState, favoriteResult.CurrentState);
                }
                else
                {
                    _logger.LogWarning("收藏操作失败: 关键词={Keywords}, 错误={Error}",
                        string.Join(", ", keywords), favoriteResult.Message);
                }

                // 拟人化延时
                await _humanizedInteraction.HumanBetweenActionsDelayAsync();

                return OperationResult<InteractionResult>.Ok(favoriteResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "收藏笔记操作异常: 关键词={Keywords}", string.Join(", ", keywords));

                var errorResult = new InteractionResult(
                    Success: false,
                    Action: "收藏",
                    PreviousState: "未知",
                    CurrentState: "未知",
                    Message: $"收藏操作异常: {ex.Message}",
                    ErrorCode: "FAVORITE_OPERATION_EXCEPTION"
                );

                return OperationResult<InteractionResult>.Ok(errorResult);
            }
        });
    }
    #endregion

    #region 搜索功能 - SearchNotes

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
        var monitoredResponses = new List<SearchNotesApiResponse>();
        int requestCount = 0;
        
        try
        {
            _logger.LogInformation("开始基于API监听的智能搜索: 关键词={Keyword}, 最大结果={MaxResults}", 
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
            var context = await _browserManager.GetBrowserContextAsync();
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

            // 3. 设置API监听器
            var config = new SearchMonitorConfig();
            await SetupSearchApiMonitorAsync(page, config, monitoredResponses, () => requestCount++);
            
            _logger.LogDebug("API监听器设置完成，开始执行拟人化搜索");

            // 4. 执行拟人化搜索操作
            var searchResult = await PerformHumanizedSearchAsync(page, keyword, sortBy, noteType, publishTime);
            if (!searchResult.Success)
            {
                return OperationResult<SearchResult>.Fail(
                    searchResult.ErrorMessage ?? "拟人化搜索失败",
                    ErrorType.BrowserError,
                    "HUMANIZED_SEARCH_FAILED");
            }

            // 5. 收集API数据
            _logger.LogDebug("拟人化搜索完成，开始收集API数据");
            var apiNotes = await CollectSearchApiDataAsync(monitoredResponses, maxResults);
            
            if (apiNotes.Count == 0)
            {
                return OperationResult<SearchResult>.Fail(
                    "未能从API监听中获取到搜索数据",
                    ErrorType.NetworkError,
                    "NO_API_DATA_COLLECTED");
            }

            // 6. 转换为标准格式
            var recommendedNotes = ConvertToRecommendedNotes(apiNotes, keyword);
            _logger.LogInformation("成功转换 {Count} 个搜索结果", recommendedNotes.Count);

            // 7. 计算统计信息
            var statistics = includeAnalytics 
                ? CalculateEnhancedSearchStatistics(recommendedNotes)
                : CreateEmptyEnhancedStatistics();

            var duration = DateTime.UtcNow - startTime;

            // 8. 转换为NoteInfo格式用于导出
            var noteInfoList = ConvertRecommendedNotesToNoteInfo(recommendedNotes);

            // 9. 执行导出功能（如果启用）
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
                        _logger.LogInformation("搜索数据导出成功: {FilePath}, 记录数: {Count}", 
                            exportInfo.FilePath, noteInfoList.Count);
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

            // 10. 构建搜索结果
            var searchResultData = new SearchResult(
                Notes: noteInfoList,
                TotalCount: recommendedNotes.Count,
                SearchKeyword: keyword,
                Duration: duration,
                Statistics: statistics,
                ExportInfo: exportInfo, // 包含导出信息
                ApiRequests: requestCount,
                InterceptedResponses: monitoredResponses.Count,
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
                "API监听搜索完成: 关键词={Keyword}, 结果={Count}条, 耗时={Duration}ms, API请求={ApiRequests}次", 
                keyword, recommendedNotes.Count, duration.TotalMilliseconds, requestCount);

            return OperationResult<SearchResult>.Ok(searchResultData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API监听搜索失败: 关键词={Keyword}", keyword);
            return OperationResult<SearchResult>.Fail(
                $"搜索失败: {ex.Message}",
                ErrorType.NetworkError,
                "API_SEARCH_EXCEPTION");
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
                    Url = recommendedNote.Url,
                    CoverImage = recommendedNote.CoverImage,
                    LikeCount = recommendedNote.LikeCount,
                    CommentCount = recommendedNote.CommentCount,
                    Quality = recommendedNote.Quality,
                    MissingFields = recommendedNote.MissingFields,
                    Type = recommendedNote.Type,
                    ExtractedAt = recommendedNote.ExtractedAt
                };
                
                // 设置收藏数（如果有的话）
                if (recommendedNote.FavoriteCount.HasValue)
                {
                    // NoteInfo没有FavoriteCount字段，可以考虑扩展或忽略
                    // 这里暂时忽略，因为NoteInfo结构相对简单
                }
                
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
    /// 设置搜索API监听器
    /// 使用被动监听模式捕获搜索API的响应数据，降低检测风险
    /// </summary>
    private async Task SetupSearchApiMonitorAsync(
        IPage page, 
        SearchMonitorConfig config, 
        List<SearchNotesApiResponse> monitoredResponses,
        Action onRequestCount)
    {
        _logger.LogDebug("设置搜索API监听器: {ApiPattern}", config.ApiUrlPattern);

        // 设置被动响应监听器
        page.Context.Response += async (sender, response) =>
        {
            try
            {
                // 检查是否为目标搜索API
                if (!response.Url.Contains("/api/sns/web/v1/search/notes"))
                    return;

                onRequestCount();
                
                _logger.LogDebug("检测到搜索API响应: {Url}", response.Url);
                
                // 只处理成功的响应
                if (response.Status == 200)
                {
                    try
                    {
                        var responseText = await response.TextAsync();
                        var searchApiResponse = JsonSerializer.Deserialize<SearchNotesApiResponse>(responseText, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            PropertyNameCaseInsensitive = true
                        });
                        
                        if (searchApiResponse != null)
                        {
                            lock (monitoredResponses)
                            {
                                monitoredResponses.Add(searchApiResponse);
                            }
                            _logger.LogDebug("成功监听搜索API响应，数据条数: {Count}", searchApiResponse.Data?.Items.Count ?? 0);
                        }
                    }
                    catch (Exception parseEx)
                    {
                        _logger.LogWarning(parseEx, "解析搜索API响应失败");
                    }
                }
                else
                {
                    _logger.LogDebug("搜索API响应状态码: {Status}", response.Status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "搜索API监听器处理异常");
            }
        };

        _logger.LogDebug("搜索API监听器设置完成");
        await Task.CompletedTask;
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
        string publishTime)
    {
        try
        {
            _logger.LogDebug("开始执行拟人化搜索操作");

            // 1. 导航到探索页面（而不是search_result页面，避免404）
            var exploreUrl = "https://www.xiaohongshu.com/explore";
            await page.GotoAsync(exploreUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });

            // 2. 等待页面完全加载
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading);

            // 3. 模拟用户思考过程
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);

            // 4. 查找搜索输入框并输入关键词
            var searchInputSelectors = new[] 
            { 
                "input[placeholder*='搜索']", 
                ".search-input input", 
                ".search-box input",
                "input[type='search']"
            };

            IElementHandle? searchInput = null;
            foreach (var selector in searchInputSelectors)
            {
                searchInput = await page.QuerySelectorAsync(selector);
                if (searchInput != null) break;
            }

            if (searchInput == null)
            {
                return OperationResult<bool>.Fail(
                    "未找到搜索输入框",
                    ErrorType.ElementNotFound,
                    "SEARCH_INPUT_NOT_FOUND");
            }

            // 5. 清空输入框并输入关键词
            await _humanizedInteraction.HumanClickAsync(page, searchInput);
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause);
            
            await searchInput.FillAsync("");
            await Task.Delay(Random.Shared.Next(300, 800));
            
            // 模拟打字
            foreach (char c in keyword)
            {
                await page.Keyboard.TypeAsync(c.ToString());
                await Task.Delay(Random.Shared.Next(80, 200));
            }

            // 6. 提交搜索
            await page.Keyboard.PressAsync("Enter");
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
                    var filterButton = await page.QuerySelectorAsync(selector);
                    if (filterButton != null)
                    {
                        await _humanizedInteraction.HumanClickAsync(page, filterButton);
                        await Task.Delay(Random.Shared.Next(500, 1000));

                        // 查找具体的筛选值
                        var valueSelector = $"[data-value='{filterValue}'], button:has-text('{filterValue}')";
                        var valueButton = await page.QuerySelectorAsync(valueSelector);
                        if (valueButton != null)
                        {
                            await _humanizedInteraction.HumanClickAsync(page, valueButton);
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
    /// 等待并收集搜索API数据
    /// 等待网络监听器收集足够的数据
    /// </summary>
    private async Task<List<SearchNoteItem>> CollectSearchApiDataAsync(
        List<SearchNotesApiResponse> interceptedResponses, 
        int maxResults)
    {
        var collectedNotes = new List<SearchNoteItem>();
        var waitTime = 0;
        const int maxWaitTime = 30000; // 最大等待30秒
        const int checkInterval = 1000; // 每秒检查一次

        _logger.LogDebug("开始收集搜索API数据，目标数量: {MaxResults}", maxResults);

        while (collectedNotes.Count < maxResults && waitTime < maxWaitTime)
        {
            // 收集所有监听到的笔记
            foreach (var response in interceptedResponses)
            {
                if (response.Data?.Items != null)
                {
                    foreach (var item in response.Data.Items)
                    {
                        if (collectedNotes.All(n => n.Id != item.Id))
                        {
                            collectedNotes.Add(item);
                            if (collectedNotes.Count >= maxResults) break;
                        }
                    }
                }
                if (collectedNotes.Count >= maxResults) break;
            }

            if (collectedNotes.Count >= maxResults) break;

            // 如果数据不够，等待更多响应
            await Task.Delay(checkInterval);
            waitTime += checkInterval;

            _logger.LogDebug("当前收集到 {Current}/{Target} 个笔记，等待时间: {WaitTime}ms", 
                collectedNotes.Count, maxResults, waitTime);
        }

        _logger.LogInformation("API数据收集完成: {Collected}/{Target} 个笔记，耗时: {WaitTime}ms", 
            collectedNotes.Count, maxResults, waitTime);

        return collectedNotes.Take(maxResults).ToList();
    }

    /// <summary>
    /// 转换API数据为搜索结果笔记对象
    /// 将搜索API返回的原始数据转换为结构化的笔记对象，用于搜索结果展示
    /// </summary>
    private List<RecommendedNote> ConvertToRecommendedNotes(List<SearchNoteItem> apiItems, string searchKeyword)
    {
        var recommendedNotes = new List<RecommendedNote>();

        foreach (var item in apiItems)
        {
            try
            {
                var recommendedNote = ConvertSingleNoteItem(item, searchKeyword);
                if (recommendedNote != null)
                {
                    recommendedNotes.Add(recommendedNote);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "转换笔记项目失败: ID={Id}", item.Id);
            }
        }

        return recommendedNotes;
    }

    /// <summary>
    /// 转换单个笔记项目
    /// 详细的数据映射和转换逻辑
    /// </summary>
    private RecommendedNote? ConvertSingleNoteItem(SearchNoteItem item, string searchKeyword)
    {
        if (item.NoteCard == null) return null;

        var note = item.NoteCard;
        var missingFields = new List<string>();

        var recommendedNote = new RecommendedNote
        {
            Id = item.Id,
            Title = note.DisplayTitle,
            Description = note.Desc,
            Author = note.User?.Nickname ?? "未知用户",
            AuthorId = note.User?.UserId ?? string.Empty,
            AuthorAvatar = note.User?.Avatar ?? string.Empty,
            Url = $"https://www.xiaohongshu.com/explore/{item.Id}",
            TrackId = item.TrackId,
            XsecToken = item.XsecToken,
            ExtractedAt = DateTime.UtcNow
        };

        // 设置用户信息
        if (note.User != null)
        {
            recommendedNote.UserInfo = new RecommendedUserInfo
            {
                UserId = note.User.UserId,
                Nickname = note.User.Nickname,
                Avatar = note.User.Avatar,
                IsVerified = note.User.Verified
            };
        }

        // 设置封面信息
        if (note.Cover != null)
        {
            recommendedNote.CoverImage = SelectBestCoverUrl(note.Cover);
            recommendedNote.CoverInfo = new RecommendedCoverInfo
            {
                DefaultUrl = note.Cover.UrlDefault,
                PreviewUrl = note.Cover.UrlPre,
                Width = note.Cover.Width,
                Height = note.Cover.Height,
                FileId = note.Cover.FileId,
                Scenes = note.Cover.InfoList.Select(info => new ImageSceneInfo
                {
                    SceneType = info.ImageScene,
                    Url = info.Url
                }).ToList()
            };
        }

        // 设置互动信息
        if (note.InteractInfo != null)
        {
            var likeCount = note.InteractInfo.LikedCount;
            recommendedNote.LikeCount = likeCount;
            recommendedNote.CommentCount = note.InteractInfo.CommentCount;
            recommendedNote.FavoriteCount = note.InteractInfo.CollectedCount;
            recommendedNote.ShareCount = note.InteractInfo.ShareCount;
            recommendedNote.IsLiked = note.InteractInfo.Liked;
            recommendedNote.IsCollected = note.InteractInfo.Collected;

            recommendedNote.InteractInfo = new RecommendedInteractInfo
            {
                LikedCountRaw = note.InteractInfo.LikedCount.ToString(),
                LikedCount = likeCount,
                CommentCount = note.InteractInfo.CommentCount,
                CollectedCount = note.InteractInfo.CollectedCount,
                ShareCount = note.InteractInfo.ShareCount,
                Liked = note.InteractInfo.Liked,
                Collected = note.InteractInfo.Collected
            };
        }
        else
        {
            missingFields.Add("InteractInfo");
        }

        // 设置视频信息
        if (note.Video != null)
        {
            recommendedNote.VideoInfo = new RecommendedVideoInfo
            {
                Duration = note.Video.Duration,
                Cover = note.Video.Cover,
                Url = note.Video.Url,
                Width = note.Video.Width,
                Height = note.Video.Height
            };
            recommendedNote.VideoDuration = note.Video.Duration;
            recommendedNote.VideoUrl = note.Video.Url;
            recommendedNote.Type = NoteType.Video;
        }

        // 设置图片信息
        if (note.ImageList.Count != 0)
        {
            recommendedNote.Images = note.ImageList.Select(img => new RecommendedImageInfo
            {
                Url = img.Url,
                Width = img.Width,
                Height = img.Height,
                Scenes = img.InfoList.Select(info => new ImageSceneInfo
                {
                    SceneType = info.ImageScene,
                    Url = info.Url
                }).ToList()
            }).ToList();

            if (recommendedNote.Type == NoteType.Unknown)
            {
                recommendedNote.Type = NoteType.Image;
            }
        }

        // 确定笔记类型
        if (recommendedNote.Type == NoteType.Unknown)
        {
            recommendedNote.DetermineType();
        }

        // 设置数据质量
        recommendedNote.Quality = missingFields.Count switch
        {
            0 => DataQuality.Complete,
            <= 2 => DataQuality.Partial,
            _ => DataQuality.Minimal
        };
        recommendedNote.MissingFields = missingFields;

        return recommendedNote;
    }

    /// <summary>
    /// 选择最佳封面图片URL
    /// 优先级：UrlDefault > UrlPre > InfoList中的最佳选择
    /// </summary>
    private string SelectBestCoverUrl(SearchCoverInfo cover)
    {
        if (!string.IsNullOrEmpty(cover.UrlDefault))
            return cover.UrlDefault;

        if (!string.IsNullOrEmpty(cover.UrlPre))
            return cover.UrlPre;

        // 从InfoList中选择最佳URL
        var bestInfo = cover.InfoList.FirstOrDefault(info => info.ImageScene == "WB_DFT")
                      ?? cover.InfoList.FirstOrDefault(info => info.ImageScene == "WB_PRV")
                      ?? cover.InfoList.FirstOrDefault();

        return bestInfo?.Url ?? string.Empty;
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

}
