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
///             等待方法按 MCP 统一超时（<see cref="XhsSettings.McpSettingsSection.WaitTimeoutMs"/>）。
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
    private readonly XhsSettings.McpSettingsSection _mcpSettings;
    private readonly Dictionary<ApiEndpointType, List<MonitoredApiResponse>> _monitoredResponses;
    private readonly Dictionary<ApiEndpointType, IApiResponseProcessor> _processors;
    private readonly object _lock = new();
    private IBrowserContext? _context;
    private bool _isMonitoring;
    private HashSet<ApiEndpointType> _activeEndpoints;
    private long _responseEventSeq;

    // 数据去重相关字段
    private readonly HashSet<string> _processedNoteIds;
    private readonly Dictionary<string, NoteDetail> _uniqueNoteDetails;
    private DeduplicationStats _deduplicationStats;

    public UniversalApiMonitor(ILogger<UniversalApiMonitor> logger, Microsoft.Extensions.Options.IOptions<XhsSettings> xhsOptions)
    {
        _logger = logger;
        var xhs = xhsOptions.Value ?? new XhsSettings();
        _mcpSettings = xhs.McpSettings ?? new XhsSettings.McpSettingsSection();
        _monitoredResponses = new Dictionary<ApiEndpointType, List<MonitoredApiResponse>>();
        _processors = new Dictionary<ApiEndpointType, IApiResponseProcessor>();
        _activeEndpoints = [];

        // 初始化数据去重相关字段
        _processedNoteIds = new HashSet<string>();
        _uniqueNoteDetails = new Dictionary<string, NoteDetail>();
        _deduplicationStats = new DeduplicationStats();

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
        _processors[ApiEndpointType.Comments] = new CommentsResponseProcessor(_logger);

        // 互动动作端点（点赞/收藏/评论）的权威响应处理器
        _processors[ApiEndpointType.LikeNote] = new LikeActionResponseProcessor(_logger);
        _processors[ApiEndpointType.DislikeNote] = new DislikeActionResponseProcessor(_logger);
        _processors[ApiEndpointType.CollectNote] = new CollectActionResponseProcessor(_logger);
        _processors[ApiEndpointType.UncollectNote] = new UncollectActionResponseProcessor(_logger);
        _processors[ApiEndpointType.CommentPost] = new CommentPostResponseProcessor(_logger);
        _processors[ApiEndpointType.CommentDelete] = new CommentDeleteResponseProcessor(_logger);
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
                        
                        // 应用数据去重机制
                        ApplyDeduplication(processedResponse, endpointType.Value);
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
    /// <summary>
    /// 识别 API 端点类型（改为版本无关正则，兼容 v1/v2/v3...）。
    /// - 示例：
    ///   https://edith.xiaohongshu.com/api/sns/web/v1/feed → Feed
    ///   https://edith.xiaohongshu.com/api/sns/web/v2/comment/page?... → Comments
    /// </summary>
    internal static ApiEndpointType? IdentifyApiEndpoint(string url)
    {
        // 使用不区分大小写的编译正则，兼容所有 vN 版本
        if (Regex.IsMatch(url, @"/api/sns/web/v\d+/homefeed", RegexOptions.IgnoreCase | RegexOptions.Compiled))
            return ApiEndpointType.Homefeed;

        if (Regex.IsMatch(url, @"/api/sns/web/v\d+/feed(\b|/|\?)", RegexOptions.IgnoreCase | RegexOptions.Compiled))
            return ApiEndpointType.Feed;

        if (Regex.IsMatch(url, @"/api/sns/web/v\d+/search/notes(\b|/|\?)", RegexOptions.IgnoreCase | RegexOptions.Compiled))
            return ApiEndpointType.SearchNotes;

        if (Regex.IsMatch(url, @"/api/sns/web/v\d+/comment/page(\b|/|\?)", RegexOptions.IgnoreCase | RegexOptions.Compiled))
            return ApiEndpointType.Comments;

        // ===== 新增：互动动作端点（版本无关） =====
        if (Regex.IsMatch(url, @"/api/sns/web/v\d+/note/like(\b|/|\?)", RegexOptions.IgnoreCase | RegexOptions.Compiled))
            return ApiEndpointType.LikeNote;
        if (Regex.IsMatch(url, @"/api/sns/web/v\d+/note/dislike(\b|/|\?)", RegexOptions.IgnoreCase | RegexOptions.Compiled))
            return ApiEndpointType.DislikeNote;
        if (Regex.IsMatch(url, @"/api/sns/web/v\d+/note/collect(\b|/|\?)", RegexOptions.IgnoreCase | RegexOptions.Compiled))
            return ApiEndpointType.CollectNote;
        if (Regex.IsMatch(url, @"/api/sns/web/v\d+/note/uncollect(\b|/|\?)", RegexOptions.IgnoreCase | RegexOptions.Compiled))
            return ApiEndpointType.UncollectNote;
        if (Regex.IsMatch(url, @"/api/sns/web/v\d+/comment/post(\b|/|\?)", RegexOptions.IgnoreCase | RegexOptions.Compiled))
            return ApiEndpointType.CommentPost;
        if (Regex.IsMatch(url, @"/api/sns/web/v\d+/comment/delete(\b|/|\?)", RegexOptions.IgnoreCase | RegexOptions.Compiled))
            return ApiEndpointType.CommentDelete;

        return null;
    }

    /// <summary>
    /// 等待指定端点的 API 响应。
    /// - 统一等待时长：使用 <see cref="XhsSettings.McpSettingsSection.WaitTimeoutMs"/>（默认 10 分钟），不做上限封顶；
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
    /// 获取指定端点监听到的"结构化笔记详情"集合（累积）。
    /// 数据来源于各端点处理器对原始响应的解析产物。
    /// 注意：返回的数据已经过去重处理。
    /// </summary>
    public List<NoteDetail> GetMonitoredNoteDetails(ApiEndpointType endpointType)
    {
        lock (_lock)
        {
            // 从去重缓存中获取指定端点的数据
            var endpointNotes = _uniqueNoteDetails.Values
                .Where(note => GetNoteSourceEndpoint(note) == endpointType)
                .ToList();

            _logger.LogDebug("从 {EndpointType} 获取到 {Count} 个去重后的笔记详情", endpointType, endpointNotes.Count);
            return endpointNotes;
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
                
                // 清理该端点相关的去重数据
                ClearDeduplicationDataForEndpoint(endpointType.Value);
                
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
                
                // 清理所有去重数据
                ClearDeduplicationData();
                
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
    /// 应用数据去重机制
    /// </summary>
    /// <param name="response">API响应数据</param>
    /// <param name="endpointType">端点类型</param>
    private void ApplyDeduplication(MonitoredApiResponse response, ApiEndpointType endpointType)
    {
        if (response.ProcessedNoteDetails == null || response.ProcessedNoteDetails.Count == 0)
            return;

        int totalProcessed = 0;
        int duplicatesFound = 0;
        int newUnique = 0;

        foreach (var noteDetail in response.ProcessedNoteDetails)
        {
            totalProcessed++;
            
            if (string.IsNullOrEmpty(noteDetail.Id))
            {
                _logger.LogWarning("发现笔记ID为空，跳过去重处理");
                continue;
            }

            if (_processedNoteIds.Contains(noteDetail.Id))
            {
                duplicatesFound++;
                _logger.LogDebug("发现重复笔记ID: {NoteId}，已跳过", noteDetail.Id);
            }
            else
            {
                // 添加到去重缓存
                _processedNoteIds.Add(noteDetail.Id);
                
                // 标记数据来源端点
                noteDetail.SourceEndpoint = endpointType;
                _uniqueNoteDetails[noteDetail.Id] = noteDetail;
                
                newUnique++;
            }
        }

        // 更新统计信息
        _deduplicationStats.TotalProcessed += totalProcessed;
        _deduplicationStats.DuplicatesFound += duplicatesFound;
        _deduplicationStats.UniqueNotesCount = _uniqueNoteDetails.Count;

        if (duplicatesFound > 0 || _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogInformation("去重统计 [{Endpoint}]: 处理={Total}, 新增={New}, 重复={Duplicate}, 总唯一数={Unique}", 
                endpointType, totalProcessed, newUnique, duplicatesFound, _uniqueNoteDetails.Count);
        }
    }

    /// <summary>
    /// 获取笔记的来源端点
    /// </summary>
    private ApiEndpointType GetNoteSourceEndpoint(NoteDetail note)
    {
        // 如果笔记有SourceEndpoint属性，直接返回
        if (note.SourceEndpoint.HasValue)
            return note.SourceEndpoint.Value;

        // 否则返回Unknown
        return ApiEndpointType.Homefeed; // 默认值
    }

    /// <summary>
    /// 清理所有去重数据
    /// </summary>
    private void ClearDeduplicationData()
    {
        var prevCount = _uniqueNoteDetails.Count;
        _processedNoteIds.Clear();
        _uniqueNoteDetails.Clear();
        _deduplicationStats = new DeduplicationStats();
        
        _logger.LogDebug("清理了所有去重数据，原有唯一笔记数: {Count}", prevCount);
    }

    /// <summary>
    /// 清理指定端点的去重数据
    /// </summary>
    private void ClearDeduplicationDataForEndpoint(ApiEndpointType endpointType)
    {
        var notesToRemove = _uniqueNoteDetails.Values
            .Where(note => GetNoteSourceEndpoint(note) == endpointType)
            .Select(note => note.Id)
            .ToList();

        foreach (var noteId in notesToRemove)
        {
            _processedNoteIds.Remove(noteId);
            _uniqueNoteDetails.Remove(noteId);
        }

        _deduplicationStats.UniqueNotesCount = _uniqueNoteDetails.Count;
        
        _logger.LogDebug("清理了端点 {EndpointType} 的 {Count} 个去重数据", endpointType, notesToRemove.Count);
    }

    /// <summary>
    /// 获取去重统计信息
    /// </summary>
    public DeduplicationStats GetDeduplicationStats()
    {
        lock (_lock)
        {
            return new DeduplicationStats
            {
                TotalProcessed = _deduplicationStats.TotalProcessed,
                DuplicatesFound = _deduplicationStats.DuplicatesFound,
                UniqueNotesCount = _deduplicationStats.UniqueNotesCount
            };
        }
    }

    /// <summary>
    /// 获取所有去重后的笔记详情
    /// </summary>
    public List<NoteDetail> GetAllUniqueNoteDetails()
    {
        lock (_lock)
        {
            return _uniqueNoteDetails.Values.ToList();
        }
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

                // 写入临时交互状态缓存（基于权威API，而非DOM）
                foreach (var nd in noteDetails)
                {
                    try { InteractionStateCache.SetFromNoteDetail(nd); }
                    catch { /* 忽略缓存写入异常，不影响主流程 */ }
                }

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
            string? pageToken = null;
            string? searchId = null;

            // 捕获分页令牌与搜索ID
            if (rootElement.TryGetProperty("data", out var dataRoot))
            {
                if (dataRoot.TryGetProperty("page_token", out var pte))
                {
                    pageToken = pte.GetString();
                }
                if (dataRoot.TryGetProperty("search_id", out var sie))
                {
                    searchId = sie.GetString();
                }
            }

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
                            if (!string.IsNullOrEmpty(pageToken)) noteDetail.PageToken = pageToken;
                            if (!string.IsNullOrEmpty(searchId)) noteDetail.SearchId = searchId;
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
            var modelType = item.TryGetProperty("model_type", out var modelTypeEl) ? (modelTypeEl.GetString() ?? string.Empty) : string.Empty;

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
                MissingFields = [],
                ModelType = modelType
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

                if (userElement.TryGetProperty("xsec_token", out var userTokenEl))
                {
                    noteDetail.AuthorXsecToken = userTokenEl.GetString();
                }

                // 可用信息填充UserInfo
                noteDetail.UserInfo = new RecommendedUserInfo
                {
                    UserId = noteDetail.AuthorId,
                    Nickname = noteDetail.Author,
                    Avatar = noteDetail.AuthorAvatar,
                    // IsVerified 暂无字段，保持默认false
                    Description = string.Empty
                };
            }

            // 封面图片：note_card.cover.url_default 优先
            if (noteCard.ValueKind != JsonValueKind.Undefined &&
                noteCard.TryGetProperty("cover", out var coverElement))
            {
                string? urlDefaultStr = null;
                string? urlPreStr = null;
                int width = 0, height = 0;
                string fileId = string.Empty;

                if (coverElement.TryGetProperty("url_default", out var urlDefault))
                {
                    urlDefaultStr = urlDefault.GetString();
                }
                if (coverElement.TryGetProperty("url_pre", out var urlPre))
                {
                    urlPreStr = urlPre.GetString();
                }
                if (coverElement.TryGetProperty("width", out var wEl) && wEl.ValueKind == JsonValueKind.Number)
                {
                    width = wEl.GetInt32();
                }
                if (coverElement.TryGetProperty("height", out var hEl) && hEl.ValueKind == JsonValueKind.Number)
                {
                    height = hEl.GetInt32();
                }
                if (coverElement.TryGetProperty("file_id", out var fidEl))
                {
                    fileId = fidEl.GetString() ?? string.Empty;
                }

                // 选取封面图
                if (!string.IsNullOrEmpty(urlDefaultStr))
                {
                    noteDetail.CoverImage = urlDefaultStr;
                }
                else if (coverElement.TryGetProperty("url", out var coverUrl))
                {
                    noteDetail.CoverImage = coverUrl.GetString() ?? string.Empty;
                }
                else if (coverElement.ValueKind == JsonValueKind.String)
                {
                    noteDetail.CoverImage = coverElement.GetString() ?? string.Empty;
                }

                // 解析场景列表
                var scenes = new List<ImageSceneInfo>();
                if (coverElement.TryGetProperty("info_list", out var infoListEl) && infoListEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sc in infoListEl.EnumerateArray())
                    {
                        try
                        {
                            var sceneType = sc.TryGetProperty("image_scene", out var scType) ? (scType.GetString() ?? string.Empty) : string.Empty;
                            var sceneUrl = sc.TryGetProperty("url", out var scUrl) ? (scUrl.GetString() ?? string.Empty) : string.Empty;
                            if (!string.IsNullOrEmpty(sceneUrl))
                            {
                                scenes.Add(new ImageSceneInfo
                                {
                                    SceneType = sceneType,
                                    Url = sceneUrl
                                });
                            }
                        }
                        catch { }
                    }
                }

                noteDetail.CoverInfo = new RecommendedCoverInfo
                {
                    DefaultUrl = urlDefaultStr ?? string.Empty,
                    PreviewUrl = urlPreStr ?? string.Empty,
                    Width = width,
                    Height = height,
                    FileId = fileId,
                    Scenes = scenes
                };
            }

            // 笔记类型：type=video 或存在 note_card.video 则判定为视频，并提取视频关键字段
            if (noteCard.ValueKind != JsonValueKind.Undefined && noteCard.TryGetProperty("type", out var typeEl))
            {
                noteDetail.RawNoteType = typeEl.GetString();
            }

            if (string.Equals(noteDetail.RawNoteType, "video", StringComparison.OrdinalIgnoreCase))
            {
                noteDetail.Type = NoteType.Video;
            }
            else if (noteCard.ValueKind != JsonValueKind.Undefined && noteCard.TryGetProperty("video", out var videoEl))
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
            else if (noteDetail.Type == NoteType.Unknown)
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

            // 角标信息：用于提取发布时间（如 08-30、昨天、今天）
            if (noteCard.ValueKind != JsonValueKind.Undefined && noteCard.TryGetProperty("corner_tag_info", out var cornerEl) && cornerEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in cornerEl.EnumerateArray())
                {
                    try
                    {
                        var t = new CornerTag
                        {
                            Type = tag.TryGetProperty("type", out var tEl) ? (tEl.GetString() ?? string.Empty) : string.Empty,
                            Text = tag.TryGetProperty("text", out var xEl) ? (xEl.GetString() ?? string.Empty) : string.Empty
                        };
                        if (!string.IsNullOrEmpty(t.Type) || !string.IsNullOrEmpty(t.Text))
                        {
                            noteDetail.CornerTags.Add(t);
                        }
                    }
                    catch { }
                }

                // 解析发布时间（优先基于 publish_time 类型）
                var publishTag = noteDetail.CornerTags.FirstOrDefault(ct => string.Equals(ct.Type, "publish_time", StringComparison.OrdinalIgnoreCase));
                if (publishTag != null && string.IsNullOrEmpty(publishTag.Text) == false)
                {
                    if (TryParsePublishTime(publishTag.Text, out var pub))
                    {
                        noteDetail.PublishTime = pub;
                    }
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

    /// <summary>
    /// 解析 corner_tag 文本为发布时间
    /// 支持：MM-dd、今天、昨天、前天
    /// </summary>
    private static bool TryParsePublishTime(string text, out DateTime publishTime)
    {
        publishTime = DateTime.MinValue;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var now = DateTime.Now;
        text = text.Trim();

        // MM-dd 形式（无年份，默认当前年；若晚于今天则回退一年）
        if (System.Text.RegularExpressions.Regex.IsMatch(text, "^\\d{2}-\\d{2}$"))
        {
            if (DateTime.TryParse($"{now.Year}-{text}", out var dt))
            {
                if (dt > now) dt = dt.AddYears(-1);
                publishTime = dt;
                return true;
            }
        }

        // 今天/昨天/前天
        if (text is "今天") { publishTime = now.Date; return true; }
        if (text is "昨天") { publishTime = now.Date.AddDays(-1); return true; }
        if (text is "前天") { publishTime = now.Date.AddDays(-2); return true; }

        return false;
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

/// <summary>
/// 评论API响应处理器（/api/sns/web/v2/comment/page）
/// </summary>
public class CommentsResponseProcessor : IApiResponseProcessor
{
    private readonly ILogger _logger;

    public CommentsResponseProcessor(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<MonitoredApiResponse?> ProcessResponseAsync(string requestUrl, string responseBody)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(responseBody);
            var root = jsonDoc.RootElement;

            // 验证成功标志
            if (!root.TryGetProperty("success", out var succ) || !succ.GetBoolean())
            {
                _logger.LogDebug("评论API响应success=false");
                return null;
            }

            if (!root.TryGetProperty("data", out var dataEl))
            {
                _logger.LogDebug("评论API缺少data字段");
                return null;
            }

            // 提取分页标记
            string? cursor = dataEl.TryGetProperty("cursor", out var cEl) ? (cEl.GetString() ?? string.Empty) : string.Empty;
            bool hasMore = dataEl.TryGetProperty("has_more", out var hmEl) && hmEl.ValueKind == JsonValueKind.True;

            // 从URL中取note_id
            var noteId = ExtractQueryParam(requestUrl, "note_id") ?? string.Empty;

            var comments = new List<CommentInfo>();
            if (dataEl.TryGetProperty("comments", out var commentsEl) && commentsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var ce in commentsEl.EnumerateArray())
                {
                    var ci = ParseComment(ce, noteId);
                    if (ci != null) comments.Add(ci);
                }
            }

            return new MonitoredApiResponse
            {
                ResponseTime = DateTime.UtcNow,
                RequestUrl = requestUrl,
                ResponseBody = responseBody,
                ProcessedDataCount = comments.Count,
                ProcessedData = new Dictionary<string, object>
                {
                    ["NoteId"] = noteId,
                    ["Comments"] = comments,
                    ["Cursor"] = cursor ?? string.Empty,
                    ["HasMore"] = hasMore
                },
                EndpointType = ApiEndpointType.Comments
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理评论API响应失败");
            return null;
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    private static CommentInfo? ParseComment(JsonElement ce, string noteId)
    {
        try
        {
            var comment = new CommentInfo
            {
                Id = ce.TryGetProperty("id", out var idEl) ? (idEl.GetString() ?? string.Empty) : string.Empty,
                Content = ce.TryGetProperty("content", out var cntEl) ? (cntEl.GetString() ?? string.Empty) : string.Empty,
                IpLocation = ce.TryGetProperty("ip_location", out var ipEl) ? (ipEl.GetString() ?? string.Empty) : string.Empty,
                Liked = ce.TryGetProperty("liked", out var likedEl) && likedEl.ValueKind == JsonValueKind.True,
                NoteId = noteId
            };

            // 用户信息
            if (ce.TryGetProperty("user_info", out var uEl) && uEl.ValueKind == JsonValueKind.Object)
            {
                comment.Author = uEl.TryGetProperty("nickname", out var nnEl) ? (nnEl.GetString() ?? string.Empty) : string.Empty;
                comment.AuthorId = uEl.TryGetProperty("user_id", out var uidEl) ? (uidEl.GetString() ?? string.Empty) : string.Empty;
                comment.AuthorAvatar = uEl.TryGetProperty("image", out var imgEl) ? (imgEl.GetString() ?? string.Empty) : string.Empty;
                if (uEl.TryGetProperty("xsec_token", out var tkEl))
                {
                    comment.AuthorXsecToken = tkEl.GetString();
                }
            }

            // 点赞数（字符串或数字）
            if (ce.TryGetProperty("like_count", out var likeEl))
            {
                comment.LikeCount = ParseCountElement(likeEl);
            }

            // 时间（毫秒时间戳）
            if (ce.TryGetProperty("create_time", out var tsEl) && tsEl.ValueKind == JsonValueKind.Number)
            {
                try
                {
                    var ts = tsEl.GetInt64();
                    comment.PublishTime = DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime;
                }
                catch { }
            }

            // 图片
            if (ce.TryGetProperty("pictures", out var picsEl) && picsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var pe in picsEl.EnumerateArray())
                {
                    try
                    {
                        if (pe.TryGetProperty("info_list", out var infoEl) && infoEl.ValueKind == JsonValueKind.Array)
                        {
                            string? best = null;
                            foreach (var sc in infoEl.EnumerateArray())
                            {
                                if (sc.TryGetProperty("image_scene", out var sEl) && sEl.GetString() == "WB_DFT" && sc.TryGetProperty("url", out var uUrl))
                                {
                                    best = uUrl.GetString();
                                    break;
                                }
                            }
                            if (best == null)
                            {
                                foreach (var sc in infoEl.EnumerateArray())
                                {
                                    if (sc.TryGetProperty("url", out var u2))
                                    {
                                        best = u2.GetString(); if (!string.IsNullOrEmpty(best)) break;
                                    }
                                }
                            }
                            if (!string.IsNullOrEmpty(best)) comment.PictureUrls.Add(best);
                        }
                        else if (pe.TryGetProperty("url_default", out var dEl))
                        {
                            var u = dEl.GetString(); if (!string.IsNullOrEmpty(u)) comment.PictureUrls.Add(u);
                        }
                    }
                    catch { }
                }
            }

            // 子评论
            if (ce.TryGetProperty("sub_comments", out var subsEl) && subsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var se in subsEl.EnumerateArray())
                {
                    var sub = ParseComment(se, noteId);
                    if (sub != null) comment.Replies.Add(sub);
                }
            }

            // 标签
            if (ce.TryGetProperty("show_tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tagsEl.EnumerateArray())
                {
                    if (t.ValueKind == JsonValueKind.String)
                    {
                        var v = t.GetString(); if (!string.IsNullOrEmpty(v)) comment.ShowTags.Add(v);
                    }
                }
            }

            return comment;
        }
        catch
        {
            return null;
        }
    }

    private static int ParseCountElement(JsonElement el)
    {
        try
        {
            return el.ValueKind switch
            {
                JsonValueKind.Number => (int)el.GetInt64(),
                JsonValueKind.String => ParseChineseCount(el.GetString()),
                _ => 0
            };
        }
        catch { return 0; }
    }

    private static int ParseChineseCount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        raw = raw.Trim();
        try
        {
            if (raw.EndsWith("万", StringComparison.Ordinal))
                return (int)Math.Round(double.Parse(raw[..^1]) * 10000);
            if (raw.EndsWith("亿", StringComparison.Ordinal))
                return (int)Math.Round(double.Parse(raw[..^1]) * 100000000);
            return int.TryParse(raw, out var i) ? i : 0;
        }
        catch { return 0; }
    }

    private static string? ExtractQueryParam(string url, string name)
    {
        try
        {
            var qIdx = url.IndexOf('?');
            if (qIdx < 0 || qIdx == url.Length - 1) return null;
            var qs = url[(qIdx + 1)..];
            foreach (var part in qs.Split('&'))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && string.Equals(kv[0], name, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }
        }
        catch { }
        return null;
    }
}

/// <summary>
/// 点赞响应处理器（/api/sns/web/v1/note/like）。
/// 语义：当返回 { code:0, success:true } 且 data.new_like=true 时视为点赞成功。
/// </summary>
public sealed class LikeActionResponseProcessor : IApiResponseProcessor
{
    private readonly ILogger _logger;
    public LikeActionResponseProcessor(ILogger logger) => _logger = logger;

    public async Task<MonitoredApiResponse?> ProcessResponseAsync(string requestUrl, string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number && c.GetInt32() == 0
                     && root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            if (!ok) return null;

            bool newLike = false;
            int? likeCount = null;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                newLike = data.TryGetProperty("new_like", out var nl) && nl.ValueKind == JsonValueKind.True;
                if (data.TryGetProperty("like_count", out var lc) && lc.ValueKind == JsonValueKind.Number)
                {
                    likeCount = (int)lc.GetInt64();
                }
            }

            var noteId = ApiParserUtils.ExtractQueryParamSafe(requestUrl, "note_id") ?? string.Empty;
            if (!string.IsNullOrEmpty(noteId))
            {
                try { InteractionStateCache.ApplyLikeResult(noteId, liked: true, likeCount); }
                catch { }
            }

            return new MonitoredApiResponse
            {
                ResponseTime = DateTime.UtcNow,
                RequestUrl = requestUrl,
                ResponseBody = responseBody,
                ProcessedDataCount = 1,
                ProcessedData = new Dictionary<string, object>
                {
                    ["Action"] = "like",
                    ["Success"] = true,
                    ["NewLike"] = newLike,
                    ["NoteId"] = noteId,
                    ["LikeCount"] = likeCount ?? 0
                },
                EndpointType = ApiEndpointType.LikeNote
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理点赞响应失败");
            return null;
        }
        finally { await Task.CompletedTask; }
    }
}

/// <summary>
/// 取消点赞响应处理器（/api/sns/web/v1/note/dislike）。
/// 语义：当返回 { code:0, success:true } 即视为取消成功；可读取 data.like_count。
/// </summary>
public sealed class DislikeActionResponseProcessor : IApiResponseProcessor
{
    private readonly ILogger _logger;
    public DislikeActionResponseProcessor(ILogger logger) => _logger = logger;

    public async Task<MonitoredApiResponse?> ProcessResponseAsync(string requestUrl, string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number && c.GetInt32() == 0
                     && root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            if (!ok) return null;

            int? likeCount = null;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("like_count", out var lc) && lc.ValueKind == JsonValueKind.Number)
            {
                likeCount = (int)lc.GetInt64();
            }

            var noteId = ApiParserUtils.ExtractQueryParamSafe(requestUrl, "note_id") ?? string.Empty;
            if (!string.IsNullOrEmpty(noteId))
            {
                try { InteractionStateCache.ApplyLikeResult(noteId, liked: false, likeCount); }
                catch { }
            }

            return new MonitoredApiResponse
            {
                ResponseTime = DateTime.UtcNow,
                RequestUrl = requestUrl,
                ResponseBody = responseBody,
                ProcessedDataCount = 1,
                ProcessedData = new Dictionary<string, object>
                {
                    ["Action"] = "dislike",
                    ["Success"] = true,
                    ["LikeCount"] = likeCount ?? 0,
                    ["NoteId"] = noteId
                },
                EndpointType = ApiEndpointType.DislikeNote
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理取消点赞响应失败");
            return null;
        }
        finally { await Task.CompletedTask; }
    }
}

/// <summary>
/// 收藏响应处理器（/api/sns/web/v1/note/collect）。
/// </summary>
public sealed class CollectActionResponseProcessor : IApiResponseProcessor
{
    private readonly ILogger _logger;
    public CollectActionResponseProcessor(ILogger logger) => _logger = logger;

    public async Task<MonitoredApiResponse?> ProcessResponseAsync(string requestUrl, string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number && c.GetInt32() == 0
                     && root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            if (!ok) return null;

            var noteId = ApiParserUtils.ExtractQueryParamSafe(requestUrl, "note_id") ?? string.Empty;
            if (!string.IsNullOrEmpty(noteId))
            {
                try { InteractionStateCache.ApplyCollectResult(noteId, collected: true); } catch { }
            }

            return new MonitoredApiResponse
            {
                ResponseTime = DateTime.UtcNow,
                RequestUrl = requestUrl,
                ResponseBody = responseBody,
                ProcessedDataCount = 1,
                ProcessedData = new Dictionary<string, object>
                {
                    ["Action"] = "collect",
                    ["Success"] = true,
                    ["NoteId"] = noteId
                },
                EndpointType = ApiEndpointType.CollectNote
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理收藏响应失败");
            return null;
        }
        finally { await Task.CompletedTask; }
    }
}

/// <summary>
/// 取消收藏响应处理器（/api/sns/web/v1/note/uncollect）。
/// </summary>
public sealed class UncollectActionResponseProcessor : IApiResponseProcessor
{
    private readonly ILogger _logger;
    public UncollectActionResponseProcessor(ILogger logger) => _logger = logger;

    public async Task<MonitoredApiResponse?> ProcessResponseAsync(string requestUrl, string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number && c.GetInt32() == 0
                     && root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            if (!ok) return null;

            var noteId = ApiParserUtils.ExtractQueryParamSafe(requestUrl, "note_id") ?? string.Empty;
            if (!string.IsNullOrEmpty(noteId))
            {
                try { InteractionStateCache.ApplyCollectResult(noteId, collected: false); } catch { }
            }

            return new MonitoredApiResponse
            {
                ResponseTime = DateTime.UtcNow,
                RequestUrl = requestUrl,
                ResponseBody = responseBody,
                ProcessedDataCount = 1,
                ProcessedData = new Dictionary<string, object>
                {
                    ["Action"] = "uncollect",
                    ["Success"] = true,
                    ["NoteId"] = noteId
                },
                EndpointType = ApiEndpointType.UncollectNote
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理取消收藏响应失败");
            return null;
        }
        finally { await Task.CompletedTask; }
    }
}

/// <summary>
/// 发表评论响应处理器（/api/sns/web/v1/comment/post）。
/// </summary>
public sealed class CommentPostResponseProcessor : IApiResponseProcessor
{
    private readonly ILogger _logger;
    public CommentPostResponseProcessor(ILogger logger) => _logger = logger;

    public async Task<MonitoredApiResponse?> ProcessResponseAsync(string requestUrl, string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number && c.GetInt32() == 0
                     && root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            if (!ok) return null;

            string commentId = string.Empty;
            string noteId = string.Empty;
            string content = string.Empty;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("comment", out var ce) && ce.ValueKind == JsonValueKind.Object)
            {
                commentId = ce.TryGetProperty("id", out var idEl) ? (idEl.GetString() ?? string.Empty) : string.Empty;
                noteId = ce.TryGetProperty("note_id", out var nidEl) ? (nidEl.GetString() ?? string.Empty) : string.Empty;
                content = ce.TryGetProperty("content", out var ctEl) ? (ctEl.GetString() ?? string.Empty) : string.Empty;
            }

            if (string.IsNullOrEmpty(noteId)) noteId = ApiParserUtils.ExtractQueryParamSafe(requestUrl, "note_id") ?? string.Empty;
            if (!string.IsNullOrEmpty(noteId))
            {
                try { InteractionStateCache.ApplyCommentDelta(noteId, delta: +1); } catch { }
            }

            return new MonitoredApiResponse
            {
                ResponseTime = DateTime.UtcNow,
                RequestUrl = requestUrl,
                ResponseBody = responseBody,
                ProcessedDataCount = 1,
                ProcessedData = new Dictionary<string, object>
                {
                    ["Action"] = "comment_post",
                    ["Success"] = true,
                    ["CommentId"] = commentId,
                    ["NoteId"] = noteId,
                    ["Content"] = content
                },
                EndpointType = ApiEndpointType.CommentPost
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理发表评论响应失败");
            return null;
        }
        finally { await Task.CompletedTask; }
    }
}

