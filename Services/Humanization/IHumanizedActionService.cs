using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Behavior;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization;

/// <summary>
/// 涓枃锛氬畾涔夋嫙浜哄寲鍔ㄤ綔鎵ц鎺ュ彛锛岃礋璐ｈ皟搴︿笌琛屼负瀹¤銆?/// English: Defines the humanized action service responsible for orchestration and auditing.
/// </summary>
public interface IHumanizedActionService
{
    Task<HumanizedActionPlan> PrepareAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken);
    Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionPlan plan, CancellationToken cancellationToken);
    Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken);
}

/// <summary>
/// 中文：拟人化动作类型枚举。
/// English: Enumeration of humanized action kinds.
/// </summary>
public enum HumanizedActionKind
{
    /// <summary>随机浏览首页 | Random browse home page</summary>
    RandomBrowse,

    /// <summary>按关键词浏览 | Browse by keyword</summary>
    KeywordBrowse,

    /// <summary>导航到探索页 | Navigate to explore page</summary>
    NavigateExplore,

    /// <summary>搜索关键词 | Search keyword</summary>
    SearchKeyword,

    /// <summary>根据关键词选择笔记 | Select note by keyword matching</summary>
    SelectNote,

    /// <summary>点赞当前笔记 | Like current note</summary>
    LikeCurrentNote,

    /// <summary>收藏当前笔记 | Favorite current note</summary>
    FavoriteCurrentNote,

    /// <summary>评论当前笔记 | Comment on current note</summary>
    CommentCurrentNote,

    /// <summary>拟人化滚动浏览当前页面 | Humanized scroll browsing on current page</summary>
    ScrollBrowse,

    /// <summary>发布笔记（上传图片、填写标题和正文、暂存离开）| Publish note (upload image, fill title and content, save draft and leave)</summary>
    PublishNote
}

public sealed record HumanizedActionRequest(
    IReadOnlyList<string> Keywords,
    string? PortraitId,
    string? CommentText,
    string BrowserKey,
    string? RequestId,
    string BehaviorProfile = "default",
    string? ImagePath = null,
    string? NoteTitle = null,
    string? NoteContent = null);

public sealed record HumanizedActionOutcome(
    bool Success,
    string Status,
    string? Message,
    IReadOnlyDictionary<string, string> Metadata)
{
    public static HumanizedActionOutcome Ok(IReadOnlyDictionary<string, string>? metadata = null)
        => new(true, "ok", null, metadata ?? Empty);

    public static HumanizedActionOutcome Fail(string status, string message, IReadOnlyDictionary<string, string>? metadata = null)
        => new(false, string.IsNullOrWhiteSpace(status) ? "ERR_UNEXPECTED" : status, message, metadata ?? Empty);

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record HumanizedActionPlan(
    HumanizedActionKind Kind,
    HumanizedActionRequest Request,
    string ResolvedKeyword,
    string BrowserKey,
    string BehaviorProfile,
    BrowserOpenResult Profile,
    HumanBehaviorProfileOptions BehaviorProfileOptions,
    HumanizedActionScript Script,
    IReadOnlyDictionary<string, string> Metadata)
{
    public string SelectedKeyword => ResolvedKeyword;

    public static HumanizedActionPlan Create(
        HumanizedActionKind kind,
        HumanizedActionRequest request,
        string resolvedKeyword,
        BrowserOpenResult profile,
        HumanBehaviorProfileOptions behaviorProfileOptions,
        HumanizedActionScript script,
        IDictionary<string, string> metadata)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(behaviorProfileOptions);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(metadata);

        var browserKey = string.IsNullOrWhiteSpace(request.BrowserKey) ? BrowserOpenRequest.UserProfileKey : request.BrowserKey.Trim();
        var behaviorProfile = string.IsNullOrWhiteSpace(request.BehaviorProfile) ? "default" : request.BehaviorProfile.Trim();

        return new HumanizedActionPlan(
            kind,
            request,
            resolvedKeyword,
            browserKey,
            behaviorProfile,
            profile,
            behaviorProfileOptions,
            script,
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase)));
    }
}





