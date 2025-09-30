# 设计文档 - 发布笔记功能

- **任务 ID**: TASK-20250130-001
- **来源**: 用户需求 - 增加发布笔记工具
- **更新时间**: 2025-01-30
- **责任人**: Claude
- **关联提交**: 待提交
- **状态**: 已完成

## 设计目标

为 HushOps.Servers.XiaoHongShu 实现完整的笔记发布功能,支持图片上传、标题正文编辑、暂存离开,遵循人性化交互原则。

## 方案对比

### 方案 A: 点击发布按钮 + 处理新标签页

**优点**:
- 模拟真实用户行为
- 无需硬编码 URL

**缺点**:
- 需要处理标签页切换逻辑
- 可靠性较低(按钮位置可能变化)
- 实现复杂度较高

### 方案 B: URL 直接导航(推荐)

**优点**:
- 简单可靠
- 性能更好
- 代码清晰易维护
- 避免标签页切换复杂性

**缺点**:
- URL 变更需要更新代码(风险低)

**决策**: 采用方案 B - URL 直接导航

**理由**:
1. 用户明确询问是否可以直接使用 URL
2. PlaywrightSessionManager 已有直接导航示例
3. 简单可靠,符合工程实践

## 系统设计

### 1. 数据流

```
用户调用 xhs_publish_note
    ↓
NotePublishTool.PublishNoteAsync
    ↓
创建 HumanizedActionRequest (包含 ImagePath, NoteTitle, NoteContent)
    ↓
调用 HumanizedActionService.ExecuteAsync (HumanizedActionKind.PublishNote)
    ↓
DefaultHumanizedActionScriptBuilder.BuildPublishNote
    ↓
生成 HumanizedActionScript (包含 UploadFile, Click, InputText 等动作)
    ↓
HumanizedInteractionExecutor.ExecuteAsync
    ↓
逐个执行拟人化动作
```

### 2. 核心类扩展

#### 2.1 HumanizedActionKind 枚举

```csharp
public enum HumanizedActionKind
{
    // ... 现有类型 ...
    PublishNote  // 新增
}
```

#### 2.2 HumanizedActionRequest 记录

```csharp
public sealed record HumanizedActionRequest(
    IReadOnlyList<string> Keywords,
    string? PortraitId,
    string? CommentText,
    string BrowserKey,
    string? RequestId,
    string BehaviorProfile = "default",
    string? ImagePath = null,      // 新增:图片路径
    string? NoteTitle = null,      // 新增:标题
    string? NoteContent = null);   // 新增:正文
```

#### 2.3 HumanizedActionType 枚举

```csharp
public enum HumanizedActionType
{
    // ... 现有类型 ...
    UploadFile  // 新增(基础设施已存在)
}
```

### 3. 新增工具类

#### NotePublishTool

```csharp
[McpServerToolType]
public sealed class NotePublishTool
{
    [McpServerTool(Name = "xhs_publish_note")]
    public async Task<OperationResult<NotePublishResult>> PublishNoteAsync(
        string imagePath,              // 必填
        string? noteTitle = null,      // 可选,默认"分享日常"
        string? noteContent = null,    // 可选,默认"记录美好瞬间"
        string? browserKey = null,
        string? behaviorProfile = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 导航到发布页面
        // 2. 执行 PublishNote 动作脚本
        // 3. 返回结果
    }
}
```

### 4. 动作脚本设计

`BuildPublishNote` 方法生成的动作序列:

```csharp
1. UploadFile (上传图片)
   - Target: Selector "input.upload-input[type='file']"
   - Parameters: filePath

2. MoveRandom (等待上传完成)

3. Click (点击标题输入框)
   - Target: Placeholder "填写标题会有更多赞哦～"

4. InputText (填写标题)
   - Target: Placeholder "填写标题会有更多赞哦～"
   - Parameters: text

5. Click (点击正文输入框)
   - Target: Selector ".tiptap.ProseMirror"

6. InputText (填写正文)
   - Target: Selector ".tiptap.ProseMirror"
   - Parameters: text

7. MoveRandom (模拟用户思考)

8. Click (点击暂存离开)
   - Target: Text "暂存离开"
```

## 技术决策

### 决策 1: 文件上传实现

**选择**: 使用 Playwright `SetInputFilesAsync` API

**理由**:
- Playwright 原生支持,无需额外处理
- 可靠性高
- 已有 `PerformUploadFileAsync` 基础设施

### 决策 2: URL 跳转位置

**选择**: 在 `NotePublishTool.PublishNoteAsync` 中处理导航

**理由**:
- 导航是工具特定逻辑,不属于通用人性化动作
- 保持 HumanizedActionScript 纯净(只包含页面内交互)
- 更清晰的职责分离

### 决策 3: 默认值设计

**选择**:
- 标题默认值: "分享日常"
- 正文默认值: "记录美好瞬间"

**理由**:
- 提供合理默认值,降低使用门槛
- 避免空标题/正文被小红书拒绝
- 用户可根据需要覆盖

## 接口设计

### MCP 工具接口

```json
{
  "name": "xhs_publish_note",
  "description": "发布笔记（上传图片、填写标题正文、暂存离开）| Publish note (upload image, fill title/content, save draft and leave)",
  "inputSchema": {
    "type": "object",
    "properties": {
      "imagePath": {
        "type": "string",
        "description": "图片文件路径 | Image file path"
      },
      "noteTitle": {
        "type": "string",
        "description": "笔记标题，不填则使用默认标题 | Note title, defaults to generic title"
      },
      "noteContent": {
        "type": "string",
        "description": "笔记正文，不填则使用默认正文 | Note content, defaults to generic content"
      },
      "browserKey": {
        "type": "string",
        "description": "浏览器键，user 表示用户配置 | Browser key: 'user' for user profile"
      },
      "behaviorProfile": {
        "type": "string",
        "description": "行为档案键，默认 default | Behavior profile key"
      }
    },
    "required": ["imagePath"]
  }
}
```

### 返回结果

```csharp
public sealed record NotePublishResult(
    string ImagePath,
    string? Title,
    string? Content,
    string Message);
```

## 风险缓解

1. **文件路径验证**: 在上传前验证文件是否存在
2. **超时处理**: 为上传和页面加载设置合理超时
3. **错误恢复**: 失败时记录详细日志,便于问题定位
4. **URL 稳定性**: 文档中记录 URL 来源,便于维护

## 兼容性考虑

1. **向后兼容**: 新增参数全部为可选,不影响现有代码
2. **测试覆盖**: 现有 51 个测试保持通过
3. **文档同步**: 更新 CLAUDE.md 和 README.md

## 下一步

进入实现阶段,按照设计方案编码。