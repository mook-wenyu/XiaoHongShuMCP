using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Services.Notes;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization;

/// <summary>
/// 中文：关键词解析器，优先使用请求参数，其次画像标签，最后回退默认配置。
/// </summary>
public sealed class KeywordResolver : IKeywordResolver
{
    private readonly IAccountPortraitStore _portraitStore;
    private readonly IDefaultKeywordProvider _defaultKeywordProvider;
    private readonly ILogger<KeywordResolver> _logger;

    public KeywordResolver(
        IAccountPortraitStore portraitStore,
        IDefaultKeywordProvider defaultKeywordProvider,
        ILogger<KeywordResolver> logger)
    {
        _portraitStore = portraitStore;
        _defaultKeywordProvider = defaultKeywordProvider;
        _logger = logger;
    }

    public async Task<string> ResolveAsync(IReadOnlyList<string> keywords, string? portraitId, IDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        var fromRequest = PickFromCandidates(keywords);
        if (fromRequest is not null)
        {
            metadata["keyword"] = fromRequest;
            metadata["selectedKeyword"] = fromRequest;
            metadata["keywords.selected"] = fromRequest;
            metadata["keyword.source"] = "request";
            metadata["keyword.candidates"] = keywords is null ? string.Empty : string.Join(",", keywords);
            return fromRequest;
        }

        if (!string.IsNullOrWhiteSpace(portraitId))
        {
            var portrait = await _portraitStore.GetAsync(portraitId!, cancellationToken).ConfigureAwait(false);
            if (portrait is not null && portrait.Tags.Count > 0)
            {
                var weighted = SelectKeywordByWeight(portrait);
                metadata["keyword"] = weighted;
                metadata["selectedKeyword"] = weighted;
                metadata["keywords.selected"] = weighted;
                metadata["portraitId"] = portraitId!;
                metadata["keyword.source"] = "portrait";
                return weighted;
            }
        }

        var fallback = await _defaultKeywordProvider.GetDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            metadata["keyword"] = fallback!;
            metadata["selectedKeyword"] = fallback!;
            metadata["keywords.selected"] = fallback!;
            metadata["keyword.source"] = "default";
            return fallback!;
        }

        throw new InvalidOperationException("无法解析关键词：请求、画像与默认配置均为空。");
    }

    private static string? PickFromCandidates(IReadOnlyList<string>? keywords)
    {
        if (keywords is null || keywords.Count == 0)
        {
            return null;
        }

        var candidates = keywords
            .Where(static k => !string.IsNullOrWhiteSpace(k))
            .Select(static k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        var index = Random.Shared.Next(candidates.Length);
        return candidates[index];
    }

    private static string SelectKeywordByWeight(AccountPortrait portrait)
    {
        if (portrait.Metadata is { Count: > 0 })
        {
            var weights = new List<(string Tag, double Weight)>();
            foreach (var (key, value) in portrait.Metadata)
            {
                if (!key.StartsWith("tagWeight:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var tagName = key["tagWeight:".Length..].Trim();
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    continue;
                }

                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var weight) && weight > 0)
                {
                    weights.Add((tagName, weight));
                }
            }

            if (weights.Count > 0)
            {
                var total = weights.Sum(x => x.Weight);
                var random = Random.Shared.NextDouble() * total;
                foreach (var (tag, weight) in weights)
                {
                    if (random < weight)
                    {
                        return tag;
                    }

                    random -= weight;
                }

                return weights[0].Tag;
            }
        }

        if (portrait.Tags.Count > 0)
        {
            return portrait.Tags[Random.Shared.Next(portrait.Tags.Count)];
        }

        throw new InvalidOperationException($"画像 {portrait.Id} 不包含可用标签。");
    }
}

public interface IAccountPortraitStore
{
    Task<AccountPortrait?> GetAsync(string id, CancellationToken cancellationToken);
}

public interface IDefaultKeywordProvider
{
    Task<string?> GetDefaultAsync(CancellationToken cancellationToken);
}

