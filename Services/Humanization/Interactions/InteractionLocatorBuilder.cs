using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：基于 <see cref="ActionLocator"/> 构建 Playwright 定位器的默认实现。
/// English: Default implementation that resolves Playwright locators from <see cref="ActionLocator"/> hints.
/// </summary>
public sealed class InteractionLocatorBuilder : IInteractionLocatorBuilder
{
    private const int ScrollRetryLimit = 4;
    private const double ScrollMinFraction = 0.35d;
    private const double ScrollMaxFraction = 0.75d;
    private const double ReverseScrollProbability = 0.18d;
    private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromMilliseconds(2400);

    private readonly ILogger<InteractionLocatorBuilder> _logger;
    private readonly Random _random;

    public InteractionLocatorBuilder(ILogger<InteractionLocatorBuilder> logger, Random? random = null)
    {
        _logger = logger;
        _random = random ?? Random.Shared;
    }

    public async Task<ILocator> ResolveAsync(IPage page, ActionLocator locator, CancellationToken cancellationToken = default)
    {
        if (page is null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        if (locator is null || locator.IsEmpty())
        {
            throw new ArgumentException("定位线索不能为空", nameof(locator));
        }

        var candidates = new List<LocatorCandidate>();

        AppendSelectorCandidate(page, locator, candidates);
        AppendIdCandidate(page, locator, candidates);
        AppendRoleCandidates(page, locator, candidates);
        AppendTestIdCandidate(page, locator, candidates);
        AppendTextCandidate(page, locator, candidates);
        AppendLabelCandidate(page, locator, candidates);
        AppendPlaceholderCandidate(page, locator, candidates);
        AppendTitleCandidate(page, locator, candidates);
        AppendAltTextCandidate(page, locator, candidates);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolved = await TryResolveAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        throw new PlaywrightException($"无法根据提供的线索定位元素：{Describe(locator)}");
    }

    private static string Describe(ActionLocator locator)
        => $"role={locator.Role?.ToString() ?? "<null>"}, text={locator.Text ?? "<null>"}, label={locator.Label ?? "<null>"}, placeholder={locator.Placeholder ?? "<null>"}, altText={locator.AltText ?? "<null>"}, title={locator.Title ?? "<null>"}, testId={locator.TestId ?? "<null>"}, id={locator.Id ?? "<null>"}, selector={locator.Selector ?? "<null>"}";

    private async Task<ILocator?> TryResolveAsync(LocatorCandidate candidate, CancellationToken cancellationToken)
    {
        var resolved = await TrySelectAsync(candidate, cancellationToken).ConfigureAwait(false);
        if (resolved is not null)
        {
            _logger.LogDebug("[LocatorBuilder] resolved via {Strategy}: {Description}", candidate.Strategy, candidate.Description);
            return resolved;
        }

        if (!candidate.AllowScrollSearch)
        {
            return null;
        }

        for (var attempt = 0; attempt < ScrollRetryLimit; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await PerformScrollAsync(candidate.Page, cancellationToken).ConfigureAwait(false);

            resolved = await TrySelectAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (resolved is not null)
            {
                _logger.LogDebug("[LocatorBuilder] resolved via {Strategy} after scroll attempt {Attempt}: {Description}", candidate.Strategy, attempt + 1, candidate.Description);
                return resolved;
            }
        }

        return null;
    }

    private async Task<ILocator?> TrySelectAsync(LocatorCandidate candidate, CancellationToken cancellationToken)
    {
        try
        {
            var count = await candidate.Locator.CountAsync().ConfigureAwait(false);
            if (count <= 0)
            {
                return null;
            }

            var index = count == 1 ? 0 : _random.Next(count);
            var selected = count == 1 ? candidate.Locator.First : candidate.Locator.Nth(index);

            var waitTimeout = (float)Math.Max(500d, candidate.Timeout.TotalMilliseconds);

            await selected.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = waitTimeout
            }).ConfigureAwait(false);

            return selected;
        }
        catch (PlaywrightException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogTrace(ex, "[LocatorBuilder] strategy {Strategy} failed during wait: {Description}", candidate.Strategy, candidate.Description);
            return null;
        }
    }

    private async Task PerformScrollAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            var viewportHeight = await page.EvaluateAsync<float>("() => window.innerHeight || 800").ConfigureAwait(false);
            var baseDelta = Math.Max(200f, viewportHeight * (float)(ScrollMinFraction + (_random.NextDouble() * (ScrollMaxFraction - ScrollMinFraction))));
            var direction = _random.NextDouble() < ReverseScrollProbability ? -1f : 1f;
            await page.Mouse.WheelAsync(0, baseDelta * direction).ConfigureAwait(false);
            await page.WaitForTimeoutAsync((float)(90 + (_random.NextDouble() * 120))).ConfigureAwait(false);
        }
        catch (PlaywrightException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogTrace(ex, "[LocatorBuilder] 滚动尝试失败");
        }
    }

    private void AppendSelectorCandidate(IPage page, ActionLocator locator, ICollection<LocatorCandidate> output)
    {
        if (locator.Selector is not null)
        {
            output.Add(CreateCandidate(page, page.Locator(locator.Selector), $"selector:{locator.Selector}", "selector", allowScrollSearch: true));
        }
    }

    private void AppendIdCandidate(IPage page, ActionLocator locator, ICollection<LocatorCandidate> output)
    {
        if (locator.Id is not null)
        {
            var selector = $"#{locator.Id}";
            output.Add(CreateCandidate(page, page.Locator(selector), $"id:{locator.Id}", "id", allowScrollSearch: false));
        }
    }

    private void AppendRoleCandidates(IPage page, ActionLocator locator, ICollection<LocatorCandidate> output)
    {
        if (locator.Role is null)
        {
            return;
        }

        var options = new PageGetByRoleOptions();
        if (locator.Text is not null)
        {
            options.Name = locator.Text;
            options.Exact = false;
        }

        output.Add(CreateCandidate(page, page.GetByRole(locator.Role.Value, options), $"role:{locator.Role}", locator.Text is null ? "role" : "role+text", allowScrollSearch: true));

        if (locator.Text is not null && TryCreateFuzzyRegex(locator.Text, out var regex))
        {
            var regexOptions = new PageGetByRoleOptions
            {
                NameRegex = regex,
            };
            output.Add(CreateCandidate(page, page.GetByRole(locator.Role.Value, regexOptions), $"role-regex:{regex}", "role+regex", allowScrollSearch: true));
        }
    }

    private void AppendTestIdCandidate(IPage page, ActionLocator locator, ICollection<LocatorCandidate> output)
    {
        if (locator.TestId is not null)
        {
            output.Add(CreateCandidate(page, page.GetByTestId(locator.TestId), $"testId:{locator.TestId}", "testid", allowScrollSearch: false));
        }
    }

    private void AppendTextCandidate(IPage page, ActionLocator locator, ICollection<LocatorCandidate> output)
    {
        if (locator.Text is null)
        {
            return;
        }

        var options = new PageGetByTextOptions { Exact = false };
        output.Add(CreateCandidate(page, page.GetByText(locator.Text, options), $"text:{locator.Text}", "text", allowScrollSearch: true));

        if (TryCreateFuzzyRegex(locator.Text, out var regex))
        {
            output.Add(CreateCandidate(page, page.GetByText(regex), $"text-regex:{regex}", "text-regex", allowScrollSearch: true));
        }
    }

    private void AppendLabelCandidate(IPage page, ActionLocator locator, ICollection<LocatorCandidate> output)
    {
        if (locator.Label is null)
        {
            return;
        }

        var options = new PageGetByLabelOptions { Exact = false };
        output.Add(CreateCandidate(page, page.GetByLabel(locator.Label, options), $"label:{locator.Label}", "label", allowScrollSearch: true));

        if (TryCreateFuzzyRegex(locator.Label, out var regex))
        {
            output.Add(CreateCandidate(page, page.GetByLabel(regex), $"label-regex:{regex}", "label-regex", allowScrollSearch: true));
        }
    }

    private void AppendPlaceholderCandidate(IPage page, ActionLocator locator, ICollection<LocatorCandidate> output)
    {
        if (locator.Placeholder is null)
        {
            return;
        }

        var options = new PageGetByPlaceholderOptions { Exact = false };
        output.Add(CreateCandidate(page, page.GetByPlaceholder(locator.Placeholder, options), $"placeholder:{locator.Placeholder}", "placeholder", allowScrollSearch: true));

        if (TryCreateFuzzyRegex(locator.Placeholder, out var regex))
        {
            output.Add(CreateCandidate(page, page.GetByPlaceholder(regex), $"placeholder-regex:{regex}", "placeholder-regex", allowScrollSearch: true));
        }
    }

    private void AppendTitleCandidate(IPage page, ActionLocator locator, ICollection<LocatorCandidate> output)
    {
        if (locator.Title is null)
        {
            return;
        }

        var selector = CreateAttributeSelector("title", locator.Title);
        output.Add(CreateCandidate(page, page.Locator(selector), $"title:{locator.Title}", "title", allowScrollSearch: true));
    }

    private void AppendAltTextCandidate(IPage page, ActionLocator locator, ICollection<LocatorCandidate> output)
    {
        if (locator.AltText is null)
        {
            return;
        }

        var selector = CreateAttributeSelector("alt", locator.AltText, elementPrefix: "img");
        output.Add(CreateCandidate(page, page.Locator(selector), $"alt:{locator.AltText}", "alt", allowScrollSearch: true));
    }

    private static bool TryCreateFuzzyRegex(string value, out Regex regex)
    {
        regex = default!;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length < 3)
        {
            return false;
        }

        var tokens = Regex.Split(trimmed, "\\s+");
        var builder = new List<string>();

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var escaped = Regex.Escape(token.Trim());
            builder.Add(escaped);
        }

        if (builder.Count == 0)
        {
            return false;
        }

        string pattern;
        if (builder.Count == 1)
        {
            var single = builder[0];
            if (trimmed.Length > 8)
            {
                var headLength = Math.Min(4, trimmed.Length / 2);
                var tailLength = Math.Min(3, Math.Max(0, trimmed.Length - headLength));

                var head = Regex.Escape(trimmed[..headLength]);
                var tail = tailLength > 0 ? Regex.Escape(trimmed.Substring(trimmed.Length - tailLength, tailLength)) : string.Empty;
                pattern = tail.Length > 0 ? $"{head}.*{tail}" : head;
            }
            else
            {
                pattern = single;
            }
        }
        else
        {
            pattern = string.Join(@".{0,40}", builder);
        }

        regex = new Regex(pattern, RegexOptions.IgnoreCase);
        return true;
    }

    private static LocatorCandidate CreateCandidate(IPage page, ILocator locator, string description, string strategy, bool allowScrollSearch)
        => new(page, locator, description, strategy, DefaultWaitTimeout, allowScrollSearch);

    private static string CreateAttributeSelector(string attributeName, string value, string? elementPrefix = null)
    {
        var escaped = EscapeCss(value);
        var prefix = string.IsNullOrWhiteSpace(elementPrefix) ? string.Empty : elementPrefix.Trim();
        return string.IsNullOrEmpty(prefix)
            ? $"css=[{attributeName}=\"{escaped}\"]"
            : $"css={prefix}[{attributeName}=\"{escaped}\"]";
    }

    private static string EscapeCss(string value)
        => Regex.Replace(value, "[\\\\\"']", match => "\\" + match.Value);

    private readonly record struct LocatorCandidate(
        IPage Page,
        ILocator Locator,
        string Description,
        string Strategy,
        TimeSpan Timeout,
        bool AllowScrollSearch);
}
