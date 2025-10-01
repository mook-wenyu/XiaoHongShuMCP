using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Notes;

/// <summary>
/// 中文：页面笔记采集服务实现，通过监听网络请求从 API 响应中提取笔记数据。
/// English: Implementation of page note capture service that extracts note data from API responses by intercepting network requests.
/// </summary>
public sealed class PageNoteCaptureService : IPageNoteCaptureService
{
    private readonly IBrowserAutomationService _browserService;
    private readonly ILogger<PageNoteCaptureService> _logger;

    private const string FeedApiPattern = "**/api/sns/web/v1/feed*";
    private const string SearchApiPattern = "**/api/sns/web/v1/search/notes*";

    public PageNoteCaptureService(
        IBrowserAutomationService browserService,
        ILogger<PageNoteCaptureService> logger)
    {
        _browserService = browserService ?? throw new ArgumentNullException(nameof(browserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PageNoteCaptureResult> CaptureAsync(PageNoteCaptureContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation("[PageNoteCaptureService] 开始采集当前页面笔记（API 监听模式） browserKey={BrowserKey} targetCount={TargetCount}",
            context.BrowserKey, context.TargetCount);

        // 1. 获取页面上下文
        var pageContext = await _browserService.EnsurePageContextAsync(context.BrowserKey, cancellationToken).ConfigureAwait(false);
        var page = pageContext.Page;

        // 2. 检测并处理模态窗口
        await CloseModalIfExistsAsync(page, cancellationToken).ConfigureAwait(false);

        // 3. 验证当前页面是否为笔记列表页
        var isListPage = await IsNoteListPageAsync(page, cancellationToken).ConfigureAwait(false);
        if (!isListPage)
        {
            var currentUrl = page.Url;
            _logger.LogWarning("[PageNoteCaptureService] 当前页面不是笔记列表页 url={Url}", currentUrl);
            throw new InvalidOperationException($"当前页面不是笔记列表页，无法采集笔记。当前 URL: {currentUrl}");
        }

        // 4. 设置网络监听器并采集笔记数据
        var notes = await CaptureNotesFromApiAsync(page, context.TargetCount, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[PageNoteCaptureService] 采集到 {Count} 条笔记", notes.Count);

        // 5. 导出为 CSV
        var csvPath = await ExportToCsvAsync(notes, context.OutputDirectory, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[PageNoteCaptureService] 采集完成 csvPath={CsvPath} count={Count}", csvPath, notes.Count);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pageUrl"] = page.Url,
            ["timestamp"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };

        return new PageNoteCaptureResult(csvPath, notes.Count, metadata);
    }

    /// <summary>
    /// 中文：检测并关闭模态窗口（如果存在）。
    /// English: Detects and closes modal window if exists.
    /// </summary>
    private async Task CloseModalIfExistsAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            // 方法1：尝试按 Escape 键关闭模态窗口
            await page.Keyboard.PressAsync("Escape").ConfigureAwait(false);
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("[PageNoteCaptureService] 已尝试按 Escape 键关闭可能存在的模态窗口");

            // 方法2：如果 URL 包含 /explore/{id}，说明可能在详情页，尝试返回
            var url = page.Url;
            if (url.Contains("/explore/") && url.Split('/').Length > 4)
            {
                // 检测是否有关闭按钮（常见 class: close, 图标等）
                var closeButton = page.Locator("button[aria-label*='关闭'], button[aria-label*='close'], [class*='close-btn'], [class*='closeBtn']").First;
                var closeButtonCount = await closeButton.CountAsync().ConfigureAwait(false);
                if (closeButtonCount > 0)
                {
                    await closeButton.ClickAsync().ConfigureAwait(false);
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("[PageNoteCaptureService] 检测到详情模态窗口并已关闭");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "[PageNoteCaptureService] 关闭模态窗口时出现异常（可能不存在模态窗口）");
        }
    }

    /// <summary>
    /// 中文：检测当前页面是否为笔记列表页。
    /// English: Detects if current page is a note list page.
    /// </summary>
    private async Task<bool> IsNoteListPageAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            var url = page.Url;

            // 检查 URL 特征
            if (!url.Contains("xiaohongshu.com"))
            {
                return false;
            }

            // 允许的列表页 URL 模式：
            // - https://www.xiaohongshu.com/explore（发现页）
            // - https://www.xiaohongshu.com/explore?channel_id=...（推荐频道等）
            // - https://www.xiaohongshu.com/search_result?...（搜索页）
            if (url.Contains("/explore") || url.Contains("/search"))
            {
                // 进一步验证：检查页面上是否存在笔记卡片链接
                var noteCardCount = await page.Locator("a[href*='/explore/']").CountAsync().ConfigureAwait(false);
                return noteCardCount > 0;
            }

            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[PageNoteCaptureService] 检测列表页时出现异常");
            return false;
        }
    }

    /// <summary>
    /// 中文：检测页面类型（发现页或搜索页）。
    /// English: Detects page type (Discovery or Search).
    /// </summary>
    private PageType DetectPageType(IPage page)
    {
        var url = page.Url;

        if (url.Contains("/explore"))
        {
            return PageType.Discovery;
        }

        if (url.Contains("/search"))
        {
            return PageType.Search;
        }

        throw new InvalidOperationException($"无法识别页面类型，当前 URL: {url}");
    }

    /// <summary>
    /// 中文：页面类型枚举。
    /// English: Page type enumeration.
    /// </summary>
    private enum PageType
    {
        /// <summary>发现页（homefeed）</summary>
        Discovery,
        /// <summary>搜索页（search）</summary>
        Search
    }

    /// <summary>
    /// 中文：两阶段采集笔记数据：先滚动监听列表 API 收集 note_id，再点击获取详情。
    /// English: Two-phase note capture: scroll to collect note_ids from list API, then click to get details.
    /// </summary>
    private async Task<List<PageNoteCard>> CaptureNotesFromApiAsync(IPage page, int targetCount, CancellationToken cancellationToken)
    {
        // 阶段1：检测页面类型并滚动监听列表 API 收集 note_id
        var pageType = DetectPageType(page);
        _logger.LogInformation("[PageNoteCaptureService] 检测到页面类型：{PageType}", pageType);

        var noteIds = await CollectNoteIdsFromListApiAsync(page, pageType, targetCount, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("[PageNoteCaptureService] 阶段1完成：从列表 API 收集到 {Count} 个 note_id", noteIds.Count);

        if (noteIds.Count == 0)
        {
            _logger.LogWarning("[PageNoteCaptureService] 未收集到任何 note_id");
            return new List<PageNoteCard>();
        }

        // 阶段2：根据 note_id 逐个点击获取详情
        var collectedNotes = await CollectDetailsByClickingAsync(page, noteIds, targetCount, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("[PageNoteCaptureService] 阶段2完成：点击获取到 {Count} 条详细笔记", collectedNotes.Count);

        return collectedNotes;
    }

    /// <summary>
    /// 中文：阶段1 - 滚动页面并监听列表 API，收集 note_id 列表。
    /// English: Phase 1 - Scroll page and monitor list API to collect note_id list.
    /// </summary>
    private async Task<List<string>> CollectNoteIdsFromListApiAsync(IPage page, PageType pageType, int targetCount, CancellationToken cancellationToken)
    {
        var noteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var apiEndpoint = pageType == PageType.Discovery
            ? "/api/sns/web/v1/homefeed"
            : "/api/sns/web/v1/search/notes";

        _logger.LogInformation("[PageNoteCaptureService] 开始监听列表 API：{Endpoint}", apiEndpoint);

        // 动态计算最大滚动次数：基于目标数量，假设每次滚动返回约 20 个笔记
        // 给予 3 倍缓冲以应对重复数据，并限制在合理范围 [30, 200]
        var baseAttempts = (int)Math.Ceiling(targetCount / 20.0);
        var maxScrollAttempts = Math.Clamp(baseAttempts * 3, 30, 200);
        var scrollAttempts = 0;
        var noNewDataCount = 0;

        _logger.LogInformation("[PageNoteCaptureService] 目标采集 {Target} 条，预计最多滚动 {MaxAttempts} 次",
            targetCount, maxScrollAttempts);

        while (noteIds.Count < targetCount && scrollAttempts < maxScrollAttempts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scrollAttempts++;

            try
            {
                // 设置 API 监听器
                var apiResponseTask = page.WaitForResponseAsync(
                    response => response.Url.Contains(apiEndpoint) && response.Status == 200,
                    new PageWaitForResponseOptions { Timeout = 10000 });

                // 拟人化滚动触发加载（鼠标滚轮 + 随机距离）
                await HumanizedScrollAsync(page, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("[PageNoteCaptureService] 滚动页面第 {Attempt} 次", scrollAttempts);

                // 等待响应并解析
                var response = await apiResponseTask.ConfigureAwait(false);
                var jsonText = await response.TextAsync().ConfigureAwait(false);
                var newIds = ExtractNoteIdsFromListResponse(jsonText, pageType);

                var previousCount = noteIds.Count;
                foreach (var id in newIds)
                {
                    noteIds.Add(id);
                }

                var addedCount = noteIds.Count - previousCount;
                _logger.LogInformation("[PageNoteCaptureService] 列表 API 返回 {NewCount} 个 note_id，已收集 {Total}/{Target}",
                    addedCount, noteIds.Count, targetCount);

                // 早停策略：已达目标或连续多次无新数据
                if (addedCount == 0)
                {
                    noNewDataCount++;
                    // 已达目标且连续5次无新数据，或未达目标但连续8次无新数据
                    var earlyStopThreshold = noteIds.Count >= targetCount ? 5 : 8;
                    if (noNewDataCount >= earlyStopThreshold)
                    {
                        _logger.LogWarning("[PageNoteCaptureService] 连续 {Count} 次滚动无新数据，停止滚动（已收集 {Total}/{Target}）",
                            noNewDataCount, noteIds.Count, targetCount);
                        break;
                    }
                }
                else
                {
                    noNewDataCount = 0;
                }

                // 拟人化延迟（指数分布 + 偶尔长停顿）
                var delay = GetHumanizedDelay(1000, 2000, longPauseProbability: 0.12);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _logger.LogDebug("[PageNoteCaptureService] 等待列表 API 响应超时");
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[PageNoteCaptureService] 滚动监听列表 API 时出现异常");
                break;
            }
        }

        return noteIds.Take(targetCount).ToList();
    }

    /// <summary>
    /// 中文：从列表 API 响应中提取 note_id。
    /// English: Extracts note_id from list API response.
    /// </summary>
    private List<string> ExtractNoteIdsFromListResponse(string jsonText, PageType pageType)
    {
        var noteIds = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data))
            {
                return noteIds;
            }

            // 发现页 API（/homefeed）和搜索页 API（/search/notes）的数据结构可能不同
            JsonElement items;
            if (data.TryGetProperty("items", out var itemsArray))
            {
                items = itemsArray;
            }
            else if (data.ValueKind == JsonValueKind.Array)
            {
                items = data;
            }
            else
            {
                _logger.LogDebug("[PageNoteCaptureService] 列表 API 响应数据结构未识别");
                return noteIds;
            }

            foreach (var item in items.EnumerateArray())
            {
                // 尝试多种可能的 note_id 字段名
                if (item.TryGetProperty("note_id", out var noteId) && noteId.ValueKind == JsonValueKind.String)
                {
                    noteIds.Add(noteId.GetString()!);
                }
                else if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                {
                    noteIds.Add(id.GetString()!);
                }
                else if (item.TryGetProperty("note_card", out var noteCard))
                {
                    // 嵌套结构：{ "note_card": { "note_id": "..." } }
                    if (noteCard.TryGetProperty("note_id", out var nestedNoteId) && nestedNoteId.ValueKind == JsonValueKind.String)
                    {
                        noteIds.Add(nestedNoteId.GetString()!);
                    }
                    else if (noteCard.TryGetProperty("id", out var nestedId) && nestedId.ValueKind == JsonValueKind.String)
                    {
                        noteIds.Add(nestedId.GetString()!);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[PageNoteCaptureService] 解析列表 API 响应失败");
        }

        return noteIds;
    }

    /// <summary>
    /// 中文：阶段2 - 根据 note_id 列表，逐个点击卡片监听详情 API。
    /// English: Phase 2 - Click cards one by one based on note_id list and monitor detail API.
    /// </summary>
    private async Task<List<PageNoteCard>> CollectDetailsByClickingAsync(IPage page, List<string> noteIds, int targetCount, CancellationToken cancellationToken)
    {
        var collectedNotes = new List<PageNoteCard>();
        var extractedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < noteIds.Count && collectedNotes.Count < targetCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var noteId = noteIds[i];
            _logger.LogDebug("[PageNoteCaptureService] 点击笔记卡片 {Index}/{Total} note_id={NoteId}",
                i + 1, noteIds.Count, noteId);

            try
            {
                // 设置详情 API 响应监听器
                var apiResponseTask = WaitForFeedApiResponseAsync(page, cancellationToken);

                // 点击笔记卡片（页面会跳转到详情页）
                var cardLocator = page.Locator($"a[href*='/explore/{noteId}']").First;
                await cardLocator.ClickAsync(new LocatorClickOptions { Timeout = 5000 }).ConfigureAwait(false);

                // 等待详情 API 响应
                var response = await apiResponseTask.ConfigureAwait(false);
                if (response != null)
                {
                    var jsonText = await response.TextAsync().ConfigureAwait(false);
                    var notes = ParseDetailApiResponse(jsonText);

                    foreach (var note in notes)
                    {
                        if (!extractedIds.Contains(note.Id))
                        {
                            extractedIds.Add(note.Id);
                            collectedNotes.Add(note);
                            _logger.LogInformation("[PageNoteCaptureService] 已采集 {Current}/{Target} 条笔记 id={Id}",
                                collectedNotes.Count, targetCount, note.Id);

                            if (collectedNotes.Count >= targetCount)
                            {
                                break;
                            }
                        }
                    }
                }

                // 关闭模态窗口：优先使用 ESC 键（最拟人化），失败则点击关闭按钮
                await CloseNoteDetailModalAsync(page, cancellationToken).ConfigureAwait(false);

                // 拟人化延迟（指数分布 + 偶尔长停顿，模拟真人阅读详情）
                var delay = GetHumanizedDelay(1500, 3000, longPauseProbability: 0.2);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("[PageNoteCaptureService] 点击笔记卡片或等待响应超时 note_id={NoteId}", noteId);
                // 尝试关闭模态窗口
                try
                {
                    await CloseNoteDetailModalAsync(page, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    _logger.LogDebug("[PageNoteCaptureService] 无法关闭模态窗口（可能仍在列表页）");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[PageNoteCaptureService] 点击笔记卡片时出现异常 note_id={NoteId}", noteId);
                // 尝试关闭模态窗口
                try
                {
                    await CloseNoteDetailModalAsync(page, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    _logger.LogDebug("[PageNoteCaptureService] 无法关闭模态窗口（可能仍在列表页）");
                }
            }
        }

        return collectedNotes;
    }

    /// <summary>
    /// 中文：等待详情 feed API 响应。
    /// English: Waits for detail feed API response.
    /// </summary>
    private async Task<IResponse?> WaitForFeedApiResponseAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            var response = await page.WaitForResponseAsync(
                response => response.Url.Contains("/api/sns/web/v1/feed") && response.Status == 200,
                new PageWaitForResponseOptions { Timeout = 10000 }).ConfigureAwait(false);
            return response;
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("[PageNoteCaptureService] 等待 API 响应超时");
            return null;
        }
    }

    /// <summary>
    /// 中文：关闭笔记详情模态窗口（拟人化方式：优先 ESC 键，失败则点击关闭按钮）。
    /// English: Closes note detail modal using human-like approach: ESC key first, fallback to close button.
    /// </summary>
    private async Task CloseNoteDetailModalAsync(IPage page, CancellationToken cancellationToken)
    {
        // 方法1：按 ESC 键（最拟人化）
        try
        {
            await page.Keyboard.PressAsync("Escape").ConfigureAwait(false);
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);

            // 验证是否关闭成功
            var maskCount = await page.Locator(".note-detail-mask").CountAsync().ConfigureAwait(false);
            if (maskCount == 0)
            {
                _logger.LogDebug("[PageNoteCaptureService] ESC 键关闭模态窗口成功");
                return;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "[PageNoteCaptureService] ESC 键关闭失败，尝试点击关闭按钮");
        }

        // 方法2：点击关闭按钮（降级方案）
        try
        {
            var closeBtn = page.Locator("button.close-icon").First;
            await closeBtn.ClickAsync(new LocatorClickOptions { Timeout = 3000 }).ConfigureAwait(false);
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("[PageNoteCaptureService] 点击关闭按钮成功");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[PageNoteCaptureService] 关闭模态窗口失败（ESC 和关闭按钮均失败）");
            throw;
        }
    }

    /// <summary>
    /// 中文:解析详情 API 响应 JSON（/api/sns/web/v1/feed）。
    /// English: Parses detail API response JSON (/api/sns/web/v1/feed).
    /// </summary>
    private List<PageNoteCard> ParseDetailApiResponse(string jsonText)
    {
        var notes = new List<PageNoteCard>();

        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            // 尝试获取 data.items 或 data (不同接口结构可能不同)
            if (!root.TryGetProperty("data", out var data))
            {
                _logger.LogDebug("[PageNoteCaptureService] API 响应缺少 data 字段");
                return notes;
            }

            JsonElement items;
            if (data.TryGetProperty("items", out var itemsArray))
            {
                items = itemsArray;
            }
            else if (data.ValueKind == JsonValueKind.Array)
            {
                items = data;
            }
            else
            {
                _logger.LogDebug("[PageNoteCaptureService] API 响应 data 字段结构未知");
                return notes;
            }

            foreach (var item in items.EnumerateArray())
            {
                try
                {
                    var note = ParseNoteItem(item);
                    if (note != null)
                    {
                        notes.Add(note);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[PageNoteCaptureService] 解析单条笔记失败");
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[PageNoteCaptureService] JSON 解析失败");
        }

        return notes;
    }

    /// <summary>
    /// 中文：解析单条笔记项。
    /// English: Parses a single note item.
    /// </summary>
    private PageNoteCard? ParseNoteItem(JsonElement item)
    {
        // 小红书 API 可能的结构：
        // { "id": "...", "note_card": { "title": "...", "user": { "nickname": "..." }, ... } }
        // 或 { "id": "...", "xsec_token": "...", "title": "...", "user": { "nickname": "..." }, ... }

        string? id = null;
        string? title = null;
        string? author = null;
        string? coverImage = null;

        // 尝试获取 ID
        if (item.TryGetProperty("id", out var idProp))
        {
            id = idProp.GetString();
        }
        else if (item.TryGetProperty("note_id", out var noteIdProp))
        {
            id = noteIdProp.GetString();
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        // 尝试从 note_card 获取详细信息
        if (item.TryGetProperty("note_card", out var noteCard))
        {
            if (noteCard.TryGetProperty("display_title", out var titleProp))
            {
                title = titleProp.GetString();
            }
            else if (noteCard.TryGetProperty("title", out var titleProp2))
            {
                title = titleProp2.GetString();
            }

            if (noteCard.TryGetProperty("user", out var user))
            {
                if (user.TryGetProperty("nickname", out var nicknameProp))
                {
                    author = nicknameProp.GetString();
                }
                else if (user.TryGetProperty("nick_name", out var nicknameProp2))
                {
                    author = nicknameProp2.GetString();
                }
            }

            if (noteCard.TryGetProperty("cover", out var cover))
            {
                if (cover.TryGetProperty("url_default", out var urlProp))
                {
                    coverImage = urlProp.GetString();
                }
                else if (cover.TryGetProperty("url", out var urlProp2))
                {
                    coverImage = urlProp2.GetString();
                }
            }
        }
        else
        {
            // 直接从 item 获取
            if (item.TryGetProperty("title", out var titleProp))
            {
                title = titleProp.GetString();
            }

            if (item.TryGetProperty("user", out var user))
            {
                if (user.TryGetProperty("nickname", out var nicknameProp))
                {
                    author = nicknameProp.GetString();
                }
            }
        }

        var url = $"https://www.xiaohongshu.com/explore/{id}";

        return new PageNoteCard(
            Id: id,
            Url: url,
            Title: title ?? string.Empty,
            Author: author ?? string.Empty,
            CoverImage: coverImage ?? string.Empty);
    }

    /// <summary>
    /// 中文：导出笔记数据为 CSV 文件。
    /// English: Exports note data to CSV file.
    /// </summary>
    private async Task<string> ExportToCsvAsync(List<PageNoteCard> notes, string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var fileName = $"page_notes_{timestamp}.csv";
        var filePath = Path.Combine(outputDirectory, fileName);

        var csv = new StringBuilder();
        csv.AppendLine("ID,URL,Title,Author,CoverImage");

        foreach (var note in notes)
        {
            csv.AppendLine($"{EscapeCsv(note.Id)},{EscapeCsv(note.Url)},{EscapeCsv(note.Title)},{EscapeCsv(note.Author)},{EscapeCsv(note.CoverImage)}");
        }

        await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[PageNoteCaptureService] 已导出 CSV 文件 path={Path}", filePath);
        return filePath;
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    /// <summary>
    /// 中文：拟人化滚动页面（使用鼠标滚轮模拟，距离随机）。
    /// English: Humanized page scrolling using mouse wheel with random distance.
    /// </summary>
    private async Task HumanizedScrollAsync(IPage page, CancellationToken cancellationToken)
    {
        // 随机滚动距离 400-900px，模拟真人不同力度的滚动
        var scrollDistance = Random.Shared.Next(400, 900);

        // 模拟滚轮：每次滚动约 120px 为一个"tick"（标准滚轮单位）
        var tickCount = scrollDistance / 120;

        for (var i = 0; i < tickCount; i++)
        {
            await page.Mouse.WheelAsync(0, 120).ConfigureAwait(false);
            // 微小延迟 30-100ms，模拟滚轮连续滚动的时间间隔
            await Task.Delay(Random.Shared.Next(30, 100), cancellationToken).ConfigureAwait(false);
        }

        _logger.LogDebug("[PageNoteCaptureService] 拟人化滚动 {Distance}px", scrollDistance);
    }

    /// <summary>
    /// 中文：获取拟人化延迟时间（毫秒），使用指数分布 + 偶尔长停顿。
    /// English: Gets humanized delay in milliseconds using exponential distribution + occasional long pauses.
    /// </summary>
    private int GetHumanizedDelay(int baseMin, int baseMax, double longPauseProbability = 0.15)
    {
        // 15% 概率产生长停顿（模拟真人阅读、思考）
        if (Random.Shared.NextDouble() < longPauseProbability)
        {
            var longPause = Random.Shared.Next(baseMax * 2, baseMax * 5); // 2-5倍基础延迟
            _logger.LogDebug("[PageNoteCaptureService] 长停顿 {Delay}ms（模拟真人思考）", longPause);
            return longPause;
        }

        // 85% 概率正常延迟，使用指数分布（更接近真人行为）
        var mean = (baseMin + baseMax) / 2.0;
        var lambda = 1.0 / mean;
        var u = Random.Shared.NextDouble();
        // 指数分布: -ln(1-U) / λ
        var exponential = -Math.Log(1.0 - u) / lambda;
        var delay = (int)Math.Clamp(exponential, baseMin, baseMax * 1.5);

        return delay;
    }
}

/// <summary>
/// 中文：页面笔记卡片数据模型。
/// English: Data model for page note card.
/// </summary>
/// <param name="Id">笔记 ID | Note ID.</param>
/// <param name="Url">笔记链接 | Note URL.</param>
/// <param name="Title">笔记标题 | Note title.</param>
/// <param name="Author">作者名称 | Author name.</param>
/// <param name="CoverImage">封面图片链接 | Cover image URL.</param>
public sealed record PageNoteCard(
    string Id,
    string Url,
    string Title,
    string Author,
    string CoverImage);
