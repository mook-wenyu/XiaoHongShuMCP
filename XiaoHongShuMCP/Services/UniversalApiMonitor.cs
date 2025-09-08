using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 通用 API 监听器（实现 IUniversalApiMonitor）。
/// - 职责：在单页应用场景下，基于 Playwright 的 Response 事件被动捕获目标端点的响应，
///         同时保留“原始响应”和“结构化处理结果”，供上层统一的数据通道使用。
/// - 端点：支持推荐（Homefeed）、详情（Feed）、搜索（SearchNotes）等核心 API；可按需扩展处理器。
/// - 线程安全：内部通过 <see cref="_lock"/> 保护响应容器，事件回调与查询操作可并发；
///             等待方法按 MCP 统一超时（<see cref="McpSettings.WaitTimeoutMs"/>）。
/// - 生命周期：调用 <see cref="SetupMonitor"/> 绑定 <see cref="IBrowserContext.Response"/> 事件，
///             <see cref="StopMonitoringAsync"/> 负责解绑；<see cref="Dispose"/> 中做兜底清理。
/// - 日志：对敏感字段（如 xsec_token）进行日志脱敏（仅日志，不影响内存中数据）。
/// 使用方式建议：
/// 1) SetupMonitor(page, {Endpoints});
/// 2) 触发导航/滚动等行为使 API 发起；
/// 3) WaitForResponsesAsync(endpointType, count);
/// 4) GetMonitoredNoteDetails/GetRawResponses；必要时 ClearMonitoredData。
/// </summary>
public class UniversalApiMonitor : IUniversalApiMonitor
{
    private readonly ILogger<UniversalApiMonitor> _logger;
    private readonly McpSettings _mcpSettings;
    private readonly Dictionary<ApiEndpointType, List<MonitoredApiResponse>> _monitoredResponses;
    private readonly Dictionary<ApiEndpointType, IApiResponseProcessor> _processors;
    private readonly object _lock = new();
    private IBrowserContext? _context;
    private bool _isMonitoring;
    private HashSet<ApiEndpointType> _activeEndpoints;
    private long _responseEventSeq;

    public UniversalApiMonitor(ILogger<UniversalApiMonitor> logger, Microsoft.Extensions.Options.IOptions<McpSettings> mcp)
    {
        _logger = logger;
        _mcpSettings = mcp.Value ?? new McpSettings();
        _monitoredResponses = new Dictionary<ApiEndpointType, List<MonitoredApiResponse>>();
        _processors = new Dictionary<ApiEndpointType, IApiResponseProcessor>();
        _activeEndpoints = [];

        // 初始化各端点的响应存储
        foreach (ApiEndpointType endpointType in Enum.GetValues<ApiEndpointType>())
        {
            _monitoredResponses[endpointType] = [];
        }

        // 注册默认的响应处理器
        RegisterDefaultProcessors();
    }

    /// <summary>
    /// 注册默认的响应处理器
    /// </summary>
    private void RegisterDefaultProcessors()
    {
        _processors[ApiEndpointType.Homefeed] = new HomefeedResponseProcessor(_logger);
        _processors[ApiEndpointType.Feed] = new FeedResponseProcessor(_logger);
        _processors[ApiEndpointType.SearchNotes] = new SearchNotesResponseProcessor(_logger);
    }

