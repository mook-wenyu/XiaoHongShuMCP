using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization;

public sealed class AccountPortraitStore : IAccountPortraitStore
{
    private readonly IReadOnlyDictionary<string, AccountPortrait> _portraits;
    private readonly ILogger<AccountPortraitStore> _logger;

    public AccountPortraitStore(IOptions<XiaoHongShuOptions> options, ILogger<AccountPortraitStore> logger)
    {
        _logger = logger;
        _portraits = BuildPortraits(options.Value);
    }

    public Task<AccountPortrait?> GetAsync(string id, CancellationToken cancellationToken)
    {
        _portraits.TryGetValue(id, out var portrait);
        return Task.FromResult(portrait);
    }

    private static IReadOnlyDictionary<string, AccountPortrait> BuildPortraits(XiaoHongShuOptions options)
    {
        if (options.Portraits.Count == 0)
        {
            return new ReadOnlyDictionary<string, AccountPortrait>(new Dictionary<string, AccountPortrait>(StringComparer.OrdinalIgnoreCase));
        }

        var dictionary = new Dictionary<string, AccountPortrait>(StringComparer.OrdinalIgnoreCase);
        foreach (var portrait in options.Portraits)
        {
            var tags = portrait.Tags?.Where(static t => !string.IsNullOrWhiteSpace(t)).Select(static t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
            var metadata = portrait.Metadata is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(portrait.Metadata, StringComparer.OrdinalIgnoreCase);

            dictionary[portrait.Id] = new AccountPortrait(portrait.Id, new ReadOnlyCollection<string>(tags), new ReadOnlyDictionary<string, string>(metadata));
        }

        return new ReadOnlyDictionary<string, AccountPortrait>(dictionary);
    }
}

public sealed class DefaultKeywordProvider : IDefaultKeywordProvider
{
    private readonly XiaoHongShuOptions _options;

    public DefaultKeywordProvider(IOptions<XiaoHongShuOptions> options)
    {
        _options = options.Value;
    }

    public Task<string?> GetDefaultAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.DefaultKeyword))
        {
            return Task.FromResult<string?>(_options.DefaultKeyword!.Trim());
        }

        return Task.FromResult<string?>(null);
    }
}

public sealed class RandomDelayConfiguration : IRandomDelayConfiguration
{
    private readonly XiaoHongShuOptions.HumanizedOptions _options;

    public RandomDelayConfiguration(IOptions<XiaoHongShuOptions> options)
    {
        _options = options.Value.Humanized;
    }

    public TimeSpan GetDelay()
    {
        if (_options.MaxDelayMs <= 0)
        {
            return TimeSpan.Zero;
        }

        var min = Math.Max(0, _options.MinDelayMs);
        var max = Math.Max(min + 1, _options.MaxDelayMs);
        var value = Random.Shared.Next(min, max);
        var jitter = (int)(value * _options.Jitter);
        if (jitter > 0)
        {
            value += Random.Shared.Next(-jitter, jitter);
            value = Math.Max(min, Math.Min(max, value));
        }

        return TimeSpan.FromMilliseconds(value);
    }
}
