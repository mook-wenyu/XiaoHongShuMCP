using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Text.Json;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 小红书 Feed API 监听器 - 反检测优化版
/// 使用被动监听模式替代主动监听，降低检测风险
/// 监听真实的 https://edith.xiaohongshu.com/api/sns/web/v1/feed 接口
/// 实现拟人操作与真实API数据收集的完美结合
/// </summary>
public class FeedApiMonitor : IDisposable
{
    private readonly ILogger<FeedApiMonitor> _logger;
    private readonly List<MonitoredFeedResponse> _monitoredResponses;
    private readonly object _lock = new();
    private IBrowserContext? _context;
    private bool _isMonitoring;
    private TaskCompletionSource<bool>? _monitorSetupCompletion;

    /// <summary>
    /// 真实 Feed API 端点模式
    /// </summary>
    private const string FEED_API_ENDPOINT_PATTERN = "**/api/sns/web/v1/feed";

    public FeedApiMonitor(ILogger<FeedApiMonitor> logger)
    {
        _logger = logger;
        _monitoredResponses = new List<MonitoredFeedResponse>();
    }

    /// <summary>
    /// 设置被动 Feed API 监听器
    /// 使用 Response 事件监听 https://edith.xiaohongshu.com/api/sns/web/v1/feed 请求和响应
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>设置是否成功</returns>
    public async Task<bool> SetupMonitorAsync(IPage page, TimeSpan? timeout = null)
    {
        try
        {
            _context = page.Context;
            _monitorSetupCompletion = new TaskCompletionSource<bool>();
            
            _logger.LogDebug("正在设置被动 Feed API 监听器: {ApiPattern}", FEED_API_ENDPOINT_PATTERN);

            // 设置被动响应监听器
            _context.Response += OnResponseReceived;
            
            _isMonitoring = true;
            _monitorSetupCompletion.SetResult(true);

            _logger.LogInformation("被动 Feed API 监听器设置完成: {Pattern}", FEED_API_ENDPOINT_PATTERN);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置 Feed API 监听器失败");
            _monitorSetupCompletion?.SetResult(false);
            return false;
        }
    }

    /// <summary>
    /// 处理响应事件 - 被动监听 Feed API 响应
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="response">响应对象</param>
    private async void OnResponseReceived(object? sender, IResponse response)
    {
        try
        {
            // 检查是否为目标 Feed API
            if (!IsFeedApiUrl(response.Url))
                return;

            _logger.LogDebug("检测到 Feed API 响应: {Url}", response.Url);

            // 只处理成功的响应
            if (response.Status != 200)
            {
                _logger.LogDebug("Feed API 响应状态码不是 200: {Status}", response.Status);
                return;
            }

            var responseBody = await response.TextAsync();
            var requestUrl = response.Url;
            
            // 获取请求体 - 在被动监听模式下无法直接获取
            var requestBody = string.Empty;
            
            // 解析 Feed API 响应
            var feedApiResponse = JsonSerializer.Deserialize<FeedApiResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (feedApiResponse != null && FeedApiConverter.IsValidFeedResponse(feedApiResponse))
            {
                // 在被动监听模式下无法获取请求体，所以 source_note_id 设置为空
                var sourceNoteId = string.Empty;
                
                var monitoredResponse = new MonitoredFeedResponse
                {
                    ResponseTime = DateTime.UtcNow,
                    RequestUrl = requestUrl,
                    RequestBody = requestBody,
                    ResponseData = feedApiResponse,
                    RawResponse = responseBody,
                    SourceNoteId = sourceNoteId
                };
                
                lock (_lock)
                {
                    _monitoredResponses.Add(monitoredResponse);
                }

                _logger.LogDebug("成功监听 Feed API 响应: 笔记数={Count}", 
                    feedApiResponse.Data?.Items?.Count ?? 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理 Feed API 响应时发生错误: {Url}", response.Url);
        }
    }

    /// <summary>
    /// 检查 URL 是否为 Feed API
    /// </summary>
    /// <param name="url">URL 地址</param>
    /// <returns>是否为 Feed API</returns>
    private static bool IsFeedApiUrl(string url)
    {
        return url.Contains("/api/sns/web/v1/feed");
    }

    /// <summary>
    /// 等待监听到指定数量的 Feed API 响应
    /// </summary>
    /// <param name="expectedCount">期望的响应数量</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>是否成功监听到足够的响应</returns>
    public async Task<bool> WaitForMonitoredResponsesAsync(int expectedCount = 1, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        _logger.LogDebug("等待监听 Feed API 响应: 期望数量={ExpectedCount}, 超时={Timeout}s", 
            expectedCount, timeout.Value.TotalSeconds);

        while (DateTime.UtcNow - startTime < timeout)
        {
            lock (_lock)
            {
                var validResponsesCount = _monitoredResponses.Count(r => 
                    r.ResponseData != null && FeedApiConverter.IsValidFeedResponse(r.ResponseData));
                    
                if (validResponsesCount >= expectedCount)
                {
                    _logger.LogDebug("成功监听到足够的有效 Feed API 响应: 实际数量={ActualCount}", 
                        validResponsesCount);
                    return true;
                }
            }

            await Task.Delay(100);
        }

        lock (_lock)
        {
            var validCount = _monitoredResponses.Count(r => 
                r.ResponseData != null && FeedApiConverter.IsValidFeedResponse(r.ResponseData));
            _logger.LogWarning("等待 Feed API 响应超时: 期望={ExpectedCount}, 有效={ValidCount}, 总计={TotalCount}", 
                expectedCount, validCount, _monitoredResponses.Count);
        }

        return false;
    }

    /// <summary>
    /// 获取监听到的 Feed API 响应数据并转换为 NoteDetail
    /// 使用真实API数据结构进行转换
    /// </summary>
    /// <returns>转换后的笔记详情列表</returns>
    public List<NoteDetail> GetMonitoredNoteDetails()
    {
        lock (_lock)
        {
            var noteDetails = FeedApiConverter.ConvertBatchToNoteDetails(_monitoredResponses);
            
            _logger.LogInformation("从监听的 {ResponseCount} 个 Feed API 响应中转换出 {NoteCount} 个笔记详情", 
                _monitoredResponses.Count, noteDetails.Count);
                
            return noteDetails;
        }
    }

    /// <summary>
    /// 获取最后一个监听到的笔记详情
    /// 基于真实 Feed API 数据转换
    /// </summary>
    /// <returns>最新的笔记详情，如果没有则返回null</returns>
    public NoteDetail? GetLatestMonitoredNoteDetail()
    {
        var allDetails = GetMonitoredNoteDetails();
        return allDetails.LastOrDefault();
    }
    
    /// <summary>
    /// 清理监听到的响应数据
    /// </summary>
    public void ClearMonitoredData()
    {
        lock (_lock)
        {
            var previousCount = _monitoredResponses.Count;
            _monitoredResponses.Clear();
            _logger.LogDebug("清理了 {Count} 个监听的 Feed API 响应", previousCount);
        }
    }

    /// <summary>
    /// 停止 Feed API 监听
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        if (_context != null && _isMonitoring)
        {
            try
            {
                _context.Response -= OnResponseReceived;
                _isMonitoring = false;
                _logger.LogDebug("已停止 Feed API 监听");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "停止 Feed API 监听时发生错误");
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
        _monitorSetupCompletion?.TrySetResult(false);
    }
}
