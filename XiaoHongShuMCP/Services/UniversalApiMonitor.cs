using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 通用API监听器 - 支持多端点动态监听
/// 支持推荐、笔记详情、搜索等不同功能的API端点
/// </summary>
public class UniversalApiMonitor : IDisposable
{
    private readonly ILogger<UniversalApiMonitor> _logger;
    private readonly Dictionary<ApiEndpointType, List<MonitoredApiResponse>> _monitoredResponses;
    private readonly Dictionary<ApiEndpointType, IApiResponseProcessor> _processors;
    private readonly object _lock = new();
    private IBrowserContext? _context;
    private bool _isMonitoring;
    private HashSet<ApiEndpointType> _activeEndpoints;

    /// <summary>
    /// API端点类型枚举
    /// </summary>
    public enum ApiEndpointType
    {
        Homefeed,       // 推荐笔记 /api/sns/web/v1/homefeed
        Feed,           // 笔记详情 /api/sns/web/v1/feed
        SearchNotes     // 搜索笔记 /api/sns/web/v1/search/notes
    }

    public UniversalApiMonitor(ILogger<UniversalApiMonitor> logger)
    {
        _logger = logger;
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
    /// 设置通用API监听器
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="endpointsToMonitor">要监听的端点类型</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>设置是否成功</returns>
    public async Task<bool> SetupMonitorAsync(IPage page, 
        HashSet<ApiEndpointType> endpointsToMonitor, 
        TimeSpan? timeout = null)
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
    /// 处理响应事件 - 智能路由到不同端点处理器
    /// </summary>
    private async void OnResponseReceived(object? sender, IResponse response)
    {
        try
        {
            // 识别API端点类型
            var endpointType = IdentifyApiEndpoint(response.Url);
            if (endpointType == null || !_activeEndpoints.Contains(endpointType.Value))
                return;

            _logger.LogDebug("检测到 {EndpointType} API响应: {Url}", endpointType, response.Url);

            // 只处理成功的响应
            if (response.Status != 200)
            {
                _logger.LogDebug("{EndpointType} API响应状态码不是200: {Status}", endpointType, response.Status);
                return;
            }

            var responseBody = await response.TextAsync();
            
            // 使用对应的处理器处理响应
            if (_processors.TryGetValue(endpointType.Value, out var processor))
            {
                var processedResponse = await processor.ProcessResponseAsync(response.Url, responseBody);
                if (processedResponse != null)
                {
                    lock (_lock)
                    {
                        _monitoredResponses[endpointType.Value].Add(processedResponse);
                    }

                    _logger.LogDebug("成功处理 {EndpointType} API响应，数据项数: {Count}", 
                        endpointType, processedResponse.ProcessedDataCount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理API响应时发生错误: {Url}", response.Url);
        }
    }

    /// <summary>
    /// 识别API端点类型
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
    /// 等待指定端点的API响应
    /// </summary>
    public async Task<bool> WaitForResponsesAsync(ApiEndpointType endpointType, 
        int expectedCount = 1, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        _logger.LogDebug("等待 {EndpointType} API响应: 期望数量={ExpectedCount}, 超时={Timeout}s", 
            endpointType, expectedCount, timeout.Value.TotalSeconds);

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
    /// 获取指定端点监听到的笔记详情
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
    /// 获取指定端点监听到的原始响应数据
    /// </summary>
    public List<MonitoredApiResponse> GetRawResponses(ApiEndpointType endpointType)
    {
        lock (_lock)
        {
            return _monitoredResponses[endpointType].ToList();
        }
    }

    /// <summary>
    /// 清理指定端点的监听数据
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
    /// 停止API监听
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
    public UniversalApiMonitor.ApiEndpointType EndpointType { get; set; }
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
                    EndpointType = UniversalApiMonitor.ApiEndpointType.Homefeed
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
                    EndpointType = UniversalApiMonitor.ApiEndpointType.Feed
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
            // 这里需要根据搜索API的实际响应格式来实现
            // 暂时使用通用的JSON解析
            var jsonDoc = JsonDocument.Parse(responseBody);
            
            // TODO: 实现具体的搜索响应解析逻辑
            await Task.CompletedTask;
            
            return new MonitoredApiResponse
            {
                ResponseTime = DateTime.UtcNow,
                RequestUrl = requestUrl,
                ResponseBody = responseBody,
                ProcessedDataCount = 0, // 待实现
                ProcessedNoteDetails = [],
                EndpointType = UniversalApiMonitor.ApiEndpointType.SearchNotes
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理搜索API响应失败");
            return null;
        }
    }
}