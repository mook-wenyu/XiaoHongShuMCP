# domain/xhs/navigation.ts — 深度分析

## 角色与边界
- 负责：页面类型识别、进入发现页、关闭模态、按关键词选择并打开笔记（仅封面/标题点击）。
- 边界：不实现反检测/指纹；点击严格限制在卡片容器的封面/标题（避免其它可点击元素）。

## 关键 API/函数
- `detectPageType`：URL 优先，必要时轻量 DOM 探针（role=dialog / note-detail-mask / note-container）。
- `ensureDiscoverPage`：优先点击“发现”，失败直达 URL，辅以 `waitHomefeed` 侧证。
- `closeModalIfOpen`：Esc → 关闭按钮（多变体选择器）→ 遮罩/背景 → 左上角兜底。
- `findAndOpenNoteByKeywords`：
  - 关键词规范化：lower + `cleanTextFor` + 无空格短语；`matchAny` 任一即命中。
  - 容器：`selectors/card.resolveContainerSelector()`（内置默认集合）。
  - 可视批次扫描：`collectVisibleCards()` 按 Y 排序；避免重复 via `visited`。
  - 点击规则：仅“封面/标题”元素；若有 noteId 则在容器内优先匹配含该 id 的 cover/title 锚点。
  - 滚动策略：
    - 步长：视口高度 * 比例（默认 0.55）+ 抖动；
    - 防跳过：overlapAnchors/overlapRatio/backtrack，自适应 `adaptFactor`；
    - 进度自检：无新增则微滚动；可选短批次 API 确认；
    - 二次匹配：滚后立即复扫当前批次，减少“滚动但未匹配”的空窗。
  - 成功侧证：可选等待 `waitFeed`（或 `waitSearchNotes`）以确认 feed 到达；最终以“模态已开 + feed 回执（可选）”给出结果。

## 输入/输出
- 输入：`Page`、keywords:string[]、FindOpenOptions（maxScrolls/scrollStep/settleMs/preferApiAnchors/useApiAfterScroll）。
- 输出：`{ ok, matched?, modalOpen?, feedVerified?, feedItems?, feedType?, feedTtfbMs? }`。

## 健壮性设计
- 关键词复扫：滚动后立即对新可视批次进行二次匹配。
- 自适应：保留率低则步长缩放与回滚。
- 降级：硬超时后可选“首卡封面/标题降级点击”。

## 改进建议
- 容器分型：为不同页面（发现/搜索）提供更专一的容器映射（当前用统一缺省）。
- 关键词调试：在 artifacts 输出命中日志（已支持 kw-debug，建议独立开关）。
- 可视卡片采样：支持采样 N 张作为统计，帮助调优 overlap 与 backtrack 参数。

## 测试要点
- URL 与 DOM 混合判定的 PageType 覆盖；
- 滚动/复扫/降级全路径；
- `preferApiAnchors` 为 true 时的 id 精确点击优先级；
- `useApiAfterScroll` 的短等待对吞吐与可靠性的影响（进阶：P95 统计）。

## 文件参考
- src/domain/xhs/navigation.ts:1
- src/selectors/card.ts:1
- src/domain/xhs/netwatch.ts:1
