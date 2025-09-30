# 研究文档 - 发布笔记功能

- **任务 ID**: TASK-20250130-001
- **来源**: 用户需求 - 增加发布笔记工具支持图片上传、标题正文编辑、暂存离开
- **更新时间**: 2025-01-30
- **责任人**: Claude
- **状态**: 已完成

## 需求背景

用户要求实现一个新的 MCP 工具,用于在小红书创作者平台发布笔记,包含以下核心功能:
1. 接收图片、标题、正文参数
2. 导航到发布页面
3. 上传图片(必须先上传,才会显示编辑框)
4. 填写标题和正文
5. 点击"暂存离开"按钮

用户特别询问:**是否可以直接使用 URL 跳转而非点击发布按钮?**

## 研究方法

1. **访问小红书网站检查发布页面流程**
   - 首页发布按钮位置: 顶部导航栏
   - 点击后行为: 打开新标签页(`target="_blank"`)
   - 目标 URL: `https://creator.xiaohongshu.com/publish/publish?source=official`

2. **检查上传界面元素**
   - 文件输入元素: `input.upload-input[type="file"]`
   - 接受格式: `.jpg,.jpeg,.png,.webp`
   - 支持多文件上传: `multiple: true`
   - 上传按钮: `button.upload-button` 文本"上传图片"

3. **实际上传图片验证后续界面**
   - 使用系统自带图片: `C:/Windows/Web/Wallpaper/Windows/img0.jpg`
   - 通过 Chrome DevTools `upload_file` API 成功上传
   - 上传后页面变化:编辑界面显示

4. **编辑界面元素定位**
   - 标题输入框:
     - 类型: `<input>` 标签
     - Placeholder: `"填写标题会有更多赞哦～"`
     - Class: `d-text`
   - 正文输入框:
     - 类型: `<div contenteditable="true">`
     - Class: `tiptap ProseMirror`
     - Placeholder 提示: `"输入正文描述，真诚有价值的分享予人温暖"`
   - 暂存离开按钮:
     - 文本: `"暂存离开"`
     - Class: `cancelBtn`
   - 发布按钮:
     - 文本: `"发布"`
     - Class: `publishBtn red`

## 研究结论

### 1. URL 直接导航可行且推荐

**结论**: **强烈推荐使用 URL 直接跳转**

理由:
- 发布页面 URL 固定且稳定
- 比模拟点击更可靠
- 避免处理新标签页切换复杂性
- PlaywrightSessionManager 已有直接导航示例(`page.GotoAsync`)
- 性能更好,速度更快

### 2. 元素定位策略

| 元素 | 优先定位器 | 备用定位器 |
|------|-----------|-----------|
| 文件上传 | `Selector: "input.upload-input[type='file']"` | - |
| 标题输入 | `Placeholder: "填写标题会有更多赞哦～"` | `Selector: "input.d-text"` |
| 正文输入 | `Selector: ".tiptap.ProseMirror"` | - |
| 暂存按钮 | `Text: "暂存离开"` | `Selector: ".cancelBtn"` |
| 发布按钮 | `Text: "发布"` | `Selector: ".publishBtn"` |

### 3. 执行流程设计

```
1. 导航到发布页面 (URL 直接跳转)
   ↓
2. 上传图片文件 (使用 Playwright SetInputFilesAsync)
   ↓
3. 等待编辑界面显示 (随机鼠标移动模拟等待)
   ↓
4. 点击标题输入框 → 填写标题
   ↓
5. 点击正文输入框 → 填写正文
   ↓
6. 随机鼠标移动 (模拟用户思考)
   ↓
7. 点击"暂存离开"按钮
```

### 4. 技术约束

1. **文件上传必须在前**: 编辑框元素在上传前不存在于 DOM
2. **HumanizedActionType 需扩展**: 需添加 `UploadFile` 动作类型
3. **HumanizedActionRequest 需扩展**: 需添加 `ImagePath`、`NoteTitle`、`NoteContent` 参数
4. **HumanizedActionKind 需扩展**: 需添加 `PublishNote` 动作类型

## 风险评估

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| 上传后页面加载慢 | 中 | 中 | 添加合理的等待时间和状态检测 |
| 文件路径不存在 | 中 | 高 | 参数验证和明确错误提示 |
| contenteditable 输入兼容性 | 低 | 中 | 使用 Playwright 标准 API |
| URL 变更 | 低 | 高 | 记录 URL 来源,便于后续更新 |

## 参考资料

- Playwright SetInputFilesAsync API: https://playwright.dev/dotnet/docs/input#upload-files
- 小红书创作者平台: https://creator.xiaohongshu.com
- Chrome DevTools Protocol 文件上传能力

## 下一步

进入设计阶段,制定具体实现方案。