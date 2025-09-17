using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Humanization;
using HushOps.Core.Runtime.Playwright;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Services.CommentFlow;

/// <summary>
/// 中文：评论工作流，实现基于关键词的拟人化评论流程。
/// </summary>
internal sealed class CommentWorkflow : ICommentWorkflow
{
    private readonly ILogger<CommentWorkflow> _logger;
    private readonly IBrowserManager _browserManager;
    private readonly IAccountManager _accountManager;
    private readonly IPageStateGuard _pageStateGuard;
    private readonly IPageGuardian _pageGuardian;
    private readonly IInteractionExecutor _interactionExecutor;
    private readonly IFeedbackCoordinator _feedbackCoordinator;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly IDomElementManager _domElementManager;
    private readonly XhsSettings.SearchTimeoutsSection _timeouts;
    private readonly XhsSettings.DetailMatchSection _detailMatch;

    public CommentWorkflow(
        ILogger<CommentWorkflow> logger,
        IBrowserManager browserManager,
        IAccountManager accountManager,
        IPageStateGuard pageStateGuard,
        IPageGuardian pageGuardian,
        IInteractionExecutor interactionExecutor,
        IFeedbackCoordinator feedbackCoordinator,
        IHumanizedInteractionService humanizedInteraction,
        IDomElementManager domElementManager,
        XhsSettings.SearchTimeoutsSection timeouts,
        XhsSettings.DetailMatchSection detailMatch)
    {
        _logger = logger;
        _browserManager = browserManager;
        _accountManager = accountManager;
        _pageStateGuard = pageStateGuard;
        _pageGuardian = pageGuardian;
        _interactionExecutor = interactionExecutor;
        _feedbackCoordinator = feedbackCoordinator;
        _humanizedInteraction = humanizedInteraction;
        _domElementManager = domElementManager;
        _timeouts = timeouts;
        _detailMatch = detailMatch;
    }

    /// <inheritdoc />
    public async Task<OperationResult<CommentResult>> PostCommentAsync(string keyword, string content, CancellationToken ct)
    {
        _logger.LogInformation("开始发布评论：关键词={Keyword}，内容长度={Length}", keyword, content?.Length ?? 0);

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return OperationResult<CommentResult>.Fail("关键词不能为空", ErrorType.ValidationError, "EMPTY_KEYWORD");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return OperationResult<CommentResult>.Fail("评论内容不能为空", ErrorType.ValidationError, "EMPTY_CONTENT");
        }

        if (!await _accountManager.IsLoggedInAsync().ConfigureAwait(false))
        {
            return OperationResult<CommentResult>.Fail("当前账号未登录", ErrorType.LoginRequired, "ACCOUNT_NOT_LOGGED_IN");
        }

        var page = await _browserManager.GetPageAsync().ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var status = await _pageGuardian.InspectAsync(page, PageType.NoteDetail, ct).ConfigureAwait(false);
            if (status.PageType == PageType.NoteDetail && status.IsPageReady)
            {
                var result = await CommentOnDetailPageAsync(page, keyword, content, stopwatch, ct).ConfigureAwait(false);
                stopwatch.Stop();
                return result;
            }

            var ensured = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(page).ConfigureAwait(false);
            if (!ensured)
            {
                return OperationResult<CommentResult>.Fail("无法导航至探索/搜索页面", ErrorType.NavigationError, "ENTRY_PAGE_NOT_AVAILABLE");
            }

