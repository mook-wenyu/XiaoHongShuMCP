# humanization/plans & actions — 深度分析

## 计划层（纯函数）
- `plans/mousePlan.ts`：三次贝塞尔路径 + 收尾“微抖动”（默认 amp≈0.6, count=4, 衰减≈0.85）。
- `plans/scrollPlan.ts`：分段 delta + 抖动 + easing（默认 easeOut）+ 微/宏停顿（默认开启）。

## 执行层（基于 Playwright）
- `actions/mouse.ts`：
  - `moveMouseCubic`：ensureVisible → 计算 from/to → 逐点 move + sleep。
  - `clickHuman`：移动（可带微抖动）→ jitter(delay) → `loc.click`。
- `actions/scroll.ts`：
  - `scrollHumanized`：按 plan 逐段滚轮 + 等待；默认微/宏停顿开启。

## 建议
- 参数命名/默认对齐：plans 与 actions 的 options 字段语义一致。
- `ensureVisible` 失败分支时的诊断：可在 artifacts 落盘一次局部截图或 boundingBox 日志（仅在 dev/debug）。

## 文件参考
- src/humanization/plans/mousePlan.ts:1
- src/humanization/plans/scrollPlan.ts:1
- src/humanization/actions/mouse.ts:1
- src/humanization/actions/scroll.ts:1
