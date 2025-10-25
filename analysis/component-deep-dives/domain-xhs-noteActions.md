# domain/xhs/noteActions.ts — 深度分析

## 角色与边界
- 范畴：笔记详情模态（.note-detail-mask / dialog）内的原子动作：点赞/取消、收藏/取消、评论、关注/取消。
- 约束：
  - 仅在模态打开时执行（MODAL_REQUIRED）。
  - 所有定位严格限定在模态外壳下的 `engage-bar` 容器（`.interactions.engage-bar | .engage-bar | .engage-bar-container | .buttons.engage-bar-style`）。
  - 按钮定位带 `:visible` 且主点赞限制 `:has(svg[width="24"])`，避免评论区 16px 误点。
  - 成功语义以 `netwatch.ts` 的 API 回执为准（code=0 或 success=true）。

## 输入/输出
- 输入：`Page`、（评论时 `text: string`）。
- 输出：`{ ok: boolean, ... }`，错误时 `code` 为 LIKE_FAILED/COMMENT_FAILED 等，成功时返回少量补充字段（如 newLike、commentId/fstatus）。

## 关键流程
- `ensureNoteModalOpen()`：仅对 `:visible` 的 `noteContainer|note-detail-mask|dialog` 判定。
- `modalShellRoot()` → `engageBarRoot()`：以外壳收敛，再选取 engage-bar 变体集合中的首个可见容器。
- `likeButton/collectButton/...`：在 engage-bar 内查询目标，全部加 `:visible`，主 like 采用 24px svg 过滤。
- `clickHumanScoped()`：
  - 可点性检查：可见性 + boundingBox + 样式（display/visibility/opacity/pointer-events）+ elementFromPoint 祖先命中。
  - 激活：hoverHuman + 软等待；仍失败则 100ms 软等待；最终兜底一次“微偏移原生点击”。
- 成功判定：先挂 `waitLike/waitCollect/waitComment/waitFollow`，点击后等待回执，解析 code/success 字段。

## 易错点与已修复
- 误点评论点赞：已通过“容器作用域 + 24px 过滤 + 移除模糊回退”修正。
- 模态副本选择：以 `:visible` 过滤，避免选到隐藏副本。
- 动画/遮挡：`primeEngageArea` + 软等待 + 兜底微偏移，降低 flakiness。

## 改进建议
- 关注降级：若回执失败但按钮文案出现“已关注”，记成功并记录 warn（平台策略变化时提升成功率）。
- 诊断增强：可在失败时临时打印 engageBarRoot / likeButton 的 `count()` 与 `boundingBox()`，便于远程诊断。

## 关联组件
- `src/domain/xhs/netwatch.ts`：API 成功语义封装。
- `src/humanization/actions.{mouse,scroll}.ts`：拟人化交互。

## 测试要点
- 不同容器变体：`.interactions.engage-bar` / `.engage-bar` / `.engage-bar-container` / `.buttons.engage-bar-style`。
- 可见与隐藏副本并存；动画遮挡；评论区 like 存在的页面。
- 关注失败分支与文案降级判定的回归。

## 文件参考
- src/domain/xhs/noteActions.ts:1
