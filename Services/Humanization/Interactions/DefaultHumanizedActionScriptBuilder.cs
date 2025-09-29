using System;
using System.Collections.Generic;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：默认脚本构建器，针对常见小红书操作生成拟人化动作序列。
/// English: Default script builder that generates humanized action sequences for common XiaoHongShu flows.
/// </summary>
public sealed class DefaultHumanizedActionScriptBuilder : IHumanizedActionScriptBuilder
{
    public HumanizedActionScript Build(HumanizedActionRequest request, HumanizedActionKind kind, string keyword)
    {
        var profile = string.IsNullOrWhiteSpace(request.BehaviorProfile) ? "default" : request.BehaviorProfile.Trim();
        var actions = new List<HumanizedAction>();

        switch (kind)
        {
            case HumanizedActionKind.RandomBrowse:
                BuildRandomBrowse(actions, profile);
                break;
            case HumanizedActionKind.KeywordBrowse:
                BuildKeywordBrowse(actions, profile, keyword);
                break;
            case HumanizedActionKind.Like:
                BuildLike(actions, profile);
                break;
            case HumanizedActionKind.Favorite:
                BuildFavorite(actions, profile);
                break;
            case HumanizedActionKind.Comment:
                BuildComment(actions, profile, request.CommentText);
                break;
            default:
                throw new NotSupportedException($"尚未支持的动作类型：{kind}");
        }

        if (actions.Count == 0)
        {
            throw new InvalidOperationException("脚本生成失败：动作列表为空。");
        }

        return new HumanizedActionScript(actions);
    }

    private static void BuildRandomBrowse(ICollection<HumanizedAction> actions, string profile)
    {
        actions.Add(HumanizedAction.Create(HumanizedActionType.Hover, new ActionLocator(Role: AriaRole.Button, Text: "筛选"), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.Click, new ActionLocator(Role: AriaRole.Button, Text: "筛选"), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.Click, new ActionLocator(Role: AriaRole.Radio, Text: "综合"), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.ScrollTo, new ActionLocator(Role: AriaRole.Button, Text: "最多评论"), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.Wheel, ActionLocator.Empty, parameters: new HumanizedActionParameters(wheelDeltaY: 420), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.MoveRandom, behaviorProfile: profile));
    }

    private static void BuildKeywordBrowse(ICollection<HumanizedAction> actions, string profile, string keyword)
    {
        var resolved = string.IsNullOrWhiteSpace(keyword) ? "热门" : keyword.Trim();
        actions.Add(HumanizedAction.Create(HumanizedActionType.Click, new ActionLocator(Role: AriaRole.Textbox, Placeholder: "搜索"), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.InputText, new ActionLocator(Role: AriaRole.Textbox, Placeholder: "搜索"),
            parameters: new HumanizedActionParameters(text: resolved), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.PressKey, new ActionLocator(Role: AriaRole.Textbox, Placeholder: "搜索"),
            parameters: new HumanizedActionParameters(text: "Enter"), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.WaitFor, new ActionLocator(Role: AriaRole.Main), timing: new HumanizedActionTiming(timeout: TimeSpan.FromSeconds(3)), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.Wheel, ActionLocator.Empty,
            parameters: new HumanizedActionParameters(wheelDeltaY: 520), behaviorProfile: profile));
    }

    private static void BuildLike(ICollection<HumanizedAction> actions, string profile)
    {
        actions.Add(HumanizedAction.Create(HumanizedActionType.ScrollTo, new ActionLocator(Role: AriaRole.Button, Text: "点赞"), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.Click, new ActionLocator(Role: AriaRole.Button, Text: "点赞"), behaviorProfile: profile));
    }

    private static void BuildFavorite(ICollection<HumanizedAction> actions, string profile)
    {
        actions.Add(HumanizedAction.Create(HumanizedActionType.ScrollTo, new ActionLocator(Role: AriaRole.Button, Text: "收藏"), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.Click, new ActionLocator(Role: AriaRole.Button, Text: "收藏"), behaviorProfile: profile));
    }

    private static void BuildComment(ICollection<HumanizedAction> actions, string profile, string? commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            throw new InvalidOperationException("评论动作必须提供 commentText。");
        }

        var normalized = commentText.Trim();
        actions.Add(HumanizedAction.Create(HumanizedActionType.ScrollTo, new ActionLocator(Role: AriaRole.Textbox, Placeholder: "写评论"), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.Click, new ActionLocator(Role: AriaRole.Textbox, Placeholder: "写评论"), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.InputText, new ActionLocator(Role: AriaRole.Textbox, Placeholder: "写评论"),
            parameters: new HumanizedActionParameters(text: normalized), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.Click, new ActionLocator(Role: AriaRole.Button, Text: "发布"), behaviorProfile: profile));
    }
}
