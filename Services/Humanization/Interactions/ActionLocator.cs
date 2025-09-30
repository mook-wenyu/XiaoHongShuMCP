using System;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：描述定位页面元素的线索集合。
/// English: Represents a set of hints used to locate an element.
/// </summary>
public sealed record ActionLocator(
    AriaRole? Role = null,
    string? Text = null,
    string? Label = null,
    string? Placeholder = null,
    string? AltText = null,
    string? Title = null,
    string? TestId = null,
    string? Id = null,
    string? Selector = null)
{
    public AriaRole? Role { get; } = Role;

    public string? Text { get; } = Normalize(Text);

    public string? Label { get; } = Normalize(Label);

    public string? Placeholder { get; } = Normalize(Placeholder);

    public string? AltText { get; } = Normalize(AltText);

    public string? Title { get; } = Normalize(Title);

    public string? TestId { get; } = Normalize(TestId);

    public string? Id { get; } = Normalize(Id);

    public string? Selector { get; } = Normalize(Selector);

    public static ActionLocator Empty { get; } = new();

    public bool IsEmpty()
        => Role is null
           && Text is null
           && Label is null
           && Placeholder is null
           && AltText is null
           && Title is null
           && TestId is null
           && Id is null
           && Selector is null;

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
