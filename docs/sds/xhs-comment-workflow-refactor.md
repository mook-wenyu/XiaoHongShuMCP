# 小红书拟人化评论工作流重构设计（阶段C 第二步）

## 1. 背景与目标
- 当前 `XiaoHongShuService` 聚合了意图编排、页面守护、交互执行、API 反馈等多重职责，类体量超过 3K 行，难以测试与维护。
- 阶段C 要求“拟人化运营流程”分层，删除视觉兜底接口，并以 Locator 优先策略支撑交互。
- 本设计聚焦评论/收藏/点赞等核心交互路径，将其从 `XiaoHongShuService` 抽离为可组合、可测试的工作流组件，并为后续搜索/运营闭环奠定架构基础。

## 2. 分层结构
```
┌────────────────────────────────────────────────────────────┐
│ IXiaoHongShuService (MCP 对外接口，保持不变)              │
│   ├─ 调用 ICommentWorkflow.PostCommentAsync                │
│   ├─ 调用 ICommentWorkflow.TemporarySaveAsync             │
│   ├─ 调用 INoteEngagementWorkflow.Like/Favorite/...          │
│   ├─ 调用 IIntentOrchestrator.GetNoteDetailAsync（下一步） │
└────────────────────────────────────────────────────────────┘
           │依赖注入
           ▼
┌────────────────────────────┐
│ ICommentWorkflow           │  —— 本轮实现
│  • 评论意图编排             │
│  • 调用 IPageGuardian       │
│  • 调用 IInteractionExecutor│
│  • 调用 IFeedbackCoordinator│
└────────────────────────────┘
           │
           ├──────────────┐───────────────┐
           ▼              ▼               ▼
┌─────────────────┐ ┌─────────────────┐ ┌────────────────────┐
│ IPageGuardian   │ │ IInteractionExecutor│ │ IFeedbackCoordinator │
│ • 页面状态探测   │ │ • 拟人输入/点击    │ │ • API 监控/审计        │
│ • 等待/校验逻辑 │ │ • Emoji/标签注入 │ │ • 指标采集与回调      │
└─────────────────┘ └─────────────────┘ └────────────────────┘
           │
  ┌────────┴────────┐
  ▼                 ▼
`LocatorSelectorsCatalog` （已抽离）  与  Core Playwright 适配层
```

## 3. 新增接口定义（草案）
- `ICommentWorkflow`
  - `Task<OperationResult<CommentResult>> PostCommentAsync(string keyword, string content, CancellationToken ct)`
  - `Task<OperationResult<TemporarySaveResult>> TemporarySaveAndLeaveAsync(string draftTitle, string draftContent, CancellationToken ct)`
- `IInteractionExecutor`
  - `Task InputCommentAsync(IAutoPage page, CommentDraft draft, CancellationToken ct)`
  - `Task ExecuteAsync(InteractionAction action, IAutoPage page, CancellationToken ct)`
- `INoteEngagementWorkflow`
  - `Task<OperationResult<InteractionBundleResult>> InteractAsync(string keyword, bool like, bool favorite, CancellationToken ct)`
  - `Task<OperationResult<InteractionResult>> LikeAsync(string keyword, CancellationToken ct)`
  - `Task<OperationResult<InteractionResult>> FavoriteAsync(string keyword, CancellationToken ct)`
  - `Task<OperationResult<InteractionResult>> UnlikeAsync(string keyword, CancellationToken ct)`
  - `Task<OperationResult<InteractionResult>> UncollectAsync(string keyword, CancellationToken ct)`
- `INoteDiscoveryService`
  - `Task<IElementHandle?> FindMatchingNoteElementAsync(string keyword, CancellationToken ct)`
  - `Task<OperationResult<List<IElementHandle>>> FindVisibleMatchingNotesAsync(string keyword, int maxCount, CancellationToken ct)`
  - `Task<bool> DoesDetailMatchKeywordAsync(IPage page, string keyword, CancellationToken ct)`
- `IPageGuardian`
  - `Task<PageStatusInfo> InspectPageAsync(IPage page, PageType expected, CancellationToken ct)`
  - `Task<bool> WaitForLocatorAsync(IAutoPage page, string alias, TimeSpan timeout, CancellationToken ct)`
  - `Task<bool> ActivateCommentAreaAsync(IPage page, CancellationToken ct)`
- `IFeedbackCoordinator`
  - `Task<ApiFeedback> ObserveAsync(ApiEndpointType endpoint, CancellationToken ct)`
  - `void Audit(string operation, string keyword, FeedbackContext context)`

> 以上接口最终以 `internal` + DI 形式提供，确保 MCP 层只暴露 `IXiaoHongShuService`。

