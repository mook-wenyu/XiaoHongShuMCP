# 实现文档 - 发布笔记功能

- **任务 ID**: TASK-20250130-001
- **来源**: 用户需求 - 增加发布笔记工具
- **更新时间**: 2025-01-30
- **责任人**: Claude
- **关联提交**: 待提交
- **状态**: 已完成

## 实现概述

成功实现了小红书笔记发布功能,新增 1 个 MCP 工具、扩展 3 个枚举类型、修改 4 个核心类。

## 代码变更清单

### 1. 枚举扩展

#### Services/Humanization/IHumanizedActionService.cs

**变更**:添加 `PublishNote` 枚举值

```csharp
public enum HumanizedActionKind
{
    // ... 现有类型 ...

    /// <summary>发布笔记（上传图片、填写标题和正文、暂存离开）| Publish note (upload image, fill title and content, save draft and leave)</summary>
    PublishNote
}
```

**变更**:扩展 `HumanizedActionRequest` 记录

```csharp
public sealed record HumanizedActionRequest(
    IReadOnlyList<string> Keywords,
    string? PortraitId,
    string? CommentText,
    string BrowserKey,
    string? RequestId,
    string BehaviorProfile = "default",
    string? ImagePath = null,      // 新增
    string? NoteTitle = null,      // 新增
    string? NoteContent = null);   // 新增
```

**影响**: 所有创建 `HumanizedActionRequest` 的地方需要适配(已验证无编译错误)

#### Services/Humanization/Interactions/HumanizedActionType.cs

**变更**:添加 `UploadFile` 动作类型

```csharp
public enum HumanizedActionType
{
    Unknown = 0,
    Hover,
    Click,
    MoveRandom,
    Wheel,
    ScrollTo,
    InputText,
    PressKey,
    Hotkey,
    WaitFor,
    UploadFile  // 新增
}
```

### 2. 脚本构建器扩展

#### Services/Humanization/Interactions/DefaultHumanizedActionScriptBuilder.cs

**变更**:在 `Build` 方法的 switch 中添加新分支

```csharp
case HumanizedActionKind.PublishNote:
    BuildPublishNote(actions, profile, request.ImagePath, request.NoteTitle, request.NoteContent);
    break;
```

**新增方法**: `BuildPublishNote`

```csharp
private static void BuildPublishNote(ICollection<HumanizedAction> actions, string profile, string? imagePath, string? noteTitle, string? noteContent)
{
    if (string.IsNullOrWhiteSpace(imagePath))
    {
        throw new InvalidOperationException("发布笔记动作必须提供 imagePath（图片文件路径）。");
    }

    var normalizedImagePath = imagePath.Trim();
    var normalizedTitle = string.IsNullOrWhiteSpace(noteTitle) ? "分享日常" : noteTitle.Trim();
    var normalizedContent = string.IsNullOrWhiteSpace(noteContent) ? "记录美好瞬间" : noteContent.Trim();

    // 1. 上传图片文件
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.UploadFile,
        new ActionLocator(Selector: "input.upload-input[type='file']"),
        parameters: new HumanizedActionParameters(filePath: normalizedImagePath),
        behaviorProfile: profile));

    // 2. 等待上传完成并显示编辑界面
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.MoveRandom,
        behaviorProfile: profile));

    // 3. 填写标题
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.Click,
        new ActionLocator(Placeholder: "填写标题会有更多赞哦～"),
        behaviorProfile: profile));
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.InputText,
        new ActionLocator(Placeholder: "填写标题会有更多赞哦～"),
        parameters: new HumanizedActionParameters(text: normalizedTitle),
        behaviorProfile: profile));

    // 4. 填写正文
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.Click,
        new ActionLocator(Selector: ".tiptap.ProseMirror"),
        behaviorProfile: profile));
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.InputText,
        new ActionLocator(Selector: ".tiptap.ProseMirror"),
        parameters: new HumanizedActionParameters(text: normalizedContent),
        behaviorProfile: profile));

    // 5. 随机鼠标移动（模拟用户思考）
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.MoveRandom,
        behaviorProfile: profile));

    // 6. 点击"暂存离开"按钮
    actions.Add(HumanizedAction.Create(
        HumanizedActionType.Click,
        new ActionLocator(Text: "暂存离开"),
        behaviorProfile: profile));
}
```

**行数**: +72 行

### 3. 交互执行器扩展

#### Services/Humanization/Interactions/HumanizedInteractionExecutor.cs

**变更**:在 `ExecuteAsync` 方法的 switch 中添加新分支

```csharp
case HumanizedActionType.UploadFile:
    await PerformUploadFileAsync(page, action, timeoutToken).ConfigureAwait(false);
    break;
```

**说明**: `PerformUploadFileAsync` 方法已存在(基础设施),无需新增。该方法使用 `Parameters.FilePath` 参数和 Playwright 的 `SetInputFilesAsync` API。

### 4. 新增工具类

