# Technical Recommendations

最近更新：2025-10-25

## 高优先级（P1）
### 1) 关注动作降级判定
- 目标：`followAuthor/unfollowAuthor` 在接口回执失败但 UI 已变更（按钮文案“已关注/关注”）时视为成功（记录 warn）。
- 变更点：`src/domain/xhs/noteActions.ts`
- 建议实现：
  - 点击后等待回执 `waitFollow/waitUnfollow`；失败时尝试读取 follow 按钮 `textContent()`；
  - 若文本包含“已关注”（follow 场景）或“关注”（且不含“已关注”，unfollow 场景）则 `ok=true` + `fstatus` 推断；记录告警。
- 验收：
  - 单测：模拟接口失败 + 文案成功，返回 ok=true；接口失败且文案未变更，返回 ok=false。
  - 实机：运行 `scripts/run-note-actions.js`，关注动作在偶发失败场景不再阻塞流程。

### 2) 健康度落盘批量/退避与 P95
- 目标：降低 `artifacts/selector-health.ndjson` 高频 IO，增强统计视角。
- 变更点：`src/selectors/health-sink.ts` 与报表脚本 `scripts/selector-health-report.ts`
- 建议实现：
  - writer 内部做内存 buffer（如每 100 条或 1s flush），失败时指数退避重试；
  - 报表输出 P50/P95 以及错误分布（selectorId 分组）；
- 验收：
  - 单测：mock fs 在高频写入下无抛错；
  - 报表：新增 P95 字段且随样本变化合理。

### 3) Windows 传参与文档
- 目标：减少 PowerShell 参数转义问题导致的脚本失败。
- 变更点：文档 `analysis/developer-onboarding-guide.md` 与 `analysis/troubleshooting-guide.md`（已补充）。
- 验收：文档包含单引号/双引号/`@file` 示例，按示例可直接执行。

## 中优先级（P2）
### 4) Humanization 选项透传一致性
- 目标：plans 与 actions 的参数命名与默认值一致。
- 路径：`src/humanization/plans/*`、`src/humanization/actions/*`
- 验收：对照表+单测（默认/边界）通过。

### 5) MCP 工具最小化（生产）
- 目标：演示/探索工具标记 dev-only，防止生产暴露多余接口。
- 路径：`src/mcp/tools/xhsShortcuts.ts`（去除/注释 keyword_browse 等）
- 验收：生产清单中仅保留 action.* 与必需 xhs.* 工具。

### 6) 域名切分增强
- 目标：`domainSlugFromUrl` 支持多后缀（co.uk 等）。
- 路径：`src/lib/url.ts`
- 验收：新增用例覆盖复合后缀。

## 低优先级（P3）
### 7) 模态动作排障清单
- 目标：将常见错误与步骤固化到文档（已在 `analysis/troubleshooting-guide.md`）。

### 8) 演示脚本增强
- 目标：noteActions 演示支持更多关键词与失败截图采集开关（环境变量控制）。
- 路径：`scripts/run-note-actions.js`
- 验收：可通过 `NOTE_DEMO_SNAP_ON_ERROR=true` 触发失败截图。

## 执行顺序建议
1. (P1) 关注降级 → 健康度批量/P95
2. (P2) Humanization 透传一致性 → MCP 工具清单收敛 → 域名切分增强
3. (P3) 文档与演示脚本增强
