# ADR-0021: DetectApiFeatures 去除 Evaluate，改用事件驱动监控器
日期：2025-09-15  | 状态：通过

## 背景
原实现通过 `page.EvaluateAsync` 读取 `performance.getEntriesByType('resource')` 进行字符串过滤，存在脚本执行痕迹，不符合“禁注入/只读 Evaluate 最小化”的方向。

## 备选方案
- A：保留 Evaluate，增加白名单与采样开关（复杂度上升、与方向背离）。
- B：改用已绑定的 `IUniversalApiMonitor`（事件驱动），以端点命中推断特性（无脚本）。

## 决策
选 B。复用既有标准组件，不新增治理开关，降低可检出面，语义更聚焦。

## 后果
- 正面：零脚本、事件驱动、易回滚。
- 负面：对未命中的场景更保守，需要上层“有则用、无则跳过”。

## 引用
- evidence/changes/2025-09-15-detect-api-features-migration.md