    /// <summary>
    /// 设置通用 API 监听器并开始监听。
    /// - 将当前 <paramref name="page"/> 的 <see cref="IBrowserContext"/> 保持在本实例中；
    /// - 绑定 <see cref="IBrowserContext.Response"/> 事件；
    /// - 仅对 <paramref name="endpointsToMonitor"/> 指定的端点进行处理。
    /// 重复调用将覆盖活跃端点集合（不会重复绑定事件）。
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="endpointsToMonitor">要监听的端点类型</param>
    /// <returns>设置是否成功</returns>
    public bool SetupMonitor(IPage page,
        HashSet<ApiEndpointType> endpointsToMonitor)
    {
        try
        {
            _context = page.Context;
            _activeEndpoints = endpointsToMonitor;

            _logger.LogInformation("正在设置通用API监听器，监听端点: {Endpoints}",
                string.Join(", ", endpointsToMonitor));

            // 设置被动响应监听器
            _context.Response += OnResponseReceived;

            _isMonitoring = true;

            _logger.LogInformation("通用API监听器设置完成，活跃端点数: {Count}", _activeEndpoints.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置通用API监听器失败");
            return false;
        }
    }

    /// <summary>
    /// 响应事件处理：识别端点 → 只处理 200 成功 → 调用端点处理器 → 写入容器。
    /// 注意：此回调在 Playwright 事件线程触发，内部仅做必要处理并写入内存，避免耗时阻塞。
    /// </summary>
    private async void OnResponseReceived(object? sender, IResponse response)
    {
        try
        {
            // 识别API端点类型
            var endpointType = IdentifyApiEndpoint(response.Url);
            if (endpointType == null)
            {
                return;
            }
            if (!_activeEndpoints.Contains(endpointType.Value))
            {
                return;
            }

            _logger.LogDebug("[Resp] 命中端点 {Endpoint} | url={Url}", endpointType, response.Url);

            // 只处理成功的响应
            if (response.Status != 200)
            {
                return;
            }

            _logger.LogDebug("[Resp] 开始读取响应正文...");
            var responseBody = await response.TextAsync();
            var sanitized = SanitizeResponseBodyForLog(responseBody);

            // 使用对应的处理器处理响应
            if (_processors.TryGetValue(endpointType.Value, out var processor))
            {
                _logger.LogDebug("[Resp] 调用处理器 {Processor}...", processor.GetType().Name);
                var processedResponse = await processor.ProcessResponseAsync(response.Url, responseBody);
                if (processedResponse != null)
                {
                    lock (_lock)
                    {
                        _monitoredResponses[endpointType.Value].Add(processedResponse);
                    }

                    _logger.LogDebug("[Resp] 处理完成 | endpoint={Endpoint} items={Count}", endpointType, processedResponse.ProcessedDataCount);
                }
                else
                {
                    _logger.LogDebug("[Resp] 处理器返回空结果 | endpoint={Endpoint}", endpointType);
                }
            }
            else
            {
                _logger.LogDebug("[Resp] 未找到处理器 | endpoint={Endpoint}", endpointType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Resp] 处理API响应时发生错误 | url={Url}", response.Url);
        }
    }

    /// <summary>
    /// 日志脱敏：屏蔽敏感字段值（如 xsec_token）
    /// 仅用于日志输出，不影响内存中保存的原始数据
    /// </summary>
    private static string SanitizeResponseBodyForLog(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        try
        {
            // 屏蔽 xsec_token 的值，保留字段名
            content = Regex.Replace(
                content,
                "(\"xsec_token\"\\s*:\\s*\")(.*?)(\")",
                "$1***$3",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            return content;
        }
        catch
        {
            return content;
        }
    }

    /// <summary>
    /// 识别 API 端点类型（基于 URL 片段的轻量规则）。
    /// </summary>
    private static ApiEndpointType? IdentifyApiEndpoint(string url)
    {
        if (url.Contains("/api/sns/web/v1/homefeed"))
            return ApiEndpointType.Homefeed;

        if (url.Contains("/api/sns/web/v1/feed"))
            return ApiEndpointType.Feed;

        if (url.Contains("/api/sns/web/v1/search/notes"))
            return ApiEndpointType.SearchNotes;

        return null;
    }

    /// <summary>
    /// 等待指定端点的 API 响应。
    /// - 统一等待时长：使用 <see cref="McpSettings.WaitTimeoutMs"/>（默认 10 分钟），不做上限封顶；
    /// - 判定条件：监听容器中累计响应条数达到 <paramref name="expectedCount"/> 即返回 true；
    /// - 轮询间隔：100ms，兼顾实时性与开销；
    /// - 超时：返回 false，并记录当前已捕获数量。
    /// </summary>
    public async Task<bool> WaitForResponsesAsync(ApiEndpointType endpointType,
        int expectedCount = 1)
    {
        // 统一 MCP 等待超时（默认 10 分钟）；若配置 <=0 则回退到默认 10 分钟
        var cfgMs = _mcpSettings.WaitTimeoutMs;
        var waitMs = cfgMs > 0 ? cfgMs : 600_000;
        return await WaitForResponsesAsync(endpointType, TimeSpan.FromMilliseconds(waitMs), expectedCount);
    }

    /// <summary>
    /// 在指定超时时间内等待指定端点的API响应。
    /// </summary>
    public async Task<bool> WaitForResponsesAsync(ApiEndpointType endpointType, TimeSpan timeout, int expectedCount = 1)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogDebug("等待 {EndpointType} API响应: 期望数量={ExpectedCount}, 超时={Timeout}ms",
            endpointType, expectedCount, timeout.TotalMilliseconds);

        while (DateTime.UtcNow - startTime < timeout)
        {
            lock (_lock)
            {
                if (_monitoredResponses[endpointType].Count >= expectedCount)
                {
                    _logger.LogDebug("成功获取到足够的 {EndpointType} 响应: {Count}",
                        endpointType, _monitoredResponses[endpointType].Count);
                    return true;
                }
            }

            await Task.Delay(100);
        }

        lock (_lock)
        {
            _logger.LogWarning("等待 {EndpointType} 响应超时: 期望={Expected}, 实际={Actual}",
                endpointType, expectedCount, _monitoredResponses[endpointType].Count);
        }

        return false;
    }

    /// <summary>
    /// 获取指定端点监听到的“结构化笔记详情”集合（累积）。
    /// 数据来源于各端点处理器对原始响应的解析产物。
    /// </summary>
    public List<NoteDetail> GetMonitoredNoteDetails(ApiEndpointType endpointType)
    {
        lock (_lock)
        {
            var responses = _monitoredResponses[endpointType];
            var noteDetails = new List<NoteDetail>();

            foreach (var response in responses)
            {
                if (response.ProcessedNoteDetails != null)
                {
                    noteDetails.AddRange(response.ProcessedNoteDetails);
                }
            }

            _logger.LogDebug("从 {EndpointType} 获取到 {Count} 个笔记详情", endpointType, noteDetails.Count);
            return noteDetails;
        }
    }

    /// <summary>
    /// 获取指定端点监听到的原始响应数据（累积）。
    /// 常用于调试、回放或导出。
    /// </summary>
    public List<MonitoredApiResponse> GetRawResponses(ApiEndpointType endpointType)
    {
        lock (_lock)
        {
            return _monitoredResponses[endpointType].ToList();
        }
    }

    /// <summary>
    /// 清理监听数据。
    /// - 传入端点值时仅清理该端点；
    /// - 否则清理全部端点的累积数据。
    /// </summary>
    public void ClearMonitoredData(ApiEndpointType? endpointType = null)
    {
        lock (_lock)
        {
            if (endpointType.HasValue)
            {
                var count = _monitoredResponses[endpointType.Value].Count;
                _monitoredResponses[endpointType.Value].Clear();
                _logger.LogDebug("清理了 {EndpointType} 的 {Count} 个响应", endpointType, count);
            }
            else
            {
                // 清理所有端点数据
                var totalCount = _monitoredResponses.Values.Sum(list => list.Count);
                foreach (var responses in _monitoredResponses.Values)
                {
                    responses.Clear();
                }
                _logger.LogDebug("清理了所有端点的 {Count} 个响应", totalCount);
            }
        }
    }

    /// <summary>
    /// 停止 API 监听（解绑 Response 事件并保持幂等）。
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        if (_context != null && _isMonitoring)
        {
            try
            {
                _context.Response -= OnResponseReceived;
                _isMonitoring = false;
                _logger.LogInformation("已停止通用API监听");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "停止通用API监听时发生错误");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isMonitoring)
        {
            _ = Task.Run(async () => await StopMonitoringAsync());
        }

        ClearMonitoredData();
    }
}
/// <summary>
/// 监听到的API响应数据
/// </summary>
public class MonitoredApiResponse
{
    public DateTime ResponseTime { get; set; }
    public string RequestUrl { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public int ProcessedDataCount { get; set; }
    public List<NoteDetail>? ProcessedNoteDetails { get; set; }
    public Dictionary<string, object>? ProcessedData { get; set; }
    public ApiEndpointType EndpointType { get; set; }
}
/// <summary>
/// API响应处理器接口
/// </summary>
public interface IApiResponseProcessor
{
    Task<MonitoredApiResponse?> ProcessResponseAsync(string requestUrl, string responseBody);
}
/// <summary>
/// 推荐API响应处理器
/// </summary>
public class HomefeedResponseProcessor : IApiResponseProcessor
{
    private readonly ILogger _logger;

