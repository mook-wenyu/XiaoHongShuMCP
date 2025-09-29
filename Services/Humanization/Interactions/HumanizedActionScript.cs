using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：动作脚本集合。
/// English: Represents an ordered collection of humanized actions.
/// </summary>
public sealed class HumanizedActionScript
{
    public HumanizedActionScript(IEnumerable<HumanizedAction> actions)
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

        Actions = new ReadOnlyCollection<HumanizedAction>(list);
    }

    public IReadOnlyList<HumanizedAction> Actions { get; }

    public static HumanizedActionScript Empty { get; } = new(Array.Empty<HumanizedAction>());
}
