using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Humanization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services.EngagementFlow
{
    /// <summary>
    /// 负责点赞、收藏及取消操作的统一编排，确保拟人化交互与网络反馈审计一致。
    /// </summary>
    public sealed class NoteEngagementWorkflow : INoteEngagementWorkflow
    {
        private readonly ILogger<NoteEngagementWorkflow> _logger;
        private readonly IBrowserManager _browserManager;
        private readonly IAccountManager _accountManager;
        private readonly IPageStateGuard _pageStateGuard;
        private readonly IPageGuardian _pageGuardian;
        private readonly INoteDiscoveryService _noteDiscovery;
        private readonly IUniversalApiMonitor _apiMonitor;
        private readonly IFeedbackCoordinator _feedbackCoordinator;
        private readonly IHumanizedInteractionService _humanizedInteraction;
        private readonly XhsSettings.EndpointRetrySection _endpointRetry;

        public NoteEngagementWorkflow(
            ILogger<NoteEngagementWorkflow> logger,
            IBrowserManager browserManager,
            IAccountManager accountManager,
            IPageStateGuard pageStateGuard,
            IPageGuardian pageGuardian,
            INoteDiscoveryService noteDiscovery,
            IUniversalApiMonitor apiMonitor,
            IFeedbackCoordinator feedbackCoordinator,
            IHumanizedInteractionService humanizedInteraction,
            IOptions<XhsSettings> settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _browserManager = browserManager ?? throw new ArgumentNullException(nameof(browserManager));
            _accountManager = accountManager ?? throw new ArgumentNullException(nameof(accountManager));
            _pageStateGuard = pageStateGuard ?? throw new ArgumentNullException(nameof(pageStateGuard));
            _pageGuardian = pageGuardian ?? throw new ArgumentNullException(nameof(pageGuardian));
            _noteDiscovery = noteDiscovery ?? throw new ArgumentNullException(nameof(noteDiscovery));
            _apiMonitor = apiMonitor ?? throw new ArgumentNullException(nameof(apiMonitor));
            _feedbackCoordinator = feedbackCoordinator ?? throw new ArgumentNullException(nameof(feedbackCoordinator));
            _humanizedInteraction = humanizedInteraction ?? throw new ArgumentNullException(nameof(humanizedInteraction));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _endpointRetry = settings.Value.EndpointRetry ?? new XhsSettings.EndpointRetrySection();
        }

        /// <inheritdoc />
        public async Task<OperationResult<InteractionBundleResult>> InteractAsync(string keyword, bool like, bool favorite, CancellationToken ct = default)
        {
            _logger.LogInformation("开始执行笔记交互：关键词={Keyword}，点赞={Like}，收藏={Favorite}", keyword, like, favorite);

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return OperationResult<InteractionBundleResult>.Fail(
                    "关键词不能为空",
                    ErrorType.ValidationError,
                    "EMPTY_KEYWORD");
            }

            if (!like && !favorite)
            {
                return OperationResult<InteractionBundleResult>.Fail(
                    "未选择任何交互动作",
                    ErrorType.ValidationError,
                    "NO_ACTION_SELECTED");
            }

            if (!await _accountManager.IsLoggedInAsync().ConfigureAwait(false))
            {
                return OperationResult<InteractionBundleResult>.Fail(
                    "账号未登录，请先登录",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }

            var ensureDetail = await EnsureDetailPageAsync(keyword, ct).ConfigureAwait(false);
            if (!ensureDetail.Success || ensureDetail.Data is null)
            {
                return OperationResult<InteractionBundleResult>.Fail(
                    ensureDetail.ErrorMessage ?? "无法打开笔记详情页",
                    ensureDetail.ErrorType,
                    ensureDetail.ErrorCode ?? "DETAIL_PAGE_NOT_READY");
            }

            var page = ensureDetail.Data;
            var autoPage = await _browserManager.GetAutoPageAsync().ConfigureAwait(false);

            var endpoints = new HashSet<ApiEndpointType>();
            if (like)
            {
                endpoints.Add(ApiEndpointType.LikeNote);
                endpoints.Add(ApiEndpointType.DislikeNote);
            }
            if (favorite)
            {
                endpoints.Add(ApiEndpointType.CollectNote);
                endpoints.Add(ApiEndpointType.UncollectNote);
            }

            if (endpoints.Count > 0)
            {
                _feedbackCoordinator.Initialize(page, endpoints);
            }

            InteractionResult? likeResult = null;
            InteractionResult? favoriteResult = null;

            if (like)
            {
                likeResult = await PerformLikeAsync(keyword, page, ct).ConfigureAwait(false);
                await _humanizedInteraction.HumanBetweenActionsDelayAsync(ct).ConfigureAwait(false);
            }

            if (favorite)
            {
                favoriteResult = await PerformFavoriteAsync(keyword, page, autoPage, ct).ConfigureAwait(false);
                await _humanizedInteraction.HumanBetweenActionsDelayAsync(ct).ConfigureAwait(false);
            }

            var success = (!like || (likeResult?.Success ?? false)) && (!favorite || (favoriteResult?.Success ?? false));
            var message = like && favorite
                ? $"点赞：{(likeResult?.Success == true ? "成功" : likeResult?.Message ?? "失败")}；收藏：{(favoriteResult?.Success == true ? "成功" : favoriteResult?.Message ?? "失败")}"
                : like
                    ? likeResult?.Message ?? (likeResult?.Success == true ? "完成" : "失败")
                    : favoriteResult?.Message ?? (favoriteResult?.Success == true ? "完成" : "失败");

            return OperationResult<InteractionBundleResult>.Ok(
                new InteractionBundleResult(success, likeResult, favoriteResult, message));
        }

        /// <inheritdoc />
        public async Task<OperationResult<InteractionResult>> LikeAsync(string keyword, CancellationToken ct = default)
        {
            var bundle = await InteractAsync(keyword, like: true, favorite: false, ct).ConfigureAwait(false);
            if (!bundle.Success)
            {
                return OperationResult<InteractionResult>.Fail(
                    bundle.ErrorMessage ?? "点赞失败",
                    bundle.ErrorType,
                    bundle.ErrorCode ?? "LIKE_NOTE_FAILED");
            }

            var result = bundle.Data?.Like ?? new InteractionResult(false, "点赞", "未知", "未知", "未返回点赞结果", "LIKE_RESULT_MISSING");
            return OperationResult<InteractionResult>.Ok(result);
        }

        /// <inheritdoc />
        public async Task<OperationResult<InteractionResult>> FavoriteAsync(string keyword, CancellationToken ct = default)
        {
            var bundle = await InteractAsync(keyword, like: false, favorite: true, ct).ConfigureAwait(false);
            if (!bundle.Success)
            {
                return OperationResult<InteractionResult>.Fail(
                    bundle.ErrorMessage ?? "收藏失败",
                    bundle.ErrorType,
                    bundle.ErrorCode ?? "FAVORITE_NOTE_FAILED");
            }

            var result = bundle.Data?.Favorite ?? new InteractionResult(false, "收藏", "未知", "未知", "未返回收藏结果", "FAVORITE_RESULT_MISSING");
            return OperationResult<InteractionResult>.Ok(result);
        }

        /// <inheritdoc />
        public async Task<OperationResult<InteractionResult>> UnlikeAsync(string keyword, CancellationToken ct = default)
        {
            _logger.LogInformation("开始取消点赞：关键词={Keyword}", keyword);

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return OperationResult<InteractionResult>.Fail(
                    "关键词不能为空",
                    ErrorType.ValidationError,
                    "EMPTY_KEYWORD");
            }

            if (!await _accountManager.IsLoggedInAsync().ConfigureAwait(false))
            {
                return OperationResult<InteractionResult>.Fail(
                    "账号未登录，请先登录",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }

            var ensureDetail = await EnsureDetailPageAsync(keyword, ct).ConfigureAwait(false);
            if (!ensureDetail.Success || ensureDetail.Data is null)
            {
                return OperationResult<InteractionResult>.Fail(
                    ensureDetail.ErrorMessage ?? "无法打开笔记详情页",
                    ensureDetail.ErrorType,
                    ensureDetail.ErrorCode ?? "DETAIL_PAGE_NOT_READY");
            }

            var page = ensureDetail.Data;
            var autoPage = await _browserManager.GetAutoPageAsync().ConfigureAwait(false);
            var result = await PerformUnlikeAsync(keyword, page, autoPage, ct).ConfigureAwait(false);
            return result.Success
                ? OperationResult<InteractionResult>.Ok(result)
                : OperationResult<InteractionResult>.Fail(result.Message, ErrorType.NetworkError, result.ErrorCode ?? "UNLIKE_FAILED");
        }

        /// <inheritdoc />
        public async Task<OperationResult<InteractionResult>> UncollectAsync(string keyword, CancellationToken ct = default)
        {
            _logger.LogInformation("开始取消收藏：关键词={Keyword}", keyword);

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return OperationResult<InteractionResult>.Fail(
                    "关键词不能为空",
                    ErrorType.ValidationError,
                    "EMPTY_KEYWORD");
            }

            if (!await _accountManager.IsLoggedInAsync().ConfigureAwait(false))
            {
                return OperationResult<InteractionResult>.Fail(
                    "账号未登录，请先登录",
                    ErrorType.LoginRequired,
                    "NOT_LOGGED_IN");
            }

            var ensureDetail = await EnsureDetailPageAsync(keyword, ct).ConfigureAwait(false);
            if (!ensureDetail.Success || ensureDetail.Data is null)
            {
                return OperationResult<InteractionResult>.Fail(
                    ensureDetail.ErrorMessage ?? "无法打开笔记详情页",
                    ensureDetail.ErrorType,
                    ensureDetail.ErrorCode ?? "DETAIL_PAGE_NOT_READY");
            }

            var page = ensureDetail.Data;
            var autoPage = await _browserManager.GetAutoPageAsync().ConfigureAwait(false);
            var result = await PerformUncollectAsync(keyword, page, autoPage, ct).ConfigureAwait(false);
            return result.Success
                ? OperationResult<InteractionResult>.Ok(result)
                : OperationResult<InteractionResult>.Fail(result.Message, ErrorType.NetworkError, result.ErrorCode ?? "UNCOLLECT_FAILED");
        }

        private async Task<OperationResult<IPage>> EnsureDetailPageAsync(string keyword, CancellationToken ct)
        {
            var page = await _browserManager.GetPageAsync().ConfigureAwait(false);
            var status = await _pageGuardian.InspectAsync(page, PageType.NoteDetail, ct).ConfigureAwait(false);

            if (status.PageType == PageType.NoteDetail)
            {
                var matched = await _noteDiscovery.DoesDetailMatchKeywordAsync(page, keyword, ct).ConfigureAwait(false);
                if (matched)
                {
                    return OperationResult<IPage>.Ok(page);
                }
            }

            var autoPage = await _browserManager.GetAutoPageAsync().ConfigureAwait(false);
            var ensured = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(autoPage).ConfigureAwait(false);
            if (!ensured)
            {
                return OperationResult<IPage>.Fail(
                    "无法导航至探索/搜索页",
                    ErrorType.NavigationError,
                    "ENTRY_PAGE_NOT_AVAILABLE");
            }

            var matches = await _noteDiscovery.FindVisibleMatchingNotesAsync(keyword, 1, ct).ConfigureAwait(false);
            if (!matches.Success || matches.Data is null || matches.Data.Count == 0)
            {
                return OperationResult<IPage>.Fail(
                    matches.ErrorMessage ?? $"未找到匹配关键词的笔记: {keyword}",
                    matches.ErrorType,
                    matches.ErrorCode ?? "NO_MATCHING_NOTES");
            }

            var target = matches.Data[0];
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.ThinkingPause, ct).ConfigureAwait(false);
            await _humanizedInteraction.HumanClickAsync(target).ConfigureAwait(false);
            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.PageLoading, ct).ConfigureAwait(false);

            page = await _browserManager.GetPageAsync().ConfigureAwait(false);
            var detailStatus = await _pageGuardian.InspectAsync(page, PageType.NoteDetail, ct).ConfigureAwait(false);
            if (!detailStatus.IsPageReady)
            {
                return OperationResult<IPage>.Fail(
                    "打开笔记详情失败",
                    ErrorType.NavigationError,
                    "DETAIL_PAGE_NOT_READY");
            }

            var matchedDetail = await _noteDiscovery.DoesDetailMatchKeywordAsync(page, keyword, ct).ConfigureAwait(false);
            if (!matchedDetail)
            {
                return OperationResult<IPage>.Fail(
                    "当前详情页与关键词不匹配",
                    ErrorType.ElementNotFound,
                    "DETAIL_NOT_MATCHED");
            }

            return OperationResult<IPage>.Ok(page);
        }

        private async Task<InteractionResult> PerformLikeAsync(string keyword, IPage page, CancellationToken ct)
        {
            _feedbackCoordinator.Reset(ApiEndpointType.LikeNote);
            _feedbackCoordinator.Initialize(page, new[] { ApiEndpointType.LikeNote });
            _apiMonitor.ClearMonitoredData(ApiEndpointType.DislikeNote);

            var stopwatch = Stopwatch.StartNew();
            var likeResult = await _humanizedInteraction.HumanLikeAsync().ConfigureAwait(false);

            InteractionResult finalResult = likeResult;
            bool apiConfirmed = false;

            if (likeResult.Success && !string.Equals(likeResult.ErrorCode, "ALREADY_LIKED", StringComparison.OrdinalIgnoreCase))
            {
                var feedback = await _feedbackCoordinator.ObserveAsync(ApiEndpointType.LikeNote, ct).ConfigureAwait(false);
                if (!feedback.Success)
                {
                    feedback = await ObserveWithRetryAsync(ApiEndpointType.LikeNote, ct).ConfigureAwait(false);
                }

                apiConfirmed = feedback.Success;
                if (!feedback.Success)
                {
                    var fallback = _apiMonitor.GetRawResponses(ApiEndpointType.DislikeNote);
                    finalResult = fallback.Count > 0
                        ? new InteractionResult(false, "点赞", likeResult.PreviousState, likeResult.CurrentState,
                            "点赞失败：检测到取消点赞请求，可能初始已点赞或状态识别偏差", "UNEXPECTED_DISLIKE_CAPTURED")
                        : new InteractionResult(false, "点赞", likeResult.PreviousState, likeResult.CurrentState,
                            "点赞失败：未捕获网络确认", "LIKE_API_NOT_CONFIRMED");
                }
            }

            stopwatch.Stop();
            _feedbackCoordinator.Audit("点赞", keyword, new FeedbackContext(true, apiConfirmed, stopwatch.Elapsed, finalResult.Message));
            return finalResult;
        }

        private async Task<InteractionResult> PerformFavoriteAsync(string keyword, IPage page, IAutoPage autoPage, CancellationToken ct)
        {
            _feedbackCoordinator.Reset(ApiEndpointType.CollectNote);
            _apiMonitor.ClearMonitoredData(ApiEndpointType.UncollectNote);

            var stopwatch = Stopwatch.StartNew();
            var favoriteResult = await _humanizedInteraction.HumanFavoriteAsync(autoPage).ConfigureAwait(false);

            InteractionResult finalResult = favoriteResult;
            bool apiConfirmed = false;

            if (favoriteResult.Success && !string.Equals(favoriteResult.ErrorCode, "ALREADY_FAVORITED", StringComparison.OrdinalIgnoreCase))
            {
                var feedback = await ObserveWithRetryAsync(ApiEndpointType.CollectNote, ct).ConfigureAwait(false);
                apiConfirmed = feedback.Success;
                if (!feedback.Success)
                {
                    var fallback = _apiMonitor.GetRawResponses(ApiEndpointType.UncollectNote);
                    finalResult = fallback.Count > 0
                        ? new InteractionResult(false, "收藏", favoriteResult.PreviousState, favoriteResult.CurrentState,
                            "收藏失败：检测到取消收藏请求，可能初始已收藏或状态识别偏差", "UNEXPECTED_UNCOLLECT_CAPTURED")
                        : new InteractionResult(false, "收藏", favoriteResult.PreviousState, favoriteResult.CurrentState,
                            "收藏失败：未捕获网络确认", "COLLECT_API_NOT_CONFIRMED");
                }
            }

            stopwatch.Stop();
            _feedbackCoordinator.Audit("收藏", keyword, new FeedbackContext(true, apiConfirmed, stopwatch.Elapsed, finalResult.Message));
            return finalResult;
        }

        private async Task<InteractionResult> PerformUnlikeAsync(string keyword, IPage page, IAutoPage autoPage, CancellationToken ct)
        {
            _feedbackCoordinator.Reset(ApiEndpointType.DislikeNote);

            var stopwatch = Stopwatch.StartNew();
            var result = await _humanizedInteraction.HumanUnlikeAsync(autoPage).ConfigureAwait(false);
            if (!result.Success)
            {
                stopwatch.Stop();
                _feedbackCoordinator.Audit("取消点赞", keyword, new FeedbackContext(true, false, stopwatch.Elapsed, result.Message));
                return result;
            }

            var feedback = await ObserveWithRetryAsync(ApiEndpointType.DislikeNote, ct).ConfigureAwait(false);
            stopwatch.Stop();
            var finalResult = feedback.Success
                ? result
                : new InteractionResult(false, "取消点赞", result.PreviousState, result.CurrentState, "取消点赞失败：未捕获网络确认", "UNLIKE_API_NOT_CONFIRMED");

            _feedbackCoordinator.Audit("取消点赞", keyword, new FeedbackContext(true, feedback.Success, stopwatch.Elapsed, finalResult.Message));
            return finalResult;
        }

        private async Task<InteractionResult> PerformUncollectAsync(string keyword, IPage page, IAutoPage autoPage, CancellationToken ct)
        {
            _feedbackCoordinator.Reset(ApiEndpointType.UncollectNote);

            var stopwatch = Stopwatch.StartNew();
            var result = await _humanizedInteraction.HumanUnfavoriteAsync(autoPage).ConfigureAwait(false);
            if (!result.Success)
            {
                stopwatch.Stop();
                _feedbackCoordinator.Audit("取消收藏", keyword, new FeedbackContext(true, false, stopwatch.Elapsed, result.Message));
                return result;
            }

            var feedback = await ObserveWithRetryAsync(ApiEndpointType.UncollectNote, ct).ConfigureAwait(false);
            stopwatch.Stop();
            var finalResult = feedback.Success
                ? result
                : new InteractionResult(false, "取消收藏", result.PreviousState, result.CurrentState, "取消收藏失败：未捕获网络确认", "UNCOLLECT_API_NOT_CONFIRMED");

            _feedbackCoordinator.Audit("取消收藏", keyword, new FeedbackContext(true, feedback.Success, stopwatch.Elapsed, finalResult.Message));
            return finalResult;
        }

        private async Task<ApiFeedback> ObserveWithRetryAsync(ApiEndpointType endpoint, CancellationToken ct)
        {
            var attemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1, _endpointRetry.AttemptTimeoutMs));
            var attempts = Math.Max(1, _endpointRetry.MaxRetries);

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var success = await _apiMonitor.WaitForResponsesAsync(endpoint, attemptTimeout, 1).ConfigureAwait(false);
                if (success) break;
            }

            var responses = _apiMonitor.GetRawResponses(endpoint) ?? new List<MonitoredApiResponse>();
            var message = responses.Count > 0 ? "已捕获API响应" : "未捕获API响应";
            var ids = responses.Select((_, index) => $"{endpoint}-{index}").ToList();
            IReadOnlyDictionary<string, object?>? payload = responses.LastOrDefault()?.ProcessedData?.ToDictionary(k => k.Key, v => (object?)v.Value);
            return new ApiFeedback(responses.Count > 0, message, ids, payload);
        }
    }
}