## 4. 依赖映射
| 组件 | 注入依赖 | 说明 |
| --- | --- | --- |
| CommentWorkflow | `IBrowserManager`, `IAccountManager`, `IPageStateGuard`, `IPageGuardian`, `IInteractionExecutor`, `IFeedbackCoordinator`, `XhsSettings.DetailMatchSection`, `ILogger<CommentWorkflow>` | 聚合任务入口；不直接访问 DOM 字典。 |
| PageGuardian |
| NoteEngagementWorkflow | `IBrowserManager`, `IAccountManager`, `IPageStateGuard`, `IPageGuardian`, `INoteDiscoveryService`, `IUniversalApiMonitor`, `IFeedbackCoordinator`, `IHumanizedInteractionService`, `ILogger<NoteEngagementWorkflow>`, `IOptions<XhsSettings>` | 统一处理点赞/收藏/取消操作及 API 反馈。 |
| NoteDiscoveryService | `IBrowserManager`, `IHumanizedInteractionService`, `IDomElementManager`, `ILogger<NoteDiscoveryService>`, `IOptions<XhsSettings>` | 提供笔记定位与详情匹配能力，复用 Locator 策略。 |
| PageGuardian | `IDomElementManager`, `IHumanizedInteractionService`, `IPageLoadWaitService`, `IUniversalApiMonitor`, `LocatorSelectorsCatalog`, `ILogger<PageGuardian>` | 负责页面状态检测、Wait/Ensure、元素计数等。 |
| InteractionExecutor | `IDomElementManager`, `IHumanizedInteractionService`, `ILogger<InteractionExecutor>` | 统一封装拟人点击、输入、标签/表情插入；禁止直接 JS 注入。 |
| FeedbackCoordinator | `IUniversalApiMonitor`, `XhsSettings.EndpointRetrySection`, `ILogger<FeedbackCoordinator>` | 与 EndpointRetry 策略联动，产出审计日志与 API 结果。 |

## 5. 迁移路径
1. **阶段C2-Comment**（本轮）：
   - 创建接口 + 类骨架，更新 `ServiceCollectionExtensions` 注册。
   - 将 `PostCommentAsync` 及相关辅助方法（等待、输入、API 监控、拼音匹配等）迁移至工作流组件。
   - `XiaoHongShuService` 改为委托调用，字段缩减为组件引用。
   - 更新/新增单测：覆盖成功、输入失败、API 未确认、未登录等情形。
2. **阶段C2-Interaction**（下一轮）：
   - 迁移点赞/收藏/取消逻辑至 `InteractionExecutor`，统一 `OperationResult<InteractionResult>` 行为。
3. **阶段C2-Intent/Search**（后续）：
   - 拆分 `GetNoteDetailAsync`、搜索/滚动策略至 `IntentOrchestrator`，与 PageGuardian 联动。

## 6. 测试策略
- 单元测试
  - `CommentWorkflowTests`：使用 Moq stub `IUniversalApiMonitor`、`IPageGuardian`，验证评论成功/失败路径。
  - `PageGuardianTests`：对 Locator 打开、页面元素计数等逻辑做隔离测试。
  - `InteractionExecutorTests`：验证拟人输入、Emoji/标签注入序列。
- 集成测试
  - `XiaoHongShuServiceCommentTests`：验证服务层委托方法与 DI wiring。
- 回归
  - `dotnet test Tests`
  - `dotnet test HushOps.Core/tests`

## 7. 风险与缓解
- **依赖爆炸**：接口化并限制构造函数参数数量，必要时引入配置记录体。
- **循环依赖**：禁止组件反向调用 `IXiaoHongShuService`，通过 DTO 传递上下文。
- **逻辑迁移错误**：逐方法迁移并保持中文注释，对关键路径编写单测与审计日志。
- **性能回退**：维护原有 `HumanWaitType` 与 `EndpointRetry` 参数，迁移后执行业务压测并校验 P95/P99。

## 8. 实施计划
| 步骤 | 工作项 | 产出 |
| --- | --- | --- |
| Step1 | 搭建接口与类骨架、注册 DI | `CommentFlow/*.cs` | 
| Step2 | 迁移评论相关方法与私有依赖 | 精简后的 `XiaoHongShuService` 与新组件实现 |
| Step3 | 更新/新增单测 | `Tests/Services/CommentWorkflowTests.cs` 等 |
| Step4 | 执行测试矩阵并归档 | `dotnet test` 输出 + 证据归档 |
| Step5 | 更新文档、指标说明、审计配置 | 本设计文档、`metrics-dictionary`、`evidence/` |

## 9. 验收标准
- `XiaoHongShuService.cs` 代码行数减少 ≥ 40%。
- 评论场景单测覆盖率 ≥ 85%，整体测试通过。
- 评论成功率、API 捕获率与现有基线一致（>=95%）。
- 所有中文注释与文档同步更新，无视觉兜底接口残留。

## 10. 下一步
- 根据本设计执行 Step1/Step2 代码迁移。
- 在 `evidence/changes/` 记录迁移前后主要差异。
- 准备交互/搜索工作流的后续设计稿。