    public HomefeedResponseProcessor(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<MonitoredApiResponse?> ProcessResponseAsync(string requestUrl, string responseBody)
    {
        try
        {
            var response = JsonSerializer.Deserialize<HomefeedResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (response is {Success: true, Data.Items: not null})
            {
                var noteDetails = new List<NoteDetail>();

                foreach (var item in response.Data.Items)
                {
                    var noteInfo = HomefeedConverter.ConvertToNoteInfo(item);
                    if (noteInfo != null)
                    {
                        noteDetails.Add(noteInfo as NoteDetail ?? new NoteDetail
                        {
                            Id = noteInfo.Id,
                            Title = noteInfo.Title,
                            Author = noteInfo.Author,
                            AuthorId = noteInfo.AuthorId,
                            AuthorAvatar = noteInfo.AuthorAvatar,
                            CoverImage = noteInfo.CoverImage,
                            Url = noteInfo.Url,
                            Type = noteInfo.Type,
                            LikeCount = noteInfo.LikeCount,
                            CommentCount = noteInfo.CommentCount,
                            FavoriteCount = noteInfo.FavoriteCount,
                            ExtractedAt = noteInfo.ExtractedAt,
                            Quality = noteInfo.Quality,
                            MissingFields = noteInfo.MissingFields ?? []
                        });
                    }
                }

                return new MonitoredApiResponse
                {
                    ResponseTime = DateTime.UtcNow,
                    RequestUrl = requestUrl,
                    ResponseBody = responseBody,
                    ProcessedDataCount = noteDetails.Count,
                    ProcessedNoteDetails = noteDetails,
                    EndpointType = ApiEndpointType.Homefeed
                };
            }

            await Task.CompletedTask;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理推荐API响应失败");
            return null;
        }
    }
}
/// <summary>
/// Feed API响应处理器
/// </summary>
public class FeedResponseProcessor : IApiResponseProcessor
{
    private readonly ILogger _logger;