/// <summary>
/// 删除评论响应处理器（/api/sns/web/v1/comment/delete）。
/// </summary>
public sealed class CommentDeleteResponseProcessor : IApiResponseProcessor
{
    private readonly ILogger _logger;
    public CommentDeleteResponseProcessor(ILogger logger) => _logger = logger;

    public async Task<MonitoredApiResponse?> ProcessResponseAsync(string requestUrl, string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number && c.GetInt32() == 0
                     && root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            if (!ok) return null;

            var noteId = ApiParserUtils.ExtractQueryParamSafe(requestUrl, "note_id") ?? string.Empty;
            if (!string.IsNullOrEmpty(noteId))
            {
                try { InteractionStateCache.ApplyCommentDelta(noteId, delta: -1); } catch { }
            }

            return new MonitoredApiResponse
            {
                ResponseTime = DateTime.UtcNow,
                RequestUrl = requestUrl,
                ResponseBody = responseBody,
                ProcessedDataCount = 1,
                ProcessedData = new Dictionary<string, object>
                {
                    ["Action"] = "comment_delete",
                    ["Success"] = true,
                    ["NoteId"] = noteId
                },
                EndpointType = ApiEndpointType.CommentDelete
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理删除评论响应失败");
            return null;
        }
        finally { await Task.CompletedTask; }
    }
}

