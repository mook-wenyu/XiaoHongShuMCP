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
    private readonly PlaywrightBrowserManager _browserManager;
    private readonly IAccountManager _accountManager;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly ISelectorManager _selectorManager;

    /// <summary>
    /// URL构建常量和默认参数
    /// </summary>
    private const string BASE_CREATOR_URL = "https://creator.xiaohongshu.com";
    private const string DEFAULT_SOURCE = "web_explore_feed";
    private const string DEFAULT_XSEC_SOURCE = "pc_search";

    public XiaoHongShuService(
        ILogger<XiaoHongShuService> logger,
        PlaywrightBrowserManager browserManager,
        IAccountManager accountManager,
        IHumanizedInteractionService humanizedInteraction,
        ISelectorManager selectorManager)
    {
        _logger = logger;
        _browserManager = browserManager;
        _accountManager = accountManager;
        _humanizedInteraction = humanizedInteraction;
        _selectorManager = selectorManager;
    }

    /// <summary>
    /// 基于关键词列表查找单个笔记详情
    /// 使用新的统一架构，通过LocateAndOperateNoteAsync方法定位笔记并获取详情
    /// </summary>
    /// <param name="keywords">搜索关键词列表（匹配任意关键词）</param>
    /// <param name="includeComments">是否包含评论</param>
    /// <returns>笔记详情操作结果</returns>
    public async Task<OperationResult<NoteDetail>> GetNoteDetailAsync(
        List<string> keywords,
        bool includeComments = false)
    {
        _logger.LogInformation("基于关键词列表查找笔记详情: 关键词={Keywords}, 包含评论={IncludeComments}",
            string.Join(", ", keywords), includeComments);

        // 创建获取笔记详情的操作函数
        return await LocateAndOperateNoteAsync(keywords, async (noteElement) =>
        {
            try
            {
                var page = await _browserManager.GetPageAsync();

                // 等待模态窗口打开
                _logger.LogDebug("等待笔记详情模态窗口打开");
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ModalWaiting);

                if (!await WaitForModalOpenAsync(8000))
                {
                    return OperationResult<NoteDetail>.Fail(
                        "模态窗口打开超时",
                        ErrorType.BrowserError,
                        "MODAL_TIMEOUT");
                }

                // 从模态窗口提取完整的笔记详情
                _logger.LogDebug("开始提取笔记详情数据");
                var noteDetail = await ExtractNoteDetailFromModal(includeComments);

                if (noteDetail == null)
                {
                    await CloseModalSafely();
                    return OperationResult<NoteDetail>.Fail(
                        "无法提取笔记详情数据",
                        ErrorType.ElementNotFound,
                        "EXTRACTION_FAILED");
                }

                // 自动识别笔记类型
                noteDetail.DetermineType();

                // 安全关闭模态窗口
                if (!await CloseModalSafely())
                {
                    _logger.LogWarning("模态窗口关闭可能有问题，但已获取数据");
                }

                // 拟人化延时
                await _humanizedInteraction.HumanBetweenActionsDelayAsync();

                _logger.LogInformation("成功获取笔记详情: 关键词={Keywords}, 标题={Title}, 类型={Type}, 质量={Quality}",
                    string.Join(", ", keywords), noteDetail.Title, noteDetail.Type, noteDetail.Quality);

                return OperationResult<NoteDetail>.Ok(noteDetail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取笔记详情操作失败: 关键词={Keywords}", string.Join(", ", keywords));

                // 确保模态窗口关闭
                try
                {
                    await CloseModalSafely();
                }
                catch (Exception closeEx)
                {
                    _logger.LogWarning(closeEx, "关闭模态窗口时发生异常");
                }

                return OperationResult<NoteDetail>.Fail(
                    $"获取笔记详情失败: {ex.Message}",
                    ErrorType.BrowserError,
                    "DETAIL_EXTRACTION_ERROR");
            }
        });
    }

    /// <summary>
    /// 批量查找笔记详情（重构版） - 三位一体功能实现
    /// 基于简单关键词列表批量获取笔记详情，集成统计分析和异步导出功能
    /// 参考 SearchDataService 模式，提供同步统计计算和可选的异步导出
    /// </summary>
    /// <param name="keywords">关键词列表（简化参数）</param>
    /// <param name="maxCount">最大查找数量</param>
    /// <param name="includeComments">是否包含评论数据</param>
    /// <param name="autoExport">是否自动导出到Excel</param>
    /// <param name="exportFileName">导出文件名（可选）</param>
    /// <returns>增强的批量笔记结果，包含统计分析和导出信息</returns>
    public async Task<OperationResult<EnhancedBatchNoteResult>> BatchGetNoteDetailsAsync(
        List<string> keywords,
        int maxCount = 10,
        bool includeComments = false,
        bool autoExport = true,
        string? exportFileName = null)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("开始批量获取笔记详情（重构版）: 关键词={Keywords}, 最大数量={MaxCount}, 自动导出={AutoExport}",
            string.Join(", ", keywords), maxCount, autoExport);

        try
        {
            // 1. 参数验证
            if (!keywords.Any() || keywords.All(string.IsNullOrWhiteSpace))
            {
                return OperationResult<EnhancedBatchNoteResult>.Fail(
                    "关键词列表不能为空",
                    ErrorType.ValidationError,
                    "EMPTY_KEYWORDS");
            }

            // 2. 检查用户登录状态
            if (!await _accountManager.IsLoggedInAsync())
            {
                return OperationResult<EnhancedBatchNoteResult>.Fail(
                    "用户未登录，请先登录",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }

            // 3. 执行批量获取笔记详情
            var noteDetails = new List<NoteDetail>();
            var failedNotes = new List<(string, string)>();
            var processingStats = new Dictionary<ProcessingMode, int>
            {
                [ProcessingMode.Fast] = 0,
                [ProcessingMode.Standard] = 0,
                [ProcessingMode.Careful] = 0
            };

            var processedCount = 0;
            var individualProcessingTimes = new List<double>();

            // 使用现有的虚拟化列表搜索找到匹配的笔记
            var matchingNotesResult = await FindVisibleMatchingNotesAsync(keywords, maxCount);
            if (!matchingNotesResult.Success || !matchingNotesResult.Data?.Any() == true)
            {
                _logger.LogWarning("未找到匹配的笔记: {Error}", matchingNotesResult.ErrorMessage);

                // 返回空结果但不失败
                var emptyResult = CreateEmptyBatchResult(startTime, keywords.First());
                return OperationResult<EnhancedBatchNoteResult>.Ok(emptyResult);
            }

            var noteElements = matchingNotesResult.Data!;
            _logger.LogInformation("找到 {Count} 个匹配的笔记元素，开始批量处理", noteElements.Count);

            // 4. 逐个处理笔记元素
            foreach (var noteElement in noteElements.Take(maxCount))
            {
                var noteProcessingStart = DateTime.UtcNow;

                try
                {
                    _logger.LogDebug("处理第 {Current}/{Total} 个笔记", processedCount + 1, Math.Min(noteElements.Count, maxCount));

                    // 使用统一架构获取笔记详情 - 直接操作已找到的元素
                    var detailResult = await GetNoteDetailFromElementAsync(noteElement, includeComments);

                    if (detailResult is {Success: true, Data: not null})
                    {
                        noteDetails.Add(detailResult.Data);

                        // 确定处理模式
                        var mode = DetermineProcessingMode(processedCount, detailResult.Data);
                        processingStats[mode]++;

                        _logger.LogDebug("成功处理笔记: 标题={Title}, 模式={Mode}",
                            detailResult.Data.Title, mode);
                    }
                    else
                    {
                        var keyword = string.Join(", ", keywords);
                        var errorMsg = detailResult.ErrorMessage ?? "未知错误";
                        failedNotes.Add((keyword, errorMsg));
                        _logger.LogWarning("笔记处理失败: {Error}", errorMsg);
                    }

                    processedCount++;

                    // 记录单个处理时间
                    var processingTime = (DateTime.UtcNow - noteProcessingStart).TotalMilliseconds;
                    individualProcessingTimes.Add(processingTime);

                    // 批量操作间的拟人化延时
                    if (processedCount < maxCount && processedCount < noteElements.Count)
                    {
                        await _humanizedInteraction.HumanBetweenActionsDelayAsync();
                    }
                }
                catch (Exception ex)
                {
                    var keyword = string.Join(", ", keywords);
                    failedNotes.Add((keyword, $"处理异常: {ex.Message}"));
                    _logger.LogError(ex, "处理笔记时发生异常");
                    processedCount++;

                    // 记录失败的处理时间
                    var processingTime = (DateTime.UtcNow - noteProcessingStart).TotalMilliseconds;
                    individualProcessingTimes.Add(processingTime);
                }
            }

            var totalProcessingTime = DateTime.UtcNow - startTime;

            // 5. 同步计算统计数据（零额外成本）
            var statistics = CalculateBatchStatisticsSync(noteDetails, processingStats, individualProcessingTimes);

            // 6. 确定整体数据质量
            var overallQuality = DetermineOverallQuality(noteDetails);

            // 7. 构建增强结果（不等待导出）
            var enhancedResult = new EnhancedBatchNoteResult(
                NoteDetails: noteDetails,
                FailedNotes: failedNotes,
                TotalProcessed: processedCount,
                ProcessingTime: totalProcessingTime,
                OverallQuality: overallQuality,
                Statistics: statistics,
                ExportInfo: null // 初始为null，异步导出完成后可通过其他方式获取
            );

            // 8. 立即返回给客户端（不等待导出）
            var result = OperationResult<EnhancedBatchNoteResult>.Ok(enhancedResult);

            // 9. 异步启动导出任务（如果启用）
            if (autoExport && noteDetails.Count > 0)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        var fileName = exportFileName ??
                                       $"batch_notes_{DateTime.Now:yyyyMMdd_HHmmss}";

                        // 将NoteDetail转换为NoteInfo以兼容导出方法
                        var noteInfoList = noteDetails.Cast<NoteInfo>().ToList();

                        var exportResult = ExportNotesSync(noteInfoList, fileName);

                        if (exportResult.Success)
                        {
                            _logger.LogInformation("批量笔记结果自动导出完成: {FilePath}",
                                exportResult.Data?.FilePath);
                        }
                        else
                        {
                            _logger.LogWarning("批量结果自动导出失败: {Error}", exportResult.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "异步导出任务异常，不影响主功能");
                    }
                });
            }

            _logger.LogInformation("批量笔记详情获取完成: 成功={Success}, 失败={Failed}, 耗时={Duration}ms, 平均处理时间={AvgTime:F2}ms",
                noteDetails.Count, failedNotes.Count, totalProcessingTime.TotalMilliseconds, statistics.AverageProcessingTime);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量获取笔记详情异常");
            return OperationResult<EnhancedBatchNoteResult>.Fail(
                $"批量获取失败: {ex.Message}",
                ErrorType.BrowserError,
                "BATCH_GET_NOTES_FAILED");
        }
    }

    /// <summary>
    /// 从笔记元素直接获取详情 - 批量处理专用方法
    /// 避免重复搜索，直接从已找到的元素提取详情
    /// </summary>
    private async Task<OperationResult<NoteDetail>> GetNoteDetailFromElementAsync(IElementHandle noteElement, bool includeComments)
    {
        try
        {
            var page = await _browserManager.GetPageAsync();

            // 点击笔记元素打开详情
            await _humanizedInteraction.HumanClickAsync(page, noteElement);

            // 等待模态窗口打开
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ModalWaiting);

            if (!await WaitForModalOpenAsync(8000))
            {
                return OperationResult<NoteDetail>.Fail(
                    "模态窗口打开超时",
                    ErrorType.BrowserError,
                    "MODAL_TIMEOUT");
            }

            // 从模态窗口提取笔记详情
            var noteDetail = await ExtractNoteDetailFromModal(includeComments);

            if (noteDetail == null)
            {
                await CloseModalSafely();
                return OperationResult<NoteDetail>.Fail(
                    "无法提取笔记详情数据",
                    ErrorType.ElementNotFound,
                    "EXTRACTION_FAILED");
            }

            // 自动识别笔记类型
            noteDetail.DetermineType();

            // 安全关闭模态窗口
            await CloseModalSafely();

            return OperationResult<NoteDetail>.Ok(noteDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从元素获取笔记详情失败");

            // 确保模态窗口关闭
            try
            {
                await CloseModalSafely();
            }
            catch (Exception closeEx)
            {
                _logger.LogWarning(closeEx, "关闭模态窗口时发生异常");
            }

            return OperationResult<NoteDetail>.Fail(
                $"获取笔记详情失败: {ex.Message}",
                ErrorType.BrowserError,
                "ELEMENT_DETAIL_EXTRACTION_ERROR");
        }
    }

    /// <summary>
    /// 同步计算批量处理统计数据（零额外成本）
    /// 参考 SearchDataService 的统计计算模式
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

        var avgLikes = likeCounts.Any() ? likeCounts.Average() : 0;
        var avgComments = commentCounts.Any() ? commentCounts.Average() : 0;

        // 笔记类型分布统计
        var typeDistribution = noteDetails
            .GroupBy(n => n.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        // 平均处理时间
        var avgProcessingTime = individualProcessingTimes.Any() ? individualProcessingTimes.Average() : 0;

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
    private EnhancedBatchNoteResult CreateEmptyBatchResult(DateTime startTime, string keyword)
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

        return new EnhancedBatchNoteResult(
            NoteDetails: new List<NoteDetail>(),
            FailedNotes: new List<(string, string)> {(keyword, "未找到匹配的笔记")},
            TotalProcessed: 0,
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
            if (notes == null || notes.Count == 0)
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
            else
            {
                var failedInfo = new SimpleExportInfo(
                    FilePath: string.Empty,
                    FileName: fullFileName,
                    ExportedAt: DateTime.UtcNow,
                    Success: false
                );

                return OperationResult<SimpleExportInfo>.Fail(
                    "导出文件创建失败",
                    ErrorType.FileOperation,
                    "EXPORT_FILE_FAILED");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步导出异常");
            var errorInfo = new SimpleExportInfo(
                FilePath: string.Empty,
                FileName: fileName,
                ExportedAt: DateTime.UtcNow,
                Success: false
            );

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
        if (!keywords.Any() || keywords.All(string.IsNullOrWhiteSpace))
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
        return await LocateAndOperateNoteAsync(keywords, async (noteElement) =>
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
            var engageBarActiveSelectors = _selectorManager.GetSelectors("EngageBarActive");
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
            var commentButtonSelectors = _selectorManager.GetSelectors("DetailPageCommentButton");

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
                        var activeElement = await page.QuerySelectorAsync(engageBarActiveSelectors.First());
                        if (activeElement != null)
                        {
                            _logger.LogDebug("评论区域激活成功: {Selector}", selector);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("使用选择器 {Selector} 激活评论区域失败: {Error}", selector, ex.Message);
                    continue;
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

            var commentInputReadySelectors = _selectorManager.GetSelectors("CommentInputReady");
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
                        continue;
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
            var inputReadySelectors = _selectorManager.GetSelectors("CommentInputReady");
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
            var emojiTriggerSelectors = _selectorManager.GetSelectors("EmojiTriggerButton");
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
                    continue;
                }
            }

            if (!triggerClicked)
            {
                _logger.LogWarning("无法点击表情触发按钮，跳过表情添加");
                return;
            }

            // 2. 从最近使用的表情中选择
            var emojiClickAreaSelectors = _selectorManager.GetSelectors("EmojiClickArea");
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

            var enabledSelectors = _selectorManager.GetSelectors("CommentSubmitEnabled");
            var disabledSelectors = _selectorManager.GetSelectors("CommentSubmitDisabled");
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
            var engageBarActiveSelectors = _selectorManager.GetSelectors("EngageBarActive");
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
            var commentInputReadySelectors = _selectorManager.GetSelectors("CommentInputReady");
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
            var submitEnabledSelectors = _selectorManager.GetSelectors("CommentSubmitEnabled");
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
            await SimulateCreatorWorkflowAsync(page, title, content, tags ?? new List<string>());

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
            if (!keywords.Any() || keywords.All(string.IsNullOrWhiteSpace))
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

            if (!matchingNotesResult.Data?.Any() == true)
            {
                _logger.LogInformation("未找到匹配关键词的笔记: {Keywords}", string.Join(", ", keywords));
                return OperationResult<T>.Fail(
                    $"未找到匹配关键词的笔记: {string.Join(", ", keywords)}",
                    ErrorType.ElementNotFound,
                    "NO_MATCHING_NOTES");
            }

            var targetNoteElement = matchingNotesResult.Data.First();
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

    /// <summary>
    /// 示例：点赞笔记操作 - 展示如何使用 LocateAndOperateNoteAsync 方法
    /// 这是一个使用新架构核心方法的示例实现
    /// </summary>
    /// <param name="keywords">用于定位笔记的关键词列表</param>
    /// <returns>点赞操作结果</returns>
    /// <example>
    /// // 使用示例：
    /// var result = await LikeNoteAsync(new List&lt;string&gt; { "美食推荐", "火锅" });
    /// if (result.Success)
    /// {
    ///     var interaction = result.Data;
    ///     Console.WriteLine($"点赞成功: {interaction.CurrentState}");
    /// }
    /// </example>
    private async Task<OperationResult<InteractionResult>> LikeNoteAsync(List<string> keywords)
    {
        return await LocateAndOperateNoteAsync(keywords, async (noteElement) =>
        {
            try
            {
                var page = await _browserManager.GetPageAsync();

                // 查找点赞按钮
                var likeButtonSelectors = _selectorManager.GetSelectors("DetailPageLikeButton");
                IElementHandle? likeButton = null;
                string previousState = "未点赞";

                foreach (var selector in likeButtonSelectors)
                {
                    try
                    {
                        likeButton = await page.QuerySelectorAsync(selector);
                        if (likeButton != null)
                        {
                            // 检查当前状态
                            var isLiked = await likeButton.GetAttributeAsync("class");
                            if (isLiked?.Contains("liked") == true || isLiked?.Contains("active") == true)
                            {
                                previousState = "已点赞";
                            }
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("点赞按钮选择器 {Selector} 失败: {Error}", selector, ex.Message);
                        continue;
                    }
                }

                if (likeButton == null)
                {
                    return OperationResult<InteractionResult>.Fail(
                        "未找到点赞按钮",
                        ErrorType.ElementNotFound,
                        "LIKE_BUTTON_NOT_FOUND");
                }

                // 执行拟人化点击
                await _humanizedInteraction.HumanClickAsync(page, likeButton);
                await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ReviewPause);

                // 检查操作后状态
                var currentState = previousState == "已点赞" ? "取消点赞" : "已点赞";

                var result = new InteractionResult(
                    Success: true,
                    Action: "like",
                    PreviousState: previousState,
                    CurrentState: currentState,
                    Message: $"点赞操作成功: {previousState} -> {currentState}",
                    ErrorCode: null
                );

                return OperationResult<InteractionResult>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "点赞操作异常");
                var errorResult = new InteractionResult(
                    Success: false,
                    Action: "like",
                    PreviousState: "未知",
                    CurrentState: "未知",
                    Message: $"点赞操作失败: {ex.Message}",
                    ErrorCode: "LIKE_OPERATION_ERROR"
                );

                return OperationResult<InteractionResult>.Fail(
                    $"点赞操作失败: {ex.Message}",
                    ErrorType.BrowserError,
                    "LIKE_OPERATION_ERROR");
            }
        });
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
                NoteType.Image => ProcessingMode.Fast,      // 图文：快速处理（数量最多）
                NoteType.Video => ProcessingMode.Standard,  // 视频：标准处理（需要加载视频）
                NoteType.Article => ProcessingMode.Careful, // 长文：谨慎处理（内容复杂）
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
    /// 尝试解析时间文本
    /// </summary>
    private bool TryParseTime(string timeText, out DateTime publishTime)
    {
        publishTime = default;

        if (string.IsNullOrWhiteSpace(timeText))
            return false;

        // 处理相对时间
        if (timeText.Contains("分钟前"))
        {
            if (ExtractNumber(timeText, out int minutes))
            {
                publishTime = DateTime.UtcNow.AddMinutes(-minutes);
                return true;
            }
        }

        if (timeText.Contains("小时前"))
        {
            if (ExtractNumber(timeText, out int hours))
            {
                publishTime = DateTime.UtcNow.AddHours(-hours);
                return true;
            }
        }

        if (timeText.Contains("天前"))
        {
            if (ExtractNumber(timeText, out int days))
            {
                publishTime = DateTime.UtcNow.AddDays(-days);
                return true;
            }
        }

        // 处理绝对时间
        return DateTime.TryParse(timeText, out publishTime);
    }

    /// <summary>
    /// 从文本中提取数字
    /// </summary>
    private bool ExtractNumber(string text, out int number)
    {
        number = 0;
        var match = System.Text.RegularExpressions.Regex.Match(text, @"\d+");
        return match.Success && int.TryParse(match.Value, out number);
    }

    /// <summary>
    /// 确定数据质量
    /// </summary>
    private DataQuality DetermineDataQuality(NoteInfo note)
    {
        var hasBasicInfo = !string.IsNullOrEmpty(note.Title) && !string.IsNullOrEmpty(note.Author);
        var hasStats = note is {LikeCount: not null, CommentCount: not null};
        var hasTime = note.PublishTime.HasValue;

        if (hasBasicInfo && hasStats && hasTime)
            return DataQuality.Complete;
        if (hasBasicInfo && (hasStats || hasTime))
            return DataQuality.Partial;

        return DataQuality.Minimal;
    }

    /// <summary>
    /// 确定评论数据质量
    /// </summary>
    private DataQuality DetermineCommentDataQuality(CommentInfo comment)
    {
        var hasBasicInfo = !string.IsNullOrEmpty(comment.Author) && !string.IsNullOrEmpty(comment.Content);
        var hasStats = comment.LikeCount.HasValue;
        var hasTime = comment.PublishTime.HasValue;

        if (hasBasicInfo && hasStats && hasTime)
            return DataQuality.Complete;
        if (hasBasicInfo && (hasStats || hasTime))
            return DataQuality.Partial;

        return DataQuality.Minimal;
    }

    /// <summary>
    /// 从BRL中提取笔记ID
    /// 支持explore和search_result两种格式
    /// </summary>
    /// <param name="url">笔记URL</param>
    /// <returns>笔记ID或null</returns>
    private string? ExtractNoteIdFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        try
        {
            _logger.LogDebug("从URL提取笔记ID: {Url}", url);

            // 支持的URL模式（按优先级排序）
            var patterns = new[]
            {
                @"/explore/([a-f0-9]{24})",          // 探索页面格式（最常见）
                @"/search_result/([a-f0-9]{24})",    // 搜索结果页面格式
                @"/explore/([a-f0-9]{20,32})",       // 探索页面变长格式
                @"/search_result/([a-f0-9]{20,32})", // 搜索结果变长格式
                @"/([a-f0-9]{24})",                  // 通用格式
                @"[?&].*?([a-f0-9]{24})",            // URL参数中的ID
                @"([a-f0-9]{24})"                    // 任意位置的ID
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(url, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match is {Success: true, Groups.Count: > 1})
                {
                    var noteId = match.Groups[1].Value;
                    if (IsValidNoteId(noteId))
                    {
                        _logger.LogDebug("成功提取笔记ID: {NoteId} (使用模式: {Pattern})", noteId, pattern);
                        return noteId;
                    }
                }
            }

            _logger.LogDebug("无法从 URL 提取有效笔记ID: {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "从URL提取笔记ID失败: {Url}", url);
            return null;
        }
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
            NoteType.Article => !hasImages && !hasVideo, // 长文笔记只需要文本内容
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
            var uploadSelectors = _selectorManager.GetSelectors("ImageUploadArea");

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
            var fileInputSelectors = _selectorManager.GetSelectors("FileInput");
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
            List<string> filesToUpload = new();

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

            if (!filesToUpload.Any())
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
            var progressSelectors = _selectorManager.GetSelectors("UploadProgress");
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
            if (tags.Any())
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
            var segments = content.Split(new[] {'。', '\n', '！', '？'}, StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (!segments.Any())
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
        if (!tags.Any()) return;

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
    /// 验证笔记ID的有效性
    /// </summary>
    private bool IsValidNoteId(string noteId)
    {
        if (string.IsNullOrEmpty(noteId))
            return false;

        // 小红书笔记ID特征：20-32位十六进制字符
        if (noteId.Length is < 20 or > 32)
            return false;

        // 检查是否为有效的十六进制字符串
        return System.Text.RegularExpressions.Regex.IsMatch(noteId, @"^[a-fA-F0-9]+$");
    }

    /// <summary>
    /// 使用选择器列表提取文本 - 通用辅助方法
    /// </summary>
    private async Task<string?> ExtractTextWithSelectorList(IElementHandle element, List<string> selectors)
    {
        foreach (var selector in selectors)
        {
            try
            {
                var targetElement = await element.QuerySelectorAsync(selector);
                if (targetElement != null)
                {
                    var text = await targetElement.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("选择器 {Selector} 提取文本失败: {Error}", selector, ex.Message);
                continue;
            }
        }

        return null;
    }

    /// <summary>
    /// 提取详情页面交互数据 - 专用于详情页面的精确数据
    /// </summary>
    private async Task ExtractDetailPageInteractionDataAsync(IPage page, NoteDetail detail)
    {
        try
        {
            // 提取点赞数
            var likeCountSelectors = _selectorManager.GetSelectors("DetailPageLikeCount");
            var likeCountText = await ExtractTextWithSelectorList(page, likeCountSelectors);
            if (!string.IsNullOrEmpty(likeCountText) && ExtractNumber(likeCountText, out int likeCount))
            {
                detail.LikeCount = likeCount;
            }

            // 提取收藏数
            var collectCountSelectors = _selectorManager.GetSelectors("DetailPageCollectCount");
            var collectCountText = await ExtractTextWithSelectorList(page, collectCountSelectors);
            if (!string.IsNullOrEmpty(collectCountText) && ExtractNumber(collectCountText, out int collectCount))
            {
                detail.FavoriteCount = collectCount;
            }

            // 提取评论数
            var commentCountSelectors = _selectorManager.GetSelectors("DetailPageCommentCount");
            var commentCountText = await ExtractTextWithSelectorList(page, commentCountSelectors);
            if (!string.IsNullOrEmpty(commentCountText) && ExtractNumber(commentCountText, out int commentCount))
            {
                detail.CommentCount = commentCount;
            }

            _logger.LogDebug("详情页交互数据提取: 点赞={Like}, 收藏={Collect}, 评论={Comment}",
                detail.LikeCount, detail.FavoriteCount, detail.CommentCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "提取详情页交互数据失败");
        }
    }

    /// <summary>
    /// 提取详情页面评论数据 - 专用于详情页面的评论结构
    /// </summary>
    private async Task<List<CommentInfo>> ExtractDetailPageCommentsAsync(IPage page)
    {
        var comments = new List<CommentInfo>();

        try
        {
            _logger.LogDebug("开始提取详情页面评论数据...");

            // 使用详情页评论专用选择器
            var commentItemSelectors = _selectorManager.GetSelectors("CommentItemDetail");
            var commentElements = new List<IElementHandle>();

            // 尝试找到评论容器
            foreach (var selector in commentItemSelectors)
            {
                try
                {
                    var elements = await page.QuerySelectorAllAsync(selector);
                    if (elements.Any())
                    {
                        commentElements.AddRange(elements);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("评论选择器 {Selector} 失败: {Error}", selector, ex.Message);
                    continue;
                }
            }

            if (!commentElements.Any())
            {
                _logger.LogDebug("未找到详情页评论元素");
                return comments;
            }

            // 提取每个评论
            foreach (var commentElement in commentElements.Take(20)) // 限制评论数量
            {
                try
                {
                    var comment = await ExtractSingleDetailPageCommentAsync(commentElement);
                    if (comment != null)
                    {
                        comments.Add(comment);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "提取单个详情页评论失败");
                    continue;
                }
            }

            _logger.LogInformation("详情页评论数据提取完成: {Count}条评论", comments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "提取详情页评论数据失败");
        }

        return comments;
    }

    /// <summary>
    /// 提取单个详情页评论 - 使用详情页专用选择器
    /// </summary>
    private async Task<CommentInfo?> ExtractSingleDetailPageCommentAsync(IElementHandle commentElement)
    {
        try
        {
            var comment = new CommentInfo
            {
                Id = Guid.NewGuid().ToString(),
                ExtractedAt = DateTime.UtcNow,
                Quality = DataQuality.Minimal
            };

            // 提取作者名（详情页专用）
            var authorSelectors = _selectorManager.GetSelectors("CommentAuthorDetail");
            comment.Author = await ExtractTextWithSelectorList(commentElement, authorSelectors) ?? "匿名用户";

            // 提取评论内容（详情页专用）
            var contentSelectors = _selectorManager.GetSelectors("CommentContentDetail");
            comment.Content = await ExtractTextWithSelectorList(commentElement, contentSelectors) ?? "";

            // 提取点赞数（详情页专用）
            var likeCountSelectors = _selectorManager.GetSelectors("CommentLikeCountDetail");
            var likeCountText = await ExtractTextWithSelectorList(commentElement, likeCountSelectors);
            if (!string.IsNullOrEmpty(likeCountText) && ExtractNumber(likeCountText, out int likeCount))
            {
                comment.LikeCount = likeCount;
            }

            // 提取评论时间
            var dateTimeSelectors = _selectorManager.GetSelectors("CommentDateTime");
            var dateTimeText = await ExtractTextWithSelectorList(commentElement, dateTimeSelectors);
            if (!string.IsNullOrEmpty(dateTimeText) && TryParseTime(dateTimeText, out DateTime publishTime))
            {
                comment.PublishTime = publishTime;
            }

            // 确定评论数据质量
            comment.Quality = DetermineCommentDataQuality(comment);

            return comment;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "提取单个详情页评论失败");
            return null;
        }
    }

    /// <summary>
    /// 页面通用文本提取方法 - 支持页面级别的选择器
    /// </summary>
    private async Task<string?> ExtractTextWithSelectorList(IPage page, List<string> selectors)
    {
        foreach (var selector in selectors)
        {
            try
            {
                var targetElement = await page.QuerySelectorAsync(selector);
                if (targetElement != null)
                {
                    var text = await targetElement.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("页面选择器 {Selector} 提取文本失败: {Error}", selector, ex.Message);
                continue;
            }
        }

        return null;
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
            var noteItemSelectors = _selectorManager.GetSelectors("NoteCard");
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

            if (!allNoteElements.Any())
            {
                return OperationResult<List<IElementHandle>>.Ok(new List<IElementHandle>());
            }

            // 如果没有关键词，返回所有可见元素
            if (!keywords.Any())
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
                    continue;
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
                    var matches = System.Text.RegularExpressions.Regex.Match(href, @"(?:explore|discovery/item)/([a-f0-9]{24})")
                                  ?? System.Text.RegularExpressions.Regex.Match(href, @"/([a-f0-9]{24})(?:\?|$)");
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
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(textContent.Trim()));
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
    /// 等待模态窗口打开
    /// 检测.note-detail-mask[note-id]元素的出现
    /// </summary>
    private async Task<bool> WaitForModalOpenAsync(int timeoutMs = 5000)
    {
        try
        {
            var page = await _browserManager.GetPageAsync();
            var modalSelectors = _selectorManager.GetSelectors("NoteDetailModal");

            _logger.LogDebug("等待模态窗口打开，超时时间: {Timeout}ms", timeoutMs);

            foreach (var selector in modalSelectors)
            {
                try
                {
                    await page.WaitForSelectorAsync(selector, new() {Timeout = timeoutMs, State = WaitForSelectorState.Visible});
                    _logger.LogDebug("模态窗口已打开，使用选择器: {Selector}", selector);
                    return true;
                }
                catch (TimeoutException)
                {
                    _logger.LogDebug("选择器 {Selector} 等待超时", selector);
                    continue;
                }
            }

            _logger.LogWarning("所有模态窗口选择器都等待超时");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "等待模态窗口打开异常");
            return false;
        }
    }

    /// <summary>
    /// 等待模态窗口关闭
    /// 检测.note-detail-mask[note-id]元素的隐藏
    /// </summary>
    private async Task<bool> WaitForModalCloseAsync(int timeoutMs = 5000)
    {
        try
        {
            var page = await _browserManager.GetPageAsync();
            var modalSelectors = _selectorManager.GetSelectors("NoteDetailModal");

            _logger.LogDebug("等待模态窗口关闭，超时时间: {Timeout}ms", timeoutMs);

            foreach (var selector in modalSelectors)
            {
                try
                {
                    await page.WaitForSelectorAsync(selector, new() {Timeout = timeoutMs, State = WaitForSelectorState.Hidden});
                    _logger.LogDebug("模态窗口已关闭，使用选择器: {Selector}", selector);
                    return true;
                }
                catch (TimeoutException)
                {
                    _logger.LogDebug("选择器 {Selector} 关闭等待超时", selector);
                    continue;
                }
            }

            _logger.LogWarning("所有模态窗口选择器关闭等待都超时");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "等待模态窗口关闭异常");
            return false;
        }
    }

    /// <summary>
    /// 从模态窗口提取笔记详情
    /// 基于真实的小红书详情页模态窗口结构
    /// </summary>
    private async Task<NoteDetail?> ExtractNoteDetailFromModal(bool includeComments = false)
    {
        try
        {
            var page = await _browserManager.GetPageAsync();
            var noteDetail = new NoteDetail();

            // 确认模态窗口已打开
            var modalElement = await _humanizedInteraction.FindElementAsync(page, "NoteDetailModal");
            if (modalElement == null)
            {
                _logger.LogWarning("无法找到模态窗口元素");
                return null;
            }

            // 提取笔记ID（从URL或元素属性）
            var currentUrl = page.Url;
            noteDetail.Id = ExtractNoteIdFromUrl(currentUrl) ?? string.Empty;
            noteDetail.Url = currentUrl;

            // 提取标题
            var titleSelectors = _selectorManager.GetSelectors("NoteDetailTitle");
            var titleText = await ExtractTextWithSelectorList(page, titleSelectors);
            if (!string.IsNullOrEmpty(titleText))
            {
                noteDetail.Title = titleText;
            }

            // 提取作者信息
            var authorSelectors = _selectorManager.GetSelectors("NoteDetailAuthor");
            var authorText = await ExtractTextWithSelectorList(page, authorSelectors);
            if (!string.IsNullOrEmpty(authorText))
            {
                noteDetail.Author = authorText;
            }

            // 提取内容描述
            var contentSelectors = _selectorManager.GetSelectors("NoteDetailContent");
            var contentText = await ExtractTextWithSelectorList(page, contentSelectors);
            if (!string.IsNullOrEmpty(contentText))
            {
                noteDetail.Content = contentText;
            }

            // 提取互动数据
            await ExtractDetailPageInteractionDataAsync(page, noteDetail);

            // 提取评论（如果需要）
            if (includeComments)
            {
                var comments = await ExtractDetailPageCommentsAsync(page);
                if (comments.Any())
                {
                    noteDetail.Comments = comments;
                }
            }

            // 设置数据质量 - 简化评估
            noteDetail.Quality = DetermineDataQuality(noteDetail);
            noteDetail.ExtractedAt = DateTime.UtcNow;

            _logger.LogDebug("从模态窗口成功提取笔记详情: ID={Id}, 标题={Title}",
                noteDetail.Id, noteDetail.Title);

            return noteDetail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从模态窗口提取笔记详情失败");
            return null;
        }
    }

    /// <summary>
    /// 安全关闭模态窗口
    /// 尝试多种方式关闭并验证关闭状态
    /// </summary>
    private async Task<bool> CloseModalSafely()
    {
        try
        {
            var page = await _browserManager.GetPageAsync();

            // 方法1: 使用关闭按钮
            var closeButtonSelectors = _selectorManager.GetSelectors("NoteDetailCloseButton");
            foreach (var selector in closeButtonSelectors)
            {
                try
                {
                    var closeButton = await page.QuerySelectorAsync(selector);
                    if (closeButton != null)
                    {
                        await _humanizedInteraction.HumanClickAsync(page, closeButton);

                        // 等待关闭完成
                        if (await WaitForModalCloseAsync(3000))
                        {
                            _logger.LogDebug("使用关闭按钮成功关闭模态窗口");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("关闭按钮点击失败: {Error}", ex.Message);
                }
            }

            // 方法2: 使用ESC键
            try
            {
                await page.Keyboard.PressAsync("Escape");
                if (await WaitForModalCloseAsync(2000))
                {
                    _logger.LogDebug("使用ESC键成功关闭模态窗口");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("ESC键关闭失败: {Error}", ex.Message);
            }

            // 方法3: 点击遮罩层（如果存在）
            try
            {
                var maskSelectors = new[] {".note-detail-mask", ".modal-mask", ".overlay"};
                foreach (var selector in maskSelectors)
                {
                    var mask = await page.QuerySelectorAsync(selector);
                    if (mask != null)
                    {
                        await mask.ClickAsync();
                        if (await WaitForModalCloseAsync(2000))
                        {
                            _logger.LogDebug("点击遮罩层成功关闭模态窗口");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("遮罩层点击失败: {Error}", ex.Message);
            }

            _logger.LogWarning("所有关闭模态窗口的方法都失败了");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "安全关闭模态窗口异常");
            return false;
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
        if (string.IsNullOrEmpty(text) || !keywords.Any()) return false;

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

        return await LocateAndOperateNoteAsync(keywords, async (noteElement) =>
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

        return await LocateAndOperateNoteAsync(keywords, async (noteElement) =>
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

}
