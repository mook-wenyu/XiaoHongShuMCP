using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：动作附加参数集合。
/// English: Optional parameter bag for humanized actions.
/// </summary>
public sealed record HumanizedActionParameters
{
    public HumanizedActionParameters(
        string? text = null,
        string? secondaryText = null,
        IReadOnlyList<string>? hotkeys = null,
        double? scrollDelta = null,
        double? wheelDeltaX = null,
        double? wheelDeltaY = null,
        ActionLocator? secondaryTarget = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? filePath = null)
    {
        Text = string.IsNullOrWhiteSpace(text) ? null : text;
        SecondaryText = string.IsNullOrWhiteSpace(secondaryText) ? null : secondaryText;
        Hotkeys = hotkeys is null
            ? Array.Empty<string>()
            : new ReadOnlyCollection<string>(new List<string>(hotkeys));
        ScrollDelta = scrollDelta;
        WheelDeltaX = wheelDeltaX;
        WheelDeltaY = wheelDeltaY;
        SecondaryTarget = secondaryTarget;
        Metadata = metadata is null
            ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata));
        FilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
    }

    public string? Text { get; }

    public string? SecondaryText { get; }

    public IReadOnlyList<string> Hotkeys { get; }

    public double? ScrollDelta { get; }

    public double? WheelDeltaX { get; }

    public double? WheelDeltaY { get; }

    public ActionLocator? SecondaryTarget { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public string? FilePath { get; }

    public static HumanizedActionParameters Empty { get; } = new();
}
