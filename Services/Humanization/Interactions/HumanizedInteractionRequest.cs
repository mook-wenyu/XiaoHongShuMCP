using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：表示一次拟人化动作执行请求。
/// English: Represents a request to execute a single humanized interaction.
/// </summary>
public sealed record HumanizedInteractionRequest(
    HumanizedAction Action,
    string? BrowserKey = null,
    string? BehaviorProfile = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public HumanizedAction Action { get; } = Action ?? throw new ArgumentNullException(nameof(Action));

    public string? BrowserKey { get; } = string.IsNullOrWhiteSpace(BrowserKey) ? null : BrowserKey!.Trim();

    public string BehaviorProfile { get; } = string.IsNullOrWhiteSpace(BehaviorProfile)
        ? Action.BehaviorProfile
        : BehaviorProfile!.Trim();

    public IReadOnlyDictionary<string, string> Metadata { get; }
        = Metadata is null
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(Metadata));

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata
        = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
}

/// <summary>
/// 中文：批量执行拟人化动作的请求。
/// English: Represents a batch request that executes a sequence of humanized actions.
/// </summary>
public sealed class HumanizedInteractionBatchRequest
{
    public HumanizedInteractionBatchRequest(
        IEnumerable<HumanizedAction> actions,
        string? browserKey = null,
        string? behaviorProfile = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (actions is null)
        {
            throw new ArgumentNullException(nameof(actions));
        }

        var list = new List<HumanizedAction>();
        foreach (var action in actions)
        {
            if (action is not null)
            {
                list.Add(action);
            }
        }

        if (list.Count == 0)
        {
            throw new ArgumentException("动作集合不能为空", nameof(actions));
        }

        Actions = new ReadOnlyCollection<HumanizedAction>(list);
        BrowserKey = string.IsNullOrWhiteSpace(browserKey) ? null : browserKey.Trim();
        BehaviorProfile = string.IsNullOrWhiteSpace(behaviorProfile)
            ? list[0].BehaviorProfile
            : behaviorProfile!.Trim();
        Metadata = metadata is null
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata));
    }

    public IReadOnlyList<HumanizedAction> Actions { get; }

    public string? BrowserKey { get; }

    public string BehaviorProfile { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata
        = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
}