    public FeedResponseProcessor(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<MonitoredApiResponse?> ProcessResponseAsync(string requestUrl, string responseBody)
    {
        try
        {
            var apiResponse = JsonSerializer.Deserialize<FeedApiResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (apiResponse != null && FeedApiConverter.IsValidFeedResponse(apiResponse))
            {
                var noteDetails = FeedApiConverter.ConvertToNoteDetails(apiResponse);

                return new MonitoredApiResponse
                {
                    ResponseTime = DateTime.UtcNow,
                    RequestUrl = requestUrl,
                    ResponseBody = responseBody,
                    ProcessedDataCount = noteDetails.Count,
                    ProcessedNoteDetails = noteDetails,
                    EndpointType = ApiEndpointType.Feed
                };
            }

            await Task.CompletedTask;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理Feed API响应失败");
            return null;
        }
    }
}
/// <summary>
/// 搜索API响应处理器
/// </summary>
public class SearchNotesResponseProcessor : IApiResponseProcessor
{
    private readonly ILogger _logger;

    public SearchNotesResponseProcessor(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<MonitoredApiResponse?> ProcessResponseAsync(string requestUrl, string responseBody)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(responseBody);
            var rootElement = jsonDoc.RootElement;

            // 解析搜索API响应结构
            if (!rootElement.TryGetProperty("success", out var successElement) ||
                !successElement.GetBoolean())
            {
                _logger.LogWarning("搜索API响应成功标志为false");
                return null;
            }

            if (!rootElement.TryGetProperty("data", out var dataElement))
            {
                _logger.LogWarning("搜索API响应缺少data字段");
                return null;
            }

            var noteDetails = new List<NoteDetail>();

            // 解析搜索结果中的笔记数据
            if (dataElement.TryGetProperty("items", out var itemsElement) &&
                itemsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsElement.EnumerateArray())
                {
                    try
                    {
                        var noteDetail = ParseSearchNoteItem(item);
                        if (noteDetail != null)
                        {
                            noteDetails.Add(noteDetail);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "解析单个搜索笔记项失败");
                    }
                }
            }

