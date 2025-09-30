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
            case HumanizedActionKind.NavigateExplore:
                BuildNavigateExplore(actions, profile);
                break;
            case HumanizedActionKind.SearchKeyword:
                BuildSearchKeyword(actions, profile, keyword);
                break;
            case HumanizedActionKind.SelectNote:
                BuildSelectNote(actions, profile, request.Keywords);
                break;
            case HumanizedActionKind.LikeCurrentNote:
                BuildLikeCurrentNote(actions, profile);
                break;
            case HumanizedActionKind.FavoriteCurrentNote:
                BuildFavoriteCurrentNote(actions, profile);
                break;
            case HumanizedActionKind.CommentCurrentNote:
                BuildCommentCurrentNote(actions, profile, request.CommentText);
                break;
            case HumanizedActionKind.ScrollBrowse:
                BuildScrollBrowse(actions, profile);
                break;
            case HumanizedActionKind.PublishNote:
                BuildPublishNote(actions, profile, request.ImagePath, request.NoteTitle, request.NoteContent);
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

    /// <summary>
    /// 中文:随机浏览当前页面（不进行导航，只在当前页面操作）。
    /// English: Randomly browse the current page without navigation.
    /// </summary>
    private static void BuildRandomBrowse(ICollection<HumanizedAction> actions, string profile)
    {
        // 滚动浏览当前页面
        actions.Add(HumanizedAction.Create(HumanizedActionType.Wheel, ActionLocator.Empty, parameters: new HumanizedActionParameters(wheelDeltaY: 420), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.MoveRandom, behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.Wheel, ActionLocator.Empty, parameters: new HumanizedActionParameters(wheelDeltaY: 300), behaviorProfile: profile));
    }

    /// <summary>
    /// 中文：根据关键词数组浏览当前页面（命中任一关键词即成功，不进行搜索导航）。
    /// English: Browse current page by keyword array matching (no search navigation).
    /// </summary>
    private static void BuildKeywordBrowse(ICollection<HumanizedAction> actions, string profile, string keyword)
    {
        // 在当前页面滚动寻找包含关键词的内容
        actions.Add(HumanizedAction.Create(HumanizedActionType.Wheel, ActionLocator.Empty,
            parameters: new HumanizedActionParameters(wheelDeltaY: 520), behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.MoveRandom, behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(HumanizedActionType.Wheel, ActionLocator.Empty,
            parameters: new HumanizedActionParameters(wheelDeltaY: 400), behaviorProfile: profile));
    }

    /// <summary>
    /// 中文：导航到发现页（主页的发现频道）。
    /// English: Navigate to discover page (discover channel on explore page).
    /// </summary>
    private static void BuildNavigateExplore(ICollection<HumanizedAction> actions, string profile)
    {
        // 步骤1: 尝试关闭可能存在的模态遮罩(使用ESC键)
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.PressKey,
            ActionLocator.Empty,
            parameters: new HumanizedActionParameters(text: "Escape"),
            behaviorProfile: profile));

        // 步骤2: 短暂等待模态关闭动画
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.MoveRandom,
            behaviorProfile: profile));

        // 步骤3: 点击"发现"链接
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Click,
            new ActionLocator(Text: "发现"),
            behaviorProfile: profile));

        // 步骤4: 随机鼠标移动（模拟等待页面加载）
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.MoveRandom,
            behaviorProfile: profile));

        // 步骤5: 滚动浏览内容
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Wheel,
            ActionLocator.Empty,
            parameters: new HumanizedActionParameters(wheelDeltaY: 300),
            behaviorProfile: profile));
    }

    /// <summary>
    /// 中文：在搜索框中输入关键词并执行搜索。
    /// English: Enter keyword in search box and execute search.
    /// </summary>
    private static void BuildSearchKeyword(ICollection<HumanizedAction> actions, string profile, string keyword)
    {
        var resolved = string.IsNullOrWhiteSpace(keyword) ? "热门" : keyword.Trim();

        // 使用实际网站的 placeholder "搜索小红书"
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Click,
            new ActionLocator(Placeholder: "搜索小红书"),
            behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.InputText,
            new ActionLocator(Placeholder: "搜索小红书"),
            parameters: new HumanizedActionParameters(text: resolved),
            behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.PressKey,
            new ActionLocator(Placeholder: "搜索小红书"),
            parameters: new HumanizedActionParameters(text: "Enter"),
            behaviorProfile: profile));
        // 随机鼠标移动（模拟等待搜索结果加载）
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.MoveRandom,
            behaviorProfile: profile));
    }

    /// <summary>
    /// 中文：根据关键词数组选择笔记（命中任意关键词即成功）。
    /// English: Select note by keyword array matching (success if any keyword matches).
    /// </summary>
    private static void BuildSelectNote(ICollection<HumanizedAction> actions, string profile, IReadOnlyList<string> keywords)
    {
        if (keywords == null || keywords.Count == 0)
        {
            throw new InvalidOperationException("选择笔记动作必须提供关键词数组。");
        }

        // 滚动以加载更多笔记
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Wheel,
            ActionLocator.Empty,
            parameters: new HumanizedActionParameters(wheelDeltaY: 360),
            behaviorProfile: profile));

        // 使用关键词文本来查找并点击笔记（基于笔记标题文本匹配）
        // 注意：InteractionLocatorBuilder 会尝试匹配包含任一关键词的文本
        var keywordText = keywords[0]; // 使用第一个关键词作为定位线索
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Click,
            new ActionLocator(Text: keywordText),
            behaviorProfile: profile));

        // 随机鼠标移动（模拟等待笔记详情页加载）
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.MoveRandom,
            behaviorProfile: profile));
    }

    /// <summary>
    /// 中文：点赞当前打开的笔记。
    /// English: Like the currently open note.
    /// </summary>
    private static void BuildLikeCurrentNote(ICollection<HumanizedAction> actions, string profile)
    {
        // 滚动到点赞按钮位置（通常在笔记右侧）
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Wheel,
            ActionLocator.Empty,
            parameters: new HumanizedActionParameters(wheelDeltaY: 200),
            behaviorProfile: profile));
        // 使用CSS选择器定位点赞按钮（小红书使用.like-wrapper容器）
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Click,
            new ActionLocator(Selector: ".like-wrapper"),
            behaviorProfile: profile));
    }

    /// <summary>
    /// 中文：收藏当前打开的笔记。
    /// English: Favorite the currently open note.
    /// </summary>
    private static void BuildFavoriteCurrentNote(ICollection<HumanizedAction> actions, string profile)
    {
        // 滚动到收藏按钮位置
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Wheel,
            ActionLocator.Empty,
            parameters: new HumanizedActionParameters(wheelDeltaY: 200),
            behaviorProfile: profile));
        // 使用CSS选择器定位收藏按钮（小红书使用.collect-wrapper容器）
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Click,
            new ActionLocator(Selector: ".collect-wrapper"),
            behaviorProfile: profile));
    }

    /// <summary>
    /// 中文：评论当前打开的笔记。
    /// English: Comment on the currently open note.
    /// </summary>
    private static void BuildCommentCurrentNote(ICollection<HumanizedAction> actions, string profile, string? commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            throw new InvalidOperationException("评论动作必须提供 commentText。");
        }

        var normalized = commentText.Trim();
        // 滚动到评论区
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Wheel,
            ActionLocator.Empty,
            parameters: new HumanizedActionParameters(wheelDeltaY: 400),
            behaviorProfile: profile));
        // 使用ID定位评论输入框（小红书使用contenteditable的p标签，id为content-textarea）
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Click,
            new ActionLocator(Id: "content-textarea"),
            behaviorProfile: profile));
        // 输入评论文本
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.InputText,
            new ActionLocator(Id: "content-textarea"),
            parameters: new HumanizedActionParameters(text: normalized),
            behaviorProfile: profile));
        // 点击发布按钮（发送按钮文本为"发送"）
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Click,
            new ActionLocator(Text: "发送"),
            behaviorProfile: profile));
    }

    /// <summary>
    /// 中文：拟人化滚动浏览当前页面（不进行导航，只执行滚动操作）。
    /// English: Humanized scroll browsing on current page without navigation.
    /// </summary>
    private static void BuildScrollBrowse(ICollection<HumanizedAction> actions, string profile)
    {
        // 第一次滚动
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Wheel,
            ActionLocator.Empty,
            parameters: new HumanizedActionParameters(wheelDeltaY: 400),
            behaviorProfile: profile));

        // 随机鼠标移动（模拟真实用户）
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.MoveRandom,
            behaviorProfile: profile));

        // 第二次滚动
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Wheel,
            ActionLocator.Empty,
            parameters: new HumanizedActionParameters(wheelDeltaY: 350),
            behaviorProfile: profile));
    }

    /// <summary>
    /// 中文：发布笔记（上传图片、填写标题和正文、暂存离开）。
    /// English: Publish note (upload image, fill title and content, save draft and leave).
    /// </summary>
    private static void BuildPublishNote(ICollection<HumanizedAction> actions, string profile, string? imagePath, string? noteTitle, string? noteContent)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new InvalidOperationException("发布笔记动作必须提供 imagePath（图片文件路径）。");
        }

        var normalizedImagePath = imagePath.Trim();
        var normalizedTitle = string.IsNullOrWhiteSpace(noteTitle) ? "分享日常" : noteTitle.Trim();
        var normalizedContent = string.IsNullOrWhiteSpace(noteContent) ? "记录美好瞬间" : noteContent.Trim();

        // 1. 导航到发布页面（使用 URL 直接跳转，比点击更可靠）
        // 注意：此处需要特殊处理，因为 HumanizedActionType 没有 Navigate 类型
        // 我们使用 WaitFor 作为占位符，实际导航由工具层处理

        // 2. 上传图片文件
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.UploadFile,
            new ActionLocator(Selector: "input.upload-input[type='file']"),
            parameters: new HumanizedActionParameters(filePath: normalizedImagePath),
            behaviorProfile: profile));

        // 3. 等待上传完成并显示编辑界面
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.MoveRandom,
            behaviorProfile: profile));

        // 4. 填写标题
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Click,
            new ActionLocator(Placeholder: "填写标题会有更多赞哦～"),
            behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.InputText,
            new ActionLocator(Placeholder: "填写标题会有更多赞哦～"),
            parameters: new HumanizedActionParameters(text: normalizedTitle),
            behaviorProfile: profile));

        // 5. 填写正文
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Click,
            new ActionLocator(Selector: ".tiptap.ProseMirror"),
            behaviorProfile: profile));
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.InputText,
            new ActionLocator(Selector: ".tiptap.ProseMirror"),
            parameters: new HumanizedActionParameters(text: normalizedContent),
            behaviorProfile: profile));

        // 6. 随机鼠标移动（模拟用户思考）
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.MoveRandom,
            behaviorProfile: profile));

        // 7. 点击"暂存离开"按钮
        actions.Add(HumanizedAction.Create(
            HumanizedActionType.Click,
            new ActionLocator(Text: "暂存离开"),
            behaviorProfile: profile));
    }
}