            var matches = await FindMatchingNotesAsync(page, keyword, ct).ConfigureAwait(false);
            if (matches.Count == 0)
            {
                return OperationResult<CommentResult>.Fail($"未找到匹配关键词的笔记：{keyword}", ErrorType.ElementNotFound, "NO_MATCHING_NOTES");
            }

            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause, ct).ConfigureAwait(false);
            await _humanizedInteraction.HumanClickAsync(matches[0]).ConfigureAwait(false);
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading, ct).ConfigureAwait(false);

            page = await _browserManager.GetPageAsync().ConfigureAwait(false);
            var detailStatus = await _pageGuardian.InspectAsync(page, PageType.NoteDetail, ct).ConfigureAwait(false);
            if (!detailStatus.IsPageReady)
            {
                return OperationResult<CommentResult>.Fail("打开笔记详情失败", ErrorType.NavigationError, "DETAIL_PAGE_NOT_READY");
            }

            var finalResult = await CommentOnDetailPageAsync(page, keyword, content, stopwatch, ct).ConfigureAwait(false);
            stopwatch.Stop();
            return finalResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "评论发布流程异常：关键词={Keyword}", keyword);
            return OperationResult<CommentResult>.Fail($"发布评论异常: {ex.Message}", ErrorType.BrowserError, "POST_COMMENT_OPERATION_EXCEPTION");
        }
    }

    private async Task<OperationResult<CommentResult>> CommentOnDetailPageAsync(IPage page, string keyword, string content, Stopwatch stopwatch, CancellationToken ct)
    {
        if (!await DoesCurrentDetailMatchKeywordsAsync(page, keyword).ConfigureAwait(false))
        {
            return OperationResult<CommentResult>.Fail("当前详情页与关键词不匹配", ErrorType.ElementNotFound, "DETAIL_NOT_MATCHED");
        }

        if (!await _pageGuardian.EnsureCommentAreaReadyAsync(page, ct).ConfigureAwait(false))
        {
            return OperationResult<CommentResult>.Fail("无法激活评论区域", ErrorType.ElementNotFound, "COMMENT_AREA_NOT_ACTIVATED");
        }

        var endpoints = new[] { ApiEndpointType.CommentPost };
        _feedbackCoordinator.Initialize(page, endpoints);
        _feedbackCoordinator.Reset(ApiEndpointType.CommentPost);

        var autoPage = PlaywrightAutoFactory.Wrap(page);
        var draft = new CommentDraft(content, false, null, null);
        await _interactionExecutor.InputCommentAsync(autoPage, draft, ct).ConfigureAwait(false);
        var submit = await _interactionExecutor.SubmitCommentAsync(autoPage, ct).ConfigureAwait(false);
        if (!submit.Success)
        {
            stopwatch.Stop();
            _feedbackCoordinator.Audit("发表评论", keyword, new FeedbackContext(false, false, stopwatch.Elapsed, submit.Message));
            return OperationResult<CommentResult>.Fail(submit.Message, ErrorType.ElementNotFound, "COMMENT_SUBMIT_FAILED");
        }

        var feedback = await _feedbackCoordinator.ObserveAsync(ApiEndpointType.CommentPost, ct).ConfigureAwait(false);
        stopwatch.Stop();

        var apiConfirmed = feedback.Success;
        _feedbackCoordinator.Audit("发表评论", keyword, new FeedbackContext(false, apiConfirmed, stopwatch.Elapsed, feedback.Message));

        if (!feedback.Success)
        {
            return OperationResult<CommentResult>.Fail("评论失败：未捕获网络确认", ErrorType.NetworkError, "COMMENT_POST_API_NOT_CONFIRMED");
        }

        var commentId = ExtractCommentId(feedback) ?? Guid.NewGuid().ToString();
        await _humanizedInteraction.HumanBetweenActionsDelayAsync(ct).ConfigureAwait(false);

        return OperationResult<CommentResult>.Ok(new CommentResult(true, "评论发布成功", commentId));
    }

    private async Task<IReadOnlyList<IElementHandle>> FindMatchingNotesAsync(IPage page, string keyword, CancellationToken ct)
    {
        var results = new List<IElementHandle>();
        var processedIds = new HashSet<string>();
        var attempts = 0;

        while (results.Count == 0 && attempts < 5)
        {
            ct.ThrowIfCancellationRequested();
            var matches = await SearchCurrentAreaAsync(page, keyword).ConfigureAwait(false);
            foreach (var match in matches)
            {
                var noteId = await ExtractNoteIdAsync(match).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(noteId) && processedIds.Add(noteId))
                {
                    results.Add(match);
                    break;
                }
            }

            if (results.Count > 0) break;

            await _humanizedInteraction.HumanScrollAsync(page, cancellationToken: ct).ConfigureAwait(false);
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.VirtualListUpdate, ct).ConfigureAwait(false);
            attempts++;
        }

        return results;
    }

    private async Task<IReadOnlyList<IElementHandle>> SearchCurrentAreaAsync(IPage page, string keyword)
    {
        var noteSelectors = _domElementManager.GetSelectors("NoteItem");
        var candidates = new List<IElementHandle>();

        foreach (var selector in noteSelectors)
        {
            try
            {
                var elements = await page.QuerySelectorAllAsync(selector).ConfigureAwait(false);
                if (elements.Count > 0)
                {
                    candidates.AddRange(elements);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("使用选择器 {Selector} 搜索笔记失败：{Error}", selector, ex.Message);
            }
        }

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return candidates;
        }

        var filtered = new List<IElementHandle>();
        foreach (var element in candidates)
        {
            try
            {
                var text = await ExtractNoteTextAsync(element).ConfigureAwait(false);
                if (string.IsNullOrEmpty(text)) continue;
                if (KeywordMatcher.Matches(text, keyword, new KeywordMatchOptions
                    {
                        UseFuzzy = _detailMatch.UseFuzzy,
                        IgnoreSpaces = _detailMatch.IgnoreSpaces,
                        MaxDistanceCap = _detailMatch.MaxDistanceCap,
                        TokenCoverageThreshold = _detailMatch.TokenCoverageThreshold
                    }))
                {
                    filtered.Add(element);
                }
            }
            catch
            {
                // ignore element failure
            }
        }

        return filtered;
    }

    private async Task<string> ExtractNoteTextAsync(IElementHandle element)
    {
        var titleHandle = await element.QuerySelectorAsync(".title, .note-title").ConfigureAwait(false);
        var title = titleHandle != null ? await titleHandle.InnerTextAsync().ConfigureAwait(false) : string.Empty;

        var descHandle = await element.QuerySelectorAsync(".desc, .note-desc").ConfigureAwait(false);
        var desc = descHandle != null ? await descHandle.InnerTextAsync().ConfigureAwait(false) : string.Empty;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(title)) sb.Append(title).Append(' ');
        if (!string.IsNullOrWhiteSpace(desc)) sb.Append(desc);
        return sb.ToString();
    }

    private async Task<string> ExtractNoteIdAsync(IElementHandle element)
    {
        var noteId = await element.GetAttributeAsync("data-note-id").ConfigureAwait(false);
        if (!string.IsNullOrEmpty(noteId)) return noteId;

        var link = await element.QuerySelectorAsync("a").ConfigureAwait(false);
        var href = link != null ? await link.GetAttributeAsync("href").ConfigureAwait(false) : null;
        if (string.IsNullOrEmpty(href)) return string.Empty;

        var parts = href.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.LastOrDefault() ?? string.Empty;
    }

    private async Task<bool> DoesCurrentDetailMatchKeywordsAsync(IPage page, string keyword)
    {
        try
        {
            var options = new KeywordMatchOptions
            {
                UseFuzzy = _detailMatch.UseFuzzy,
                MaxDistanceCap = _detailMatch.MaxDistanceCap,
                TokenCoverageThreshold = _detailMatch.TokenCoverageThreshold,
                IgnoreSpaces = _detailMatch.IgnoreSpaces
            };

            var (title, author, content, hashtags, imageAlts) = await ExtractDetailFieldsAsync(page).ConfigureAwait(false);

            double score = 0;
            double total = _detailMatch.TitleWeight + _detailMatch.AuthorWeight + _detailMatch.ContentWeight +
                           _detailMatch.HashtagWeight + _detailMatch.ImageAltWeight;

            if (!string.IsNullOrWhiteSpace(title) && KeywordMatcher.Matches(title, keyword, options)) score += _detailMatch.TitleWeight;
            if (!string.IsNullOrWhiteSpace(author) && KeywordMatcher.Matches(author, keyword, options)) score += _detailMatch.AuthorWeight;
            if (!string.IsNullOrWhiteSpace(content) && KeywordMatcher.Matches(content, keyword, options)) score += _detailMatch.ContentWeight;
            if (!string.IsNullOrWhiteSpace(hashtags) && KeywordMatcher.Matches(hashtags, keyword, options)) score += _detailMatch.HashtagWeight;
            if (!string.IsNullOrWhiteSpace(imageAlts) && KeywordMatcher.Matches(imageAlts, keyword, options)) score += _detailMatch.ImageAltWeight;

            var ratio = total <= 0 ? 0 : score / total;
            return ratio >= _detailMatch.WeightedThreshold;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "详情页关键词匹配失败");
            return false;
        }
    }

    private async Task<(string Title, string Author, string Content, string Hashtags, string ImageAlts)> ExtractDetailFieldsAsync(IPage page)
    {
        var titleHandle = await page.QuerySelectorAsync(".title, .note-title, h1").ConfigureAwait(false);
        var authorHandle = await page.QuerySelectorAsync(".user-name, .author-nickname, [data-testid='user-name']").ConfigureAwait(false);
        var contentHandle = await page.QuerySelectorAsync(".note-content, .content, .desc").ConfigureAwait(false);
        var hashtagHandles = await page.QuerySelectorAllAsync(".hashtag-container .tag, .note-tag").ConfigureAwait(false);
        var imageHandles = await page.QuerySelectorAllAsync("img").ConfigureAwait(false);

        var title = titleHandle != null ? await titleHandle.InnerTextAsync().ConfigureAwait(false) : string.Empty;
        var author = authorHandle != null ? await authorHandle.InnerTextAsync().ConfigureAwait(false) : string.Empty;
        var content = contentHandle != null ? await contentHandle.InnerTextAsync().ConfigureAwait(false) : string.Empty;
        var hashtags = string.Join(' ', await Task.WhenAll(hashtagHandles.Select(async h => await h.InnerTextAsync().ConfigureAwait(false))));
        var imageAlts = string.Join(';', await Task.WhenAll(imageHandles.Select(async h => await h.GetAttributeAsync("alt").ConfigureAwait(false) ?? string.Empty)));

        return (title, author, content, hashtags, imageAlts);
    }

    private static string? ExtractCommentId(ApiFeedback feedback)
    {
        if (feedback.Payload != null && feedback.Payload.TryGetValue("CommentId", out var id) && id is { } value)
        {
            return value?.ToString();
        }

        if (feedback.ResponseIds.Count > 0)
        {
            return feedback.ResponseIds.Last();
        }

        return null;
    }
}
