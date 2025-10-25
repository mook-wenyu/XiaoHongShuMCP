# services/playwrightConnector.ts — 深度分析

## 角色
- 连接至 Roxy 打开的远程浏览器（CDP），抽象多窗口=多 Context、同 Context 多 Page 的模型。

## 流程
1) `roxy.ensureOpen(dirId, workspaceId?)` 获取 ws；
2) `chromium.connectOverCDP(ws)` 连接远程浏览器；
3) 取 contexts[0] 作为默认 Context；
4) `withContext`：封装 acquire→运行→finally 关闭 browser 与 roxy 端窗口（短连接模式，连接管理交给 ConnectionManager）。

## 风险/建议
- connectOverCDP 成功但 contexts=[] 时抛错：可在日志提示检查远端浏览器状态（异常重启）。
- withContext 模式下每次都会 close browser：默认调用场景应通过 ConnectionManager 复用连接，避免反复开关的性能成本。

## 文件参考
- src/services/playwrightConnector.ts:1
