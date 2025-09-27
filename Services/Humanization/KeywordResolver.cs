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

    public async Task<string> ResolveAsync(string? keyword, string? portraitId, IDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            metadata["keyword"] = normalized;
            metadata["source"] = "request";
            return normalized;
        }

        if (!string.IsNullOrWhiteSpace(portraitId))
        {
            var portrait = await _portraitStore.GetAsync(portraitId!, cancellationToken).ConfigureAwait(false);
            if (portrait is not null && portrait.Tags.Count > 0)
            {
                var weighted = SelectKeywordByWeight(portrait);
                metadata["keyword"] = weighted;
                metadata["portraitId"] = portraitId!;
                metadata["source"] = "portrait";
                return weighted;
            }
        }

        var fallback = await _defaultKeywordProvider.GetDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            metadata["keyword"] = fallback!;
            metadata["source"] = "default";
            return fallback!;
        }

        throw new InvalidOperationException("无法解析关键词：请求、画像与默认配置均为空。");
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