            _logger.LogDebug("成功解析搜索API响应，获得 {Count} 个笔记", noteDetails.Count);

            return new MonitoredApiResponse
            {
                ResponseTime = DateTime.UtcNow,
                RequestUrl = requestUrl,
                ResponseBody = responseBody,
                ProcessedDataCount = noteDetails.Count,
                ProcessedNoteDetails = noteDetails,
                EndpointType = ApiEndpointType.SearchNotes
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理搜索API响应失败");
            return null;
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// 解析搜索结果中的单个笔记项
    /// </summary>
    private NoteDetail? ParseSearchNoteItem(JsonElement item)
    {
        try
        {
            // 顶层字段
            var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() : string.Empty;

            // 大多数字段位于 note_card 下
            item.TryGetProperty("note_card", out var noteCard);

            // 标题
            string? title = null;
            if (noteCard.ValueKind != JsonValueKind.Undefined &&
                noteCard.TryGetProperty("display_title", out var titleElement))
            {
                title = titleElement.GetString();
            }
            // 兼容极少数直挂字段（正常不会有）
            if (string.IsNullOrEmpty(title) && item.TryGetProperty("display_title", out var titleTop))
            {
                title = titleTop.GetString();
            }

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
            {
                return null; // 必要字段缺失，丢弃该项
            }

            var noteDetail = new NoteDetail
            {
                Id = id,
                Title = title ?? string.Empty,
                ExtractedAt = DateTime.UtcNow,
                Quality = DataQuality.Partial,
                MissingFields = []
            };

            // 作者信息：note_card.user
            if (noteCard.ValueKind != JsonValueKind.Undefined &&
                noteCard.TryGetProperty("user", out var userElement))
            {
                string nickname = string.Empty;
                if (userElement.TryGetProperty("nickname", out var nicknameElement))
                {
                    nickname = nicknameElement.GetString() ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(nickname) && userElement.TryGetProperty("nick_name", out var nickNameAlt))
                {
                    nickname = nickNameAlt.GetString() ?? string.Empty;
                }

                noteDetail.Author = nickname;
                noteDetail.AuthorId = userElement.TryGetProperty("user_id", out var userIdElement)
                    ? userIdElement.GetString() ?? string.Empty : string.Empty;
                noteDetail.AuthorAvatar = userElement.TryGetProperty("avatar", out var avatarElement)
                    ? avatarElement.GetString() ?? string.Empty : string.Empty;
            }

            // 封面图片：note_card.cover.url_default 优先
            if (noteCard.ValueKind != JsonValueKind.Undefined &&
                noteCard.TryGetProperty("cover", out var coverElement))
            {
                if (coverElement.TryGetProperty("url_default", out var urlDefault))
                {
                    noteDetail.CoverImage = urlDefault.GetString() ?? string.Empty;
                }
                else if (coverElement.TryGetProperty("url", out var coverUrl))
                {
                    noteDetail.CoverImage = coverUrl.GetString() ?? string.Empty;
                }
                else if (coverElement.ValueKind == JsonValueKind.String)
                {
                    noteDetail.CoverImage = coverElement.GetString() ?? string.Empty;
                }
            }

            // 笔记类型：存在 note_card.video 则判定为视频，并提取视频关键字段
            if (noteCard.ValueKind != JsonValueKind.Undefined && noteCard.TryGetProperty("video", out var videoEl))
            {
                noteDetail.Type = NoteType.Video;
                try
                {
                    if (videoEl.TryGetProperty("url", out var vurl))
                    {
                        noteDetail.VideoUrl = vurl.GetString() ?? string.Empty;
                    }
                    if (videoEl.TryGetProperty("duration", out var vdur))
                    {
                        noteDetail.VideoDuration = vdur.GetInt32();
                    }
                }
                catch
                {
                    /* 忽略视频字段解析异常 */
                }
            }
            else
            {
                noteDetail.Type = NoteType.Image;
            }

            // 互动数据：note_card.interact_info，注意计数可能是字符串（如 "1.2万"）
            if (noteCard.ValueKind != JsonValueKind.Undefined &&
                noteCard.TryGetProperty("interact_info", out var interactElement))
            {
                var likedRawStr = string.Empty;
                if (interactElement.TryGetProperty("liked_count", out var likedEl))
                {
                    noteDetail.LikeCount = ParseCountElement(likedEl);
                    if (likedEl.ValueKind == JsonValueKind.String)
                    {
                        likedRawStr = likedEl.GetString() ?? string.Empty;
                    }
                    else
                    {
                        likedRawStr = noteDetail.LikeCount?.ToString() ?? string.Empty;
                    }
                }
                if (interactElement.TryGetProperty("comment_count", out var commentEl))
                {
                    noteDetail.CommentCount = ParseCountElement(commentEl);
                }
                if (interactElement.TryGetProperty("collected_count", out var collectedEl))
                {
                    noteDetail.FavoriteCount = ParseCountElement(collectedEl);
                }
                if (interactElement.TryGetProperty("liked", out var likedFlag) && likedFlag.ValueKind == JsonValueKind.True)
                {
                    noteDetail.IsLiked = true;
                }
                if (interactElement.TryGetProperty("collected", out var collectedFlag) && collectedFlag.ValueKind == JsonValueKind.True)
                {
                    noteDetail.IsCollected = true;
                }
                if (interactElement.TryGetProperty("shared_count", out var sharedEl))
                {
                    noteDetail.ShareCount = ParseCountElement(sharedEl);
                }

                // 记录原始交互字段
                noteDetail.InteractInfo = new RecommendedInteractInfo
                {
                    LikedCountRaw = likedRawStr,
                    LikedCount = noteDetail.LikeCount ?? 0,
                    CommentCount = noteDetail.CommentCount ?? 0,
                    CollectedCount = noteDetail.FavoriteCount ?? 0,
                    ShareCount = noteDetail.ShareCount ?? 0,
                    Liked = noteDetail.IsLiked,
                    Collected = noteDetail.IsCollected
                };
            }

            // 文本内容预览
            if (noteCard.ValueKind != JsonValueKind.Undefined && noteCard.TryGetProperty("desc", out var descEl))
            {
                noteDetail.Description = descEl.GetString() ?? string.Empty;
                if (string.IsNullOrEmpty(noteDetail.Content))
                {
                    noteDetail.Content = noteDetail.Description;
                }
            }

            // 图片列表（取每张图的默认/预览URL），同时写入 NoteDetail.Images
            if (noteCard.ValueKind != JsonValueKind.Undefined && noteCard.TryGetProperty("image_list", out var imageListEl) && imageListEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var img in imageListEl.EnumerateArray())
                {
                    try
                    {
                        if (img.TryGetProperty("info_list", out var infos) && infos.ValueKind == JsonValueKind.Array)
                        {
                            // WB_DFT 优先
                            string? chosen = null;
                            foreach (var info in infos.EnumerateArray())
                            {
                                if (info.TryGetProperty("image_scene", out var scene) && scene.GetString() == "WB_DFT" && info.TryGetProperty("url", out var url))
                                {
                                    chosen = url.GetString();
                                    break;
                                }
                            }
                            if (chosen == null)
                            {
                                foreach (var info in infos.EnumerateArray())
                                {
                                    if (info.TryGetProperty("url", out var url))
                                    {
                                        chosen = url.GetString();
                                        if (!string.IsNullOrEmpty(chosen)) break;
                                    }
                                }
                            }
                            if (!string.IsNullOrEmpty(chosen)) noteDetail.Images.Add(chosen);
                        }
                        else if (img.TryGetProperty("url", out var imgUrlEl))
                        {
                            var u = imgUrlEl.GetString();
                            if (!string.IsNullOrEmpty(u)) noteDetail.Images.Add(u);
                        }
                    }
                    catch { }
                }
            }

            // 顶层扩展字段
            if (item.TryGetProperty("xsec_token", out var xsecEl))
            {
                noteDetail.XsecToken = xsecEl.GetString();
            }
            if (item.TryGetProperty("track_id", out var trackEl))
            {
                noteDetail.TrackId = trackEl.GetString();
            }

            // 构建笔记URL
            noteDetail.Url = $"https://www.xiaohongshu.com/explore/{id}";

            // 质量评估
            DetermineSearchNoteQuality(noteDetail);

            return noteDetail;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析搜索笔记项异常");
            return null;
        }
    }

