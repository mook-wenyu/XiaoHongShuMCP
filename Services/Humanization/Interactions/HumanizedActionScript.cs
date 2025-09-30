using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：动作脚本集合，使用record类型确保可序列化。
/// English: Action script collection, using record type to ensure serializability.
/// </summary>
public sealed record HumanizedActionScript
{
    /// <summary>
    /// 中文：动作列表，过滤null值确保数据有效性。
    /// English: Action list, filters null values to ensure data validity.
    /// </summary>
    public IReadOnlyList<HumanizedAction> Actions { get; init; }

    /// <summary>
    /// 中文：空脚本单例。
    /// English: Empty script singleton.
    /// </summary>
    public static HumanizedActionScript Empty { get; } = new(Array.Empty<HumanizedAction>());

    [JsonConstructor]
    public HumanizedActionScript(IReadOnlyList<HumanizedAction> actions)
    {
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

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
}
