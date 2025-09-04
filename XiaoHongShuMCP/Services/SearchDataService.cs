using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using NPOI.XSSF.UserModel;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 搜索数据服务
/// 集成搜索、统计分析、异步导出功能的统一服务
/// 支持同步统计计算和异步导出，提供完整的搜索数据管理功能
/// 实现页面状态检测和智能导航逻辑，避免不必要的页面导航
/// </summary>
public class SearchDataService : ISearchDataService
{
    private readonly ILogger<SearchDataService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IBrowserManager _browserManager;
    private readonly IAccountManager _accountManager;
    private readonly ISelectorManager _selectorManager;
    private readonly IHumanizedInteractionService _humanizedInteraction;

    public SearchDataService(
        ILogger<SearchDataService> logger,
        IConfiguration configuration,
        IBrowserManager browserManager,
        IAccountManager accountManager,
        ISelectorManager selectorManager,
        IHumanizedInteractionService humanizedInteraction)
    {
        _logger = logger;
        _configuration = configuration;
        _browserManager = browserManager;
        _accountManager = accountManager;
        _selectorManager = selectorManager;
        _humanizedInteraction = humanizedInteraction;
    }

    /// <summary>
    /// 执行搜索并包含统计分析
    /// 集成搜索、统计计算和可选的异步导出功能
    /// </summary>
    public async Task<OperationResult<SearchResult>> SearchWithAnalyticsAsync(SearchRequest request)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // 检查是否已登录
            if (!await _accountManager.IsLoggedInAsync())
            {
                return OperationResult<SearchResult>.Fail("用户未登录，请先登录", ErrorType.LoginRequired, "NOT_LOGGED_IN");
            }

            _logger.LogInformation("开始智能搜索: 关键词={Keyword}, 最大结果数={MaxResults}",
                request.Keyword, request.MaxResults);

            // 1. 执行智能搜索
            var searchResult = await ExecuteSmartSearchAsync(request);
            if (!searchResult.Success || searchResult.Data == null)
            {
                return OperationResult<SearchResult>.Fail(
                    searchResult.ErrorMessage ?? "搜索失败",
                    ErrorType.NetworkError, "SEARCH_FAILED");
            }

            var notes = searchResult.Data;
            var duration = DateTime.UtcNow - startTime;

            // 2. 同步计算统计数据（零额外成本）
            var statistics = CalculateStatisticsSync(notes);

            // 3. 构建增强搜索结果
            var enhancedResult = new SearchResult(
                Notes: notes,
                TotalCount: notes.Count,
                SearchKeyword: request.Keyword,
                Duration: duration,
                Statistics: statistics,
                ExportInfo: null // 初始为null，异步导出完成后可更新
            );

            // 4. 立即返回给客户端（不等待导出）
            var result = OperationResult<SearchResult>.Ok(enhancedResult);

            // 5. 异步启动导出任务（如果启用）
            if (request.AutoExport && notes.Count > 0)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        var fileName = request.ExportFileName ??
                                       $"search_{request.Keyword}_{DateTime.Now:yyyyMMdd_HHmmss}";
                        var exportResult = ExportNotesAsync(notes, fileName, request.ExportOptions);