    private static int ParseCountElement(JsonElement el)
    {
        try
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Number:
                    return (int)el.GetInt64();
                case JsonValueKind.String:
                    return ParseChineseCount(el.GetString());
                default:
                    return 0;
            }
        }
        catch
        {
            return 0;
        }
    }

    private static int ParseChineseCount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        raw = raw.Trim();
        try
        {
            if (raw.EndsWith("万", StringComparison.Ordinal))
            {
                if (double.TryParse(raw[..^1], out var n)) return (int)Math.Round(n * 10000);
                return 0;
            }
            if (raw.EndsWith("亿", StringComparison.Ordinal))
            {
                if (double.TryParse(raw[..^1], out var n)) return (int)Math.Round(n * 100000000);
                return 0;
            }
            return int.TryParse(raw, out var i) ? i : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 评估搜索笔记的数据质量
    /// </summary>
    private static void DetermineSearchNoteQuality(NoteDetail note)
    {
        var missingFields = new List<string>();
        var qualityScore = 0;

        // 检查必要字段
        if (string.IsNullOrEmpty(note.Id)) missingFields.Add("Id");
        else qualityScore += 2;

        if (string.IsNullOrEmpty(note.Title)) missingFields.Add("Title");
        else qualityScore += 2;

        if (string.IsNullOrEmpty(note.Author)) missingFields.Add("Author");
        else qualityScore += 1;

        if (string.IsNullOrEmpty(note.CoverImage)) missingFields.Add("CoverImage");
        else qualityScore += 1;

        if (note.LikeCount == 0 && note.CommentCount == 0) missingFields.Add("InteractionData");
        else qualityScore += 1;

        // 设置质量评级
        note.Quality = qualityScore switch
        {
            >= 6 => DataQuality.Complete,
            >= 4 => DataQuality.Partial,
            >= 2 => DataQuality.Partial,
            _ => DataQuality.Minimal
        };

        note.MissingFields = missingFields;
    }
}