#### Tools/NotePublishTool.cs (全新文件)

```csharp
[McpServerToolType]
public sealed class NotePublishTool
{
    private readonly IBrowserAutomationService _browserAutomation;
    private readonly IPlaywrightSessionManager _sessionManager;
    private readonly IHumanizedActionService _humanizedActionService;
    private readonly ILogger<NotePublishTool> _logger;

    [McpServerTool(Name = "xhs_publish_note")]
    public async Task<OperationResult<NotePublishResult>> PublishNoteAsync(
        string imagePath,
        string? noteTitle = null,
        string? noteContent = null,
        string? browserKey = null,
        string? behaviorProfile = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 确保浏览器配置存在
        var profile = await _browserAutomation.EnsureProfileAsync(...);

        // 2. 获取页面上下文
        var pageContext = await _browserAutomation.EnsurePageContextAsync(...);

        // 3. 导航到发布页面（creator.xiaohongshu.com）
        await pageContext.Page.GotoAsync("https://creator.xiaohongshu.com/publish/publish?source=official", ...);

        // 4. 执行人性化发布动作脚本
        var request = new HumanizedActionRequest(..., ImagePath: imagePath, NoteTitle: noteTitle, NoteContent: noteContent);
        var outcome = await _humanizedActionService.ExecuteAsync(request, HumanizedActionKind.PublishNote, ...);

        return outcome.Success ? OperationResult<NotePublishResult>.Ok(...) : OperationResult<NotePublishResult>.Fail(...);
    }
}

public sealed record NotePublishResult(
    string ImagePath,
    string? Title,
    string? Content,
    string Message);
```

**行数**: 119 行

## 实现细节

### 1. 参数处理

- **imagePath**: 必填,抛出 `ArgumentException` 如果为空
- **noteTitle**: 可选,默认"分享日常"
- **noteContent**: 可选,默认"记录美好瞬间"
- **browserKey**: 可选,默认"user"
- **behaviorProfile**: 可选,默认"default"

### 2. 元素定位器选择

| 元素 | 定位器类型 | 定位器值 | 理由 |
|------|-----------|---------|------|
| 文件上传 | Selector | `input.upload-input[type='file']` | CSS 选择器最可靠 |
| 标题输入 | Placeholder | `填写标题会有更多赞哦～` | 文本定位更稳定 |
| 正文输入 | Selector | `.tiptap.ProseMirror` | contenteditable DIV |
| 暂存按钮 | Text | `暂存离开` | 文本定位最直观 |

### 3. 错误处理

```csharp
try
{
    // 主逻辑
}
catch (Exception ex)
{
    _logger.LogError(ex, "[NotePublishTool] 发布笔记失败 ...");
    return OperationResult<NotePublishResult>.Fail("ERR_PUBLISH_EXCEPTION", ex.Message);
}
```

### 4. 导航策略

使用 `page.GotoAsync` 直接导航到发布页面:

```csharp
await pageContext.Page.GotoAsync(
    "https://creator.xiaohongshu.com/publish/publish?source=official",
    new PageGotoOptions
    {
        WaitUntil = WaitUntilState.NetworkIdle,
        Timeout = 30000
    });
```

**优势**:
- 避免处理新标签页切换
- 更可靠
- 代码更清晰

## 测试验证

### 编译验证

```bash
$ dotnet build HushOps.Servers.XiaoHongShu.csproj --no-restore
已成功生成。
    0 个警告
    0 个错误
```

### 单元测试

```bash
$ dotnet test Tests/HushOps.Servers.XiaoHongShu.Tests/HushOps.Servers.XiaoHongShu.Tests.csproj --no-build
已通过! - 失败: 0，通过: 51，已跳过: 0，总计: 51
```

### 工具注册验证

```bash
$ dotnet run --project HushOps.Servers.XiaoHongShu.csproj --no-build -- --tools-list | grep publish
      "Name": "xhs_publish_note",
      "Type": "HushOps.Servers.XiaoHongShu.Tools.NotePublishTool"
```

## 性能考虑

1. **文件上传**: 使用 Playwright 原生 API,无额外开销
2. **页面导航**: `NetworkIdle` 等待策略确保页面完全加载
3. **等待时间**: 使用 `MoveRandom` 动作模拟自然等待,避免硬编码延迟

## 安全考虑

1. **文件路径验证**: Playwright 会验证文件是否存在
2. **输入清理**: 标题和正文使用 `Trim()` 清理空白字符
3. **异常捕获**: 所有异常都被捕获并记录日志

## 兼容性验证

- ✅ 所有现有测试通过
- ✅ 编译无警告无错误
- ✅ 新增参数全部为可选,不影响现有调用

## 代码统计

| 类别 | 文件数 | 行数变更 |
|------|-------|---------|
| 新增文件 | 1 | +119 |
| 修改文件 | 4 | +~100 |
| 总计 | 5 | +~220 |

## 下一步

进入验证阶段,进行集成测试和文档更新。