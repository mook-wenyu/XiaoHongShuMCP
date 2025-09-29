using System;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Tests.Humanization;

public sealed class DefaultHumanizedActionScriptBuilderTests
{
    private readonly DefaultHumanizedActionScriptBuilder _builder = new();

    [Fact]
    public void BuildKeywordBrowse_ShouldCreateSearchSequence()
    {
        var request = new HumanizedActionRequest("护肤", null, null, true, "user", null, "profileA");

        var script = _builder.Build(request, HumanizedActionKind.KeywordBrowse, "护肤");

        Assert.Equal(5, script.Actions.Count);
        Assert.Equal(HumanizedActionType.Click, script.Actions[0].Type);
        Assert.Equal(HumanizedActionType.InputText, script.Actions[1].Type);
        Assert.Equal("护肤", script.Actions[1].Parameters.Text);
        Assert.Equal(HumanizedActionType.PressKey, script.Actions[2].Type);
        Assert.Equal("profileA", script.Actions[0].BehaviorProfile);
    }

    [Fact]
    public void BuildComment_ShouldValidateCommentText()
    {
        var request = new HumanizedActionRequest("keyword", null, null, true, "user", null);

        Assert.Throws<InvalidOperationException>(() => _builder.Build(request, HumanizedActionKind.Comment, "keyword"));
    }

    [Fact]
    public void BuildRandomBrowse_ShouldIncludeWheelAndMove()
    {
        var request = new HumanizedActionRequest(null, null, null, true, "user", null);

        var script = _builder.Build(request, HumanizedActionKind.RandomBrowse, "");

        Assert.Contains(script.Actions, action => action.Type == HumanizedActionType.Wheel);
        Assert.Contains(script.Actions, action => action.Type == HumanizedActionType.MoveRandom);
        Assert.All(script.Actions, action => Assert.Equal("default", action.BehaviorProfile));
    }
}
