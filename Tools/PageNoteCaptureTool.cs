using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Notes;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Tools;

/// <summary>
/// 中文：页面笔记采集工具，从当前打开的页面采集指定数量笔记并导出 CSV。
/// English: Page note capture tool that collects specified number of notes from currently open page and exports to CSV.
/// </summary>
[McpServerToolType]
public sealed class PageNoteCaptureTool
{
    private readonly IPageNoteCaptureService _captureService;
    private readonly ILogger<PageNoteCaptureTool> _logger;

    public PageNoteCaptureTool(
        IPageNoteCaptureService captureService,
        ILogger<PageNoteCaptureTool> logger)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [McpServerTool(Name = "xhs_capture_page_notes"), Description("从当前页面采集笔记并导出 CSV（不执行导航） | Capture notes from current page and export to CSV (no navigation)")]
    public Task<OperationResult<PageNoteCaptureToolResult>> CaptureAsync(
        [Description("页面笔记采集请求参数 | Request payload for page note capture")] PageNoteCaptureToolRequest request,
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var requestId = Guid.NewGuid().ToString("N");

        return ServerToolExecutor.TryAsync(
            _logger,
            nameof(PageNoteCaptureTool),
            nameof(CaptureAsync),
            requestId,
            async () => await ExecuteAsync(request, requestId, cancellationToken).ConfigureAwait(false),
            (ex, rid) => OperationResult<PageNoteCaptureToolResult>.Fail(
                ServerToolExecutor.MapExceptionCode(ex),
                ex.Message,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["requestId"] = rid ?? string.Empty
                }));
    }

    private async Task<OperationResult<PageNoteCaptureToolResult>> ExecuteAsync(
        PageNoteCaptureToolRequest request,
        string requestId,
        CancellationToken cancellationToken)
    {
        const string DefaultOutputDirectory = "./logs/page-note-capture";

        var browserKey = NormalizeBrowserKey(request.BrowserKey);
        var targetCount = NormalizeTargetCount(request.TargetCount);

        var context = new PageNoteCaptureContext(
            browserKey,
            targetCount,
            DefaultOutputDirectory);

        try
        {
            var result = await _captureService.CaptureAsync(context, cancellationToken).ConfigureAwait(false);

            var toolResult = new PageNoteCaptureToolResult(
                result.CsvPath,
                result.CollectedCount);

            var metadata = new Dictionary<string, string>(result.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["requestId"] = requestId
            };

            _logger.LogInformation("[PageNoteCaptureTool] 成功采集 {Count} 条笔记 csv={Csv}", result.CollectedCount, result.CsvPath);

            return OperationResult<PageNoteCaptureToolResult>.Ok(toolResult, metadata: metadata);
        }
        catch (InvalidOperationException ex)
        {
            // 页面类型错误（不是列表页）
            _logger.LogWarning(ex, "[PageNoteCaptureTool] 页面类型错误 browserKey={BrowserKey}", browserKey);
            return OperationResult<PageNoteCaptureToolResult>.Fail(
                "ERR_INVALID_PAGE_TYPE",
                ex.Message,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["requestId"] = requestId
                });
        }
    }

    private static string NormalizeBrowserKey(string? browserKey)
        => string.IsNullOrWhiteSpace(browserKey) ? "user" : browserKey.Trim();

    private static int NormalizeTargetCount(int value)
        => Math.Clamp(value <= 0 ? 20 : value, 1, 200);
}

/// <summary>
/// 中文：页面笔记采集工具请求参数。
/// English: Request parameters for page note capture tool.
/// </summary>
/// <param name="TargetCount">目标采集数量，默认 20 条 | Target number of notes to collect, default 20.</param>
/// <param name="BrowserKey">浏览器键，默认 user | Browser key, default 'user'.</param>
public sealed record PageNoteCaptureToolRequest(
    [property: Description("目标采集数量，默认 20 条 | Target number of notes to collect, default 20")] int TargetCount = 20,
    [property: Description("浏览器键，user 表示用户配置 | Browser key: 'user' for user profile")] string BrowserKey = "");

/// <summary>
/// 中文：页面笔记采集工具返回结果。
/// English: Result of page note capture tool.
/// </summary>
/// <param name="CsvPath">CSV 文件路径 | CSV file path.</param>
/// <param name="CollectedCount">实际采集数量 | Actual number of notes collected.</param>
public sealed record PageNoteCaptureToolResult(
    [property: Description("CSV 文件路径 | CSV file path")] string CsvPath,
    [property: Description("实际采集数量 | Actual number of notes collected")] int CollectedCount);
