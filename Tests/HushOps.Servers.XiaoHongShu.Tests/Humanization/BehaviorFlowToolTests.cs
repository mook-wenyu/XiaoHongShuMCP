using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using HushOps.Servers.XiaoHongShu.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Humanization;

public sealed class BehaviorFlowToolTests
{
    [Fact]
    public async Task RandomBrowseAsync_ShouldReturnBrowseFlowResultWithRandomBrowseType()
    {
        var service = new StubHumanizedActionService();
        var options = CreateBehaviorOptions();
        var tool = new BehaviorFlowTool(service, NullLogger<BehaviorFlowTool>.Instance, options);

        var result = await tool.RandomBrowseAsync(
            new BehaviorFlowRequest(Array.Empty<string>(), "portrait-1", "user", "default"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("RandomBrowse", result.Data!.BrowseType);
        Assert.Equal("热门", result.Data.SelectedKeyword);
        Assert.Equal("default", result.Data.KeywordSource);
        Assert.Equal("note-123", result.Data.NoteId);
        Assert.Equal("测试笔记标题", result.Data.NoteTitle);
        Assert.Equal("https://www.xiaohongshu.com/explore/note-123", result.Data.NoteUrl);
        Assert.Equal("default", result.Data.BehaviorProfile);
        Assert.NotEmpty(result.Data.RequestId);
        Assert.True(service.PrepareSelectNoteCalled);
        Assert.True(service.ExecuteSelectNoteCalled);
    }

    [Fact]
    public async Task KeywordBrowseAsync_WithKeywords_ShouldReturnBrowseFlowResultWithKeywordBrowseType()
    {
        var service = new StubHumanizedActionService();
        var options = CreateBehaviorOptions();
        var tool = new BehaviorFlowTool(service, NullLogger<BehaviorFlowTool>.Instance, options);

        var result = await tool.KeywordBrowseAsync(
            new BehaviorFlowRequest(new[] { "露营", "徒步" }, "", "user", "default"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("KeywordBrowse", result.Data!.BrowseType);
        Assert.Equal("露营", result.Data.SelectedKeyword);
        Assert.Equal("request", result.Data.KeywordSource);
        Assert.Equal("note-123", result.Data.NoteId);
        Assert.Equal("测试笔记标题", result.Data.NoteTitle);
        Assert.Equal("https://www.xiaohongshu.com/explore/note-123", result.Data.NoteUrl);
        Assert.Contains("Like", result.Data.Interactions);
        Assert.Contains("Favorite", result.Data.Interactions);
        Assert.Empty(result.Data.SkippedInteractions);
        Assert.Empty(result.Data.FailedInteractions);
        Assert.Equal("default", result.Data.BehaviorProfile);
        Assert.True(service.PrepareSelectNoteCalled);
        Assert.True(service.ExecuteSelectNoteCalled);
        Assert.True(service.ExecuteLikeCalled);
        Assert.True(service.ExecuteFavoriteCalled);
    }

    [Fact]
    public async Task BrowseFlow_WhenSelectNoteFails_ShouldReturnFailure()
    {
        var service = new StubHumanizedActionService(selectNoteSuccess: false);
        var options = CreateBehaviorOptions();
        var tool = new BehaviorFlowTool(service, NullLogger<BehaviorFlowTool>.Instance, options);

        var result = await tool.KeywordBrowseAsync(
            new BehaviorFlowRequest(new[] { "露营" }, "", "user", "default"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Contains("选择笔记失败", result.ErrorMessage ?? "");
        Assert.True(service.PrepareSelectNoteCalled);
        Assert.True(service.ExecuteSelectNoteCalled);
        Assert.False(service.ExecuteLikeCalled);
        Assert.False(service.ExecuteFavoriteCalled);
    }

    [Fact]
    public async Task BrowseFlow_WithLowProbability_ShouldSkipInteractions()
    {
        var service = new StubHumanizedActionService();
        var options = Options.Create(new HumanBehaviorOptions
        {
            DefaultProfile = "default",
            Profiles = new Dictionary<string, HumanBehaviorProfileOptions>
            {
                ["default"] = new HumanBehaviorProfileOptions
                {
                    LikeProbability = 0.0,
                    FavoriteProbability = 0.0
                }
            }
        });
        var tool = new BehaviorFlowTool(service, NullLogger<BehaviorFlowTool>.Instance, options);

        var result = await tool.KeywordBrowseAsync(
            new BehaviorFlowRequest(new[] { "露营" }, "", "user", "default"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data!.Interactions);
        Assert.Contains("Like", result.Data.SkippedInteractions);
        Assert.Contains("Favorite", result.Data.SkippedInteractions);
        Assert.Empty(result.Data.FailedInteractions);
        Assert.False(service.ExecuteLikeCalled);
        Assert.False(service.ExecuteFavoriteCalled);
    }

    [Fact]
    public async Task BrowseFlow_WhenInteractionFails_ShouldRecordFailure()
    {
        var service = new StubHumanizedActionService(likeSuccess: false, favoriteSuccess: false);
        var options = CreateBehaviorOptions(likeProbability: 1.0, favoriteProbability: 1.0);
        var tool = new BehaviorFlowTool(service, NullLogger<BehaviorFlowTool>.Instance, options);

        var result = await tool.KeywordBrowseAsync(
            new BehaviorFlowRequest(new[] { "露营" }, "", "user", "default"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data!.Interactions);
        Assert.Empty(result.Data.SkippedInteractions);
        Assert.Contains("Like", result.Data.FailedInteractions);
        Assert.Contains("Favorite", result.Data.FailedInteractions);
        Assert.True(service.ExecuteLikeCalled);
        Assert.True(service.ExecuteFavoriteCalled);
    }

    private static IOptions<HumanBehaviorOptions> CreateBehaviorOptions(double likeProbability = 1.0, double favoriteProbability = 1.0)
    {
        return Options.Create(new HumanBehaviorOptions
        {
            DefaultProfile = "default",
            Profiles = new Dictionary<string, HumanBehaviorProfileOptions>
            {
                ["default"] = new HumanBehaviorProfileOptions
                {
                    LikeProbability = likeProbability,
                    FavoriteProbability = favoriteProbability
                }
            }
        });
    }

    private sealed class StubHumanizedActionService : IHumanizedActionService
    {
        private readonly bool _selectNoteSuccess;
        private readonly bool _likeSuccess;
        private readonly bool _favoriteSuccess;

        public StubHumanizedActionService(bool selectNoteSuccess = true, bool likeSuccess = true, bool favoriteSuccess = true)
        {
            _selectNoteSuccess = selectNoteSuccess;
            _likeSuccess = likeSuccess;
            _favoriteSuccess = favoriteSuccess;
        }

        public bool PrepareSelectNoteCalled { get; private set; }
        public bool ExecuteSelectNoteCalled { get; private set; }
        public bool ExecuteLikeCalled { get; private set; }
        public bool ExecuteFavoriteCalled { get; private set; }

        public Task<HumanizedActionPlan> PrepareAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken)
        {
            if (kind == HumanizedActionKind.SelectNote)
            {
                PrepareSelectNoteCalled = true;
            }

            var browserProfile = new Services.Browser.BrowserOpenResult(
                Services.Browser.BrowserProfileKind.User,
                "user",
                "/tmp/user",
                false,
                false,
                null,
                true,
                true,
                null);

            var plan = HumanizedActionPlan.Create(
                kind,
                request,
                request.Keywords.Count > 0 ? request.Keywords[0] : "热门",
                browserProfile,
                new HumanBehaviorProfileOptions(),
                new HumanizedActionScript(new List<HumanizedAction>()),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            return Task.FromResult(plan);
        }

        public Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionPlan plan, CancellationToken cancellationToken)
        {
            if (plan.Kind == HumanizedActionKind.SelectNote)
            {
                ExecuteSelectNoteCalled = true;

                if (!_selectNoteSuccess)
                {
                    return Task.FromResult(HumanizedActionOutcome.Fail("error", "选择笔记失败", new Dictionary<string, string>()));
                }

                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["detail.noteId"] = "note-123",
                    ["detail.title"] = "测试笔记标题",
                    ["detail.url"] = "https://www.xiaohongshu.com/explore/note-123",
                    ["keywords.selected"] = plan.ResolvedKeyword,
                    ["keyword.source"] = plan.Request.Keywords.Count > 0 ? "request" : "default"
                };

                return Task.FromResult(HumanizedActionOutcome.Ok(metadata));
            }

            return Task.FromResult(HumanizedActionOutcome.Ok(new Dictionary<string, string>()));
        }

        public Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken)
        {
            if (kind == HumanizedActionKind.LikeCurrentNote)
            {
                ExecuteLikeCalled = true;
                return Task.FromResult(_likeSuccess
                    ? HumanizedActionOutcome.Ok(new Dictionary<string, string>())
                    : HumanizedActionOutcome.Fail("error", "点赞失败", new Dictionary<string, string>()));
            }

            if (kind == HumanizedActionKind.FavoriteCurrentNote)
            {
                ExecuteFavoriteCalled = true;
                return Task.FromResult(_favoriteSuccess
                    ? HumanizedActionOutcome.Ok(new Dictionary<string, string>())
                    : HumanizedActionOutcome.Fail("error", "收藏失败", new Dictionary<string, string>()));
            }

            return Task.FromResult(HumanizedActionOutcome.Ok(new Dictionary<string, string>()));
        }
    }
}