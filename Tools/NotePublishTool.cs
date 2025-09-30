using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Browser.Playwright;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Tools;

/// <summary>
/// 中文：发布笔记工具，支持上传图片、填写标题正文并暂存离开。
/// English: Note publishing tool that uploads images, fills title and content, then saves draft and leaves.
/// </summary>
[McpServerToolType]
public sealed class NotePublishTool
{
    private readonly IBrowserAutomationService _browserAutomation;
    private readonly IPlaywrightSessionManager _sessionManager;
    private readonly IHumanizedActionService _humanizedActionService;
    private readonly ILogger<NotePublishTool> _logger;

    public NotePublishTool(
        IBrowserAutomationService browserAutomation,
        IPlaywrightSessionManager sessionManager,
        IHumanizedActionService humanizedActionService,
        ILogger<NotePublishTool> logger)
    {
        _browserAutomation = browserAutomation ?? throw new ArgumentNullException(nameof(browserAutomation));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _humanizedActionService = humanizedActionService ?? throw new ArgumentNullException(nameof(humanizedActionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [McpServerTool(Name = "xhs_publish_note"), Description("发布笔记（上传图片、填写标题正文、暂存离开）| Publish note (upload image, fill title/content, save draft and leave)")]
    public async Task<OperationResult<NotePublishResult>> PublishNoteAsync(
        [Description("图片文件路径 | Image file path")] string imagePath,
        [Description("笔记标题，不填则使用默认标题 | Note title, defaults to generic title")] string? noteTitle = null,
        [Description("笔记正文，不填则使用默认正文 | Note content, defaults to generic content")] string? noteContent = null,
        [Description("浏览器键，user 表示用户配置 | Browser key: 'user' for user profile")] string? browserKey = null,
        [Description("行为档案键，默认 default | Behavior profile key")] string? behaviorProfile = null,
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        var normalizedBrowserKey = browserKey ?? "user";
        var normalizedBehaviorProfile = behaviorProfile ?? "default";

        try
        {
            _logger.LogInformation("[NotePublishTool] 开始发布笔记 browserKey={BrowserKey} imagePath={ImagePath} title={Title}",
                normalizedBrowserKey, imagePath, noteTitle ?? "<default>");

            // 1. 确保浏览器配置存在
            var profile = await _browserAutomation.EnsureProfileAsync(normalizedBrowserKey, null, cancellationToken).ConfigureAwait(false);

            // 2. 获取页面上下文
            var pageContext = await _browserAutomation.EnsurePageContextAsync(normalizedBrowserKey, cancellationToken).ConfigureAwait(false);

            // 3. 导航到发布页面（creator.xiaohongshu.com）
            _logger.LogInformation("[NotePublishTool] 导航到发布页面");
            await pageContext.Page.GotoAsync("https://creator.xiaohongshu.com/publish/publish?source=official", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            }).ConfigureAwait(false);

            // 等待页面加载
            await Task.Delay(1500, cancellationToken).ConfigureAwait(false);

            // 4. 执行人性化发布动作脚本
            var request = new HumanizedActionRequest(
                Keywords: Array.Empty<string>(),
                PortraitId: null,
                CommentText: null,
                BrowserKey: normalizedBrowserKey,
                RequestId: null,
                BehaviorProfile: normalizedBehaviorProfile,
                ImagePath: imagePath,
                NoteTitle: noteTitle,
                NoteContent: noteContent);

            var outcome = await _humanizedActionService.ExecuteAsync(request, HumanizedActionKind.PublishNote, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("[NotePublishTool] 发布笔记完成 success={Success} status={Status}", outcome.Success, outcome.Status);

            var metadata = outcome.Metadata is Dictionary<string, string> dict
                ? dict
                : new Dictionary<string, string>(outcome.Metadata, StringComparer.OrdinalIgnoreCase);

            return outcome.Success
                ? OperationResult<NotePublishResult>.Ok(
                    new NotePublishResult(imagePath, noteTitle, noteContent, "已暂存并离开发布页面"),
                    outcome.Status,
                    metadata)
                : OperationResult<NotePublishResult>.Fail(outcome.Status, outcome.Message ?? "发布失败", metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NotePublishTool] 发布笔记失败 browserKey={BrowserKey} imagePath={ImagePath}", normalizedBrowserKey, imagePath);
            return OperationResult<NotePublishResult>.Fail("ERR_PUBLISH_EXCEPTION", ex.Message);
        }
    }
}

public sealed record NotePublishResult(
    string ImagePath,
    string? Title,
    string? Content,
    string Message);