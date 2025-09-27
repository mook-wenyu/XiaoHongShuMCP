using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization;

/// <summary>
/// 中文：定义拟人化动作执行接口，负责节奏控制与行为审计。
/// </summary>
public interface IHumanizedActionService
{
    Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken);
}

public enum HumanizedActionKind
{
    RandomBrowse,
    KeywordBrowse,
    Like,
    Favorite,
    Comment
}

public sealed record HumanizedActionRequest(
    string? Keyword,
    string? PortraitId,
    string? CommentText,
    bool WaitForLoad,
    string BrowserKey,
    string? RequestId);

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