                        if (exportResult.Success)
                        {
                            _logger.LogInformation("搜索结果自动导出完成: {FilePath}",
                                exportResult.Data?.FilePath);
                        }
                        else
                        {
                            _logger.LogWarning("自动导出失败: {Error}", exportResult.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "异步导出任务异常，不影响主功能");
                    }
                });
            }

            _logger.LogInformation("智能搜索完成: 关键词={Keyword}, 找到={Count}条结果, 耗时={Duration}ms",
                request.Keyword, notes.Count, duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "智能搜索失败: 关键词={Keyword}", request.Keyword);
            return OperationResult<SearchResult>.Fail(
                $"搜索失败: {ex.Message}", ErrorType.NetworkError, "SEARCH_EXCEPTION");
        }
    }

    /// <summary>
    /// 计算搜索统计信息
    /// 基于笔记列表生成详细的统计分析
    /// </summary>
    public Task<OperationResult<SearchStatistics>> CalculateSearchStatisticsAsync(List<NoteInfo> notes)
    {
        try
        {
            var statistics = CalculateStatisticsSync(notes);
            return Task.FromResult(OperationResult<SearchStatistics>.Ok(statistics));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算搜索统计数据失败");
            return Task.FromResult(OperationResult<SearchStatistics>.Fail($"统计计算失败: {ex.Message}", ErrorType.Unknown));
        }
    }

    /// <summary>
    /// 异步导出笔记数据
    /// 支持自定义导出选项和格式
    /// </summary>
    public OperationResult<SimpleExportInfo> ExportNotesAsync(List<NoteInfo> notes, string fileName, ExportOptions? options = null)
    {
        try
        {
            if (notes == null || notes.Count == 0)
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

            var exportSuccess = ExportToExcel(notes, filePath, options);

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
            else
            {
                var failedInfo = new SimpleExportInfo(
                    FilePath: string.Empty,
                    FileName: fullFileName,
                    ExportedAt: DateTime.UtcNow,
                    Success: false
                );

                return OperationResult<SimpleExportInfo>.Fail("导出文件创建失败", ErrorType.FileOperation, "EXPORT_FILE_FAILED");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据导出异常");
            var errorInfo = new SimpleExportInfo(
                FilePath: string.Empty,
                FileName: fileName,
                ExportedAt: DateTime.UtcNow,
                Success: false
            );

            return OperationResult<SimpleExportInfo>.Fail($"导出异常: {ex.Message}", ErrorType.FileOperation, "EXPORT_EXCEPTION");
        }
    }

    #region 私有方法
    /// <summary>
    /// 执行智能搜索操作
    /// 实现页面状态感知和智能导航逻辑
    /// </summary>
    private async Task<OperationResult<List<NoteInfo>>> ExecuteSmartSearchAsync(SearchRequest request)
    {
        try
        {
            // 检查登录状态
            if (!await _browserManager.IsLoggedInAsync())
            {
                return OperationResult<List<NoteInfo>>.Fail(
                    "用户未登录，无法执行搜索",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }

            var context = await _browserManager.GetBrowserContextAsync();
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

            // 页面状态感知和智能导航
            var currentPageState = await _selectorManager.DetectPageStateAsync(page);
            _logger.LogInformation("检测到当前页面状态: {PageState}", currentPageState);

            List<NoteInfo> notes;

            if (currentPageState == PageState.SearchResult)
            {
                // 在搜索结果页面，执行原地搜索
                _logger.LogInformation("在搜索结果页面执行原地搜索");
                notes = await ExecuteInPlaceSearchAsync(page, request, currentPageState);
            }
            else
            {
                // 在探索页面或未知页面，执行标准搜索流程
                _logger.LogInformation("执行标准搜索流程，当前页面: {PageState}", currentPageState);
                notes = await ExecuteStandardSearchAsync(page, request);
            }

            return OperationResult<List<NoteInfo>>.Ok(notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "智能搜索失败: 关键词={Keyword}", request.Keyword);
            return OperationResult<List<NoteInfo>>.Fail(
                $"搜索失败: {ex.Message}",
                ErrorType.NetworkError,
                "PERFORM_SEARCH_FAILED");
        }
    }

    /// <summary>
    /// 执行原地搜索 - 在搜索结果页面直接修改搜索关键词
    /// </summary>
    private async Task<List<NoteInfo>> ExecuteInPlaceSearchAsync(IPage page, SearchRequest request, PageState pageState)
    {
        _logger.LogInformation("开始执行原地搜索: {Keyword}", request.Keyword);

        // 使用页面状态感知的选择器查找搜索框
        var searchBoxSelectors = _selectorManager.GetSelectors("SearchInput", pageState);
        IElementHandle? searchBox = null;

        foreach (var selector in searchBoxSelectors)
        {
            try
            {
                searchBox = await page.QuerySelectorAsync(selector);
                if (searchBox != null)
                {
                    _logger.LogDebug("使用选择器找到搜索框: {Selector}", selector);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("选择器 {Selector} 查找搜索框失败: {Error}", selector, ex.Message);
                continue;
            }
        }

        if (searchBox == null)
        {
            _logger.LogWarning("未找到搜索框，回退到标准搜索流程");
            return await ExecuteStandardSearchAsync(page, request);
        }

        // 清空并输入新的关键词
        await searchBox.FillAsync(""); // 使用FillAsync清空
        await _humanizedInteraction.HumanTypeAsync(page, "SearchInput", request.Keyword);
        await Task.Delay(1000);

        // 提交搜索
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() {Timeout = 10000});

        // 使用页面状态感知的选择器提取笔记信息
        return await ExecuteSearchOperationAsync(page, request, PageState.SearchResult);
    }

    /// <summary>
    /// 执行标准搜索流程 - 导航到探索页面后执行搜索
    /// </summary>
    private async Task<List<NoteInfo>> ExecuteStandardSearchAsync(IPage page, SearchRequest request)
    {
        _logger.LogInformation("开始执行标准搜索流程: {Keyword}", request.Keyword);

        // 导航到探索页面
        await page.GotoAsync("https://www.xiaohongshu.com/explore");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() {Timeout = 10000});

        // 执行搜索操作
        return await ExecuteSearchOperationAsync(page, request, PageState.Explore);
    }

    /// <summary>
    /// 执行搜索操作
    /// 支持页面状态感知的笔记提取
    /// </summary>
    private async Task<List<NoteInfo>> ExecuteSearchOperationAsync(IPage page, SearchRequest request, PageState pageState)
    {
        var notes = new List<NoteInfo>();

        // 如果页面状态是Explore，需要先执行搜索输入
        if (pageState == PageState.Explore)
        {
            var searchBox = await _humanizedInteraction.FindElementAsync(page, "SearchInput", pageState);

            if (searchBox == null)
            {
                throw new Exception("找不到搜索框");
            }

            // 清空搜索框并输入关键词
            await searchBox.FillAsync("");
            await _humanizedInteraction.HumanTypeAsync(page, "SearchInput", request.Keyword);
            await Task.Delay(1000);

            // 提交搜索
            await page.Keyboard.PressAsync("Enter");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() {Timeout = 10000});

            // 搜索后页面状态变为SearchResult
            pageState = PageState.SearchResult;
        }

        // 使用页面状态感知的选择器提取笔记信息
        var noteSelectors = _selectorManager.GetSelectors("NoteItem", pageState);

        // 提取笔记信息
        for (int i = 0; i < request.MaxResults && i < 50; i++)
        {
            try
            {
                IReadOnlyList<IElementHandle> noteElements = new List<IElementHandle>();

                // 尝试多个选择器
                foreach (var selector in noteSelectors)
                {
                    try
                    {
                        var elements = await page.QuerySelectorAllAsync(selector);
                        if (elements.Count > 0)
                        {
                            noteElements = elements;
                            _logger.LogDebug("使用选择器 {Selector} 找到 {Count} 个笔记元素", selector, elements.Count);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("选择器 {Selector} 查找笔记失败: {Error}", selector, ex.Message);
                        continue;
                    }
                }

                if (noteElements.Count == 0)
                {
                    _logger.LogWarning("未找到笔记元素，尝试通用选择器");
                    noteElements = await page.QuerySelectorAllAsync("[data-testid='note-item'], .note-item, .feed-item");
                }

                if (i >= noteElements.Count) break;

                var noteInfo = await ExtractNoteInfo(noteElements[i], i);
                if (noteInfo != null)
                {
                    notes.Add(noteInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "提取笔记信息失败: 索引={Index}", i);
                // 继续处理下一个
            }
        }

        _logger.LogInformation("页面状态 {PageState} 下提取到 {Count} 条笔记", pageState, notes.Count);
        return notes;
    }

    /// <summary>
    /// 提取单个笔记信息
    /// </summary>
    private async Task<NoteInfo?> ExtractNoteInfo(IElementHandle noteElement, int index)
    {
        try
        {
            var noteInfo = new NoteInfo();
            var missingFields = new List<string>();

            // 提取标题
            var titleElement = await noteElement.QuerySelectorAsync("a[title], .title, .note-title");
            if (titleElement != null)
            {
                noteInfo.Title = await titleElement.GetAttributeAsync("title") ??
                                 await titleElement.TextContentAsync() ?? "";
                noteInfo.Url = await titleElement.GetAttributeAsync("href") ?? "";
            }
            else
            {
                missingFields.Add("Title");
            }

            // 提取作者
            var authorElement = await noteElement.QuerySelectorAsync(".author, .user-name, [data-testid='author']");
            if (authorElement != null)
            {
                noteInfo.Author = await authorElement.TextContentAsync() ?? "";
            }
            else
            {
                missingFields.Add("Author");
            }

            // 提取封面图片
            var imgElement = await noteElement.QuerySelectorAsync("img");
            if (imgElement != null)
            {
                noteInfo.CoverImage = await imgElement.GetAttributeAsync("src") ?? "";
            }

            // 提取交互数据（点赞、评论等）
            var likeElement = await noteElement.QuerySelectorAsync("[data-testid='like-count'], .like-count, .heart-icon + span");
            if (likeElement != null)
            {
                var likeText = await likeElement.TextContentAsync() ?? "";
                if (int.TryParse(ExtractNumberFromText(likeText), out int likes))
                {
                    noteInfo.LikeCount = likes;
                }
            }
            else
            {
                missingFields.Add("LikeCount");
            }

            // 设置数据质量
            noteInfo.Quality = missingFields.Count == 0 ? DataQuality.Complete :
                missingFields.Count <= 2 ? DataQuality.Partial : DataQuality.Minimal;
            noteInfo.MissingFields = missingFields;
            noteInfo.Id = $"note_{index}_{DateTime.UtcNow.Ticks}";

            return noteInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析笔记元素失败");
            return null;
        }
    }

    /// <summary>
    /// 同步计算统计数据（零额外成本）
    /// </summary>
    private SearchStatistics CalculateStatisticsSync(List<NoteInfo> notes)
    {
        if (notes == null || notes.Count == 0)
        {
            return new SearchStatistics(0, 0, 0, 0, 0, DateTime.UtcNow);
        }

        var completeCount = notes.Count(n => n.Quality == DataQuality.Complete);
        var partialCount = notes.Count(n => n.Quality == DataQuality.Partial);
        var minimalCount = notes.Count(n => n.Quality == DataQuality.Minimal);

        // 计算平均值（仅对非空数据）
        var likeCounts = notes.Where(n => n.LikeCount.HasValue).Select(n => n.LikeCount!.Value).ToList();
        var commentCounts = notes.Where(n => n.CommentCount.HasValue).Select(n => n.CommentCount!.Value).ToList();

        var avgLikes = likeCounts.Any() ? likeCounts.Average() : 0;
        var avgComments = commentCounts.Any() ? commentCounts.Average() : 0;

        return new SearchStatistics(
            CompleteDataCount: completeCount,
            PartialDataCount: partialCount,
            MinimalDataCount: minimalCount,
            AverageLikes: avgLikes,
            AverageComments: avgComments,
            CalculatedAt: DateTime.UtcNow
        );
    }


    /// <summary>
    /// 导出为Excel文件 - 极简版
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
    /// 从文本中提取数字
    /// </summary>
    private string ExtractNumberFromText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "0";

        var match = Regex.Match(text, @"\d+");
        return match.Success ? match.Value : "0";
    }
    #endregion
}
