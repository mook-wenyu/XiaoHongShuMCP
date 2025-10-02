using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Notes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Humanization;

public sealed class KeywordResolverTests
{
    [Fact]
    public async Task ResolveAsync_WithCandidates_ShouldWriteSelectedMetadata()
    {
        var resolver = new KeywordResolver(new NullPortraitStore(), new StubKeywordProvider("默认"), NullLogger<KeywordResolver>.Instance);
        var metadata = new Dictionary<string, string>();

        var result = await resolver.ResolveAsync(new[] { "露营" }, "", metadata, CancellationToken.None);

        Assert.Equal("露营", result);
        Assert.Equal("露营", metadata["selectedKeyword"]);
        Assert.Equal("露营", metadata["keywords.selected"]);
        Assert.Equal("request", metadata["keyword.source"]);
    }

    [Fact]
    public async Task ResolveAsync_WhenFallbackUsed_ShouldMarkSelectedKeyword()
    {
        var resolver = new KeywordResolver(new NullPortraitStore(), new StubKeywordProvider("默认"), NullLogger<KeywordResolver>.Instance);
        var metadata = new Dictionary<string, string>();

        var result = await resolver.ResolveAsync(new List<string>(), "", metadata, CancellationToken.None);

        Assert.Equal("默认", result);
        Assert.Equal("默认", metadata["selectedKeyword"]);
        Assert.Equal("默认", metadata["keywords.selected"]);
        Assert.Equal("default", metadata["keyword.source"]);
    }

    private sealed class NullPortraitStore : IAccountPortraitStore
    {
        public Task<AccountPortrait?> GetAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult<AccountPortrait?>(null);
    }

    private sealed class StubKeywordProvider : IDefaultKeywordProvider
    {
        private readonly string? _value;

        public StubKeywordProvider(string? value)
        {
            _value = value;
        }

        public Task<string?> GetDefaultAsync(CancellationToken cancellationToken)
            => Task.FromResult(_value);
    }
}


