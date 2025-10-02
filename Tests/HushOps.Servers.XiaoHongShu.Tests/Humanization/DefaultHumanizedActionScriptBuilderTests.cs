using System;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Tests.Humanization;

public sealed class DefaultHumanizedActionScriptBuilderTests
{
    private readonly DefaultHumanizedActionScriptBuilder _builder = new();

    [Fact]
    public void BuildNavigateExplore_ShouldProduceNavigationSequence()
    {
        var request = new HumanizedActionRequest(Array.Empty<string>(), "", "", "user", "", "profileB");

        var script = _builder.Build(request, HumanizedActionKind.NavigateExplore, "");

        Assert.True(script.Actions.Count >= 5);

        var firstAction = script.Actions[0];
        Assert.Equal(HumanizedActionType.PressKey, firstAction.Type);
        Assert.Equal("Escape", firstAction.Parameters.Text);
        Assert.Equal("profileB", firstAction.BehaviorProfile);

        Assert.Contains(script.Actions, action =>
            action.Type == HumanizedActionType.Click &&
            string.Equals(action.Target.Text, "发现", StringComparison.Ordinal) &&
            action.BehaviorProfile == "profileB");

        Assert.Contains(script.Actions, action =>
            action.Type == HumanizedActionType.Wheel &&
            action.Parameters.WheelDeltaY == 300 &&
            action.BehaviorProfile == "profileB");
    }

    [Fact]
    public void BuildSearchKeyword_ShouldCreateSearchSequence()
    {
        var request = new HumanizedActionRequest(new[] { "护肤" }, "", "", "user", "", "profileA");

        var script = _builder.Build(request, HumanizedActionKind.SearchKeyword, "护肤");

        Assert.True(script.Actions.Count >= 4);
        Assert.Equal(HumanizedActionType.Click, script.Actions[0].Type);
        Assert.Equal(HumanizedActionType.InputText, script.Actions[1].Type);
        Assert.Equal("护肤", script.Actions[1].Parameters.Text);
        Assert.Equal(HumanizedActionType.PressKey, script.Actions[2].Type);
        Assert.Equal("profileA", script.Actions[0].BehaviorProfile);
    }

    [Fact]
    public void BuildKeywordBrowse_ShouldBrowseCurrentPage()
    {
        var request = new HumanizedActionRequest(new[] { "护肤" }, "", "", "user", "", "profileA");

        var script = _builder.Build(request, HumanizedActionKind.KeywordBrowse, "护肤");

        // KeywordBrowse 现在只在当前页面滚动，不进行搜索
        Assert.Contains(script.Actions, action => action.Type == HumanizedActionType.Wheel);
        Assert.Contains(script.Actions, action => action.Type == HumanizedActionType.MoveRandom);
        Assert.All(script.Actions, action => Assert.Equal("profileA", action.BehaviorProfile));
    }

    [Fact]
    public void BuildSelectNote_ShouldIncludeNoteClick()
    {
        var request = new HumanizedActionRequest(new[] { "旅拍" }, "", "", "user", "", "profileB");

        var script = _builder.Build(request, HumanizedActionKind.SelectNote, "旅拍");

        Assert.Contains(script.Actions, action =>
            action.Type == HumanizedActionType.Click &&
            string.Equals(action.Target.Text, "旅拍", StringComparison.Ordinal) &&
            action.BehaviorProfile == "profileB");

        Assert.Contains(script.Actions, action =>
            action.Type == HumanizedActionType.MoveRandom &&
            action.BehaviorProfile == "profileB");
    }

    [Fact]
    public void BuildCommentCurrentNote_ShouldValidateCommentText()
    {
        var request = new HumanizedActionRequest(Array.Empty<string>(), "", "", "user", "");

        Assert.Throws<InvalidOperationException>(() => _builder.Build(request, HumanizedActionKind.CommentCurrentNote, "keyword"));
    }

    [Fact]
    public void BuildRandomBrowse_ShouldIncludeWheelAndMove()
    {
        var request = new HumanizedActionRequest(Array.Empty<string>(), "", "", "user", "");

        var script = _builder.Build(request, HumanizedActionKind.RandomBrowse, "");

        Assert.Contains(script.Actions, action => action.Type == HumanizedActionType.Wheel);
        Assert.Contains(script.Actions, action => action.Type == HumanizedActionType.MoveRandom);
        Assert.All(script.Actions, action => Assert.Equal("default", action.BehaviorProfile));
    }

    [Fact]
    public void BuildLikeCurrentNote_ShouldIncludeLikeClick()
    {
        var request = new HumanizedActionRequest(Array.Empty<string>(), "", "", "user", "");

        var script = _builder.Build(request, HumanizedActionKind.LikeCurrentNote, "");

        Assert.Contains(script.Actions, action =>
            action.Type == HumanizedActionType.Click &&
            string.Equals(action.Target.Selector, ".like-wrapper", StringComparison.Ordinal) &&
            action.BehaviorProfile == "default");
    }

    [Fact]
    public void BuildFavoriteCurrentNote_ShouldIncludeFavoriteClick()
    {
        var request = new HumanizedActionRequest(Array.Empty<string>(), "", "", "user", "");

        var script = _builder.Build(request, HumanizedActionKind.FavoriteCurrentNote, "");

        Assert.Contains(script.Actions, action =>
            action.Type == HumanizedActionType.Click &&
            string.Equals(action.Target.Selector, ".collect-wrapper", StringComparison.Ordinal) &&
            action.BehaviorProfile == "default");
    }

    [Fact]
    public void BuildScrollBrowse_ShouldProduceScrollSequence()
    {
        var request = new HumanizedActionRequest(Array.Empty<string>(), "", "", "user", "", "profileA");

        var script = _builder.Build(request, HumanizedActionKind.ScrollBrowse, "");

        Assert.Equal(3, script.Actions.Count);

        var firstWheel = script.Actions[0];
        Assert.Equal(HumanizedActionType.Wheel, firstWheel.Type);
        Assert.Equal(400, firstWheel.Parameters.WheelDeltaY);
        Assert.Equal("profileA", firstWheel.BehaviorProfile);

        var moveRandom = script.Actions[1];
        Assert.Equal(HumanizedActionType.MoveRandom, moveRandom.Type);
        Assert.Equal("profileA", moveRandom.BehaviorProfile);

        var secondWheel = script.Actions[2];
        Assert.Equal(HumanizedActionType.Wheel, secondWheel.Type);
        Assert.Equal(350, secondWheel.Parameters.WheelDeltaY);
        Assert.Equal("profileA", secondWheel.BehaviorProfile);
    }
}
