# selectors/resilient.ts — 深度分析

## 角色
- 统一的“弹性选择器解析”入口：重试 + 熔断（QPS/连续失败）+ 健康度记录 + 可选验证超时。

## 关键点
- 断路器：`PolicyEnforcer`（qps、failureThreshold、openSeconds），全局实例。
- 重试：`withRetry`（指数退避+抖动），默认 3 次，base=200ms，max=2000ms。
- 验证：`locator.first().waitFor({ state:'attached' })` 确认附加到 DOM（默认 1000ms）。
- 健康度：`healthMonitor.record` + `appendHealth`（NDJSON）记录 selectorId/耗时/ok/url/slug。
- 解析：`resolveLocatorAsync`，本仓不再依赖外部 JSON 合成候选（最小依赖）。

## 易错点与防护
- 打开状态熔断时阻塞至半开窗口，避免雪崩；失败增强错误信息标注 selectorId。
- `skipHealthMonitor` 可在批量定位场景降噪。

## 建议
- 将 selectorId 命名规范写入文档：`<page>.<area>.<action>`，便于监控聚合。
- NDJSON 写入建议批量/退避，减少 IO 抖动（在 health-sink 实现）。

## 文件参考
- src/selectors/resilient.ts:1
