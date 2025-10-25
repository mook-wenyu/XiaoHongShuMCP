# services/connectionManager.ts & pages.ts — 深度分析

## ConnectionManager（连接复用/TTL/健康检查）
- 复用：按 dirId 缓存 {browser, context}；`get()` 不存在则通过 connector.connect 打开。
- TTL 清理：定时 sweep，超过 ttlMs 的闲置连接关闭（间隔默认为 ttl/2，≥30s）。
- getHealthy：尝试新建并关闭一页验证健康，不健康则重建。
- warmup：批量预热，返回成功列表。
- healthCheck：新建/关闭一页验证。
- 关闭：按 dirId 或 closeAll（并清理定时器）。
- 默认工作区：透传 `ROXY_DEFAULT_WORKSPACE_ID`，减少上层必须传参。

## Pages（轻量页面选择）
- listPages：返回 index/url/isClosed 快照。
- ensurePage：优先未关闭的最后一个；指定 pageIndex 则取 bounds 内 idx。
- newPage/closePage：简单封装。

## 风险/建议
- sweeperTimer：进程退出时已 unref；可在 closeAll 后置为已停止状态防重复调用。
- 大量并发 getHealthy：可对“新开页→立刻关闭”加节流。

## 文件参考
- src/services/connectionManager.ts:1
- src/services/pages.ts:1