// 轻量 URL 查询参数解析工具（处理 action 端点 note_id 提取）
internal static class ApiParserUtils
{
    public static string? ExtractQueryParamSafe(string url, string name)
    {
        try
        {
            var qIdx = url.IndexOf('?');
            if (qIdx < 0 || qIdx == url.Length - 1) return null;
            var qs = url[(qIdx + 1)..];
            foreach (var part in qs.Split('&'))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && string.Equals(kv[0], name, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }
        }
        catch { }
        return null;
    }
}

/// <summary>
/// 数据去重统计信息
/// </summary>
public class DeduplicationStats
{
    /// <summary>
    /// 总处理的笔记数量
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// 发现的重复数量
    /// </summary>
    public int DuplicatesFound { get; set; }

    /// <summary>
    /// 当前唯一笔记数量
    /// </summary>
    public int UniqueNotesCount { get; set; }

    /// <summary>
    /// 去重率（重复数量/总处理数量）
    /// </summary>
    public double DeduplicationRate => TotalProcessed > 0 ? (double)DuplicatesFound / TotalProcessed : 0.0;

    public override string ToString()
    {
        return $"总处理: {TotalProcessed}, 重复: {DuplicatesFound}, 唯一: {UniqueNotesCount}, 去重率: {DeduplicationRate:P2}";
    }
}
