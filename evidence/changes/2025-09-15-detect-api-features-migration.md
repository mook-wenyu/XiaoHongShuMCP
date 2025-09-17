# 2025-09-15 A1-批次1：DetectApiFeatures 去除 Evaluate（事件驱动）

- 目标：将 XiaoHongShuService.DetectApiFeatures 从 IPage.EvaluateAsync("performance.getEntriesByType") 迁移为基于 IUniversalApiMonitor 的端点命中推断，完全移除脚本依赖。
- 变更文件：
  - XiaoHongShuMCP/Services/XiaoHongShuService.cs（替换方法体，中文注释）
- 验收：ApiFeatures 列表正确填充；公开面零脚本守卫保持；无新增注入计数。
- 回滚：方法级还原原实现或降级为空列表并在 DetectionLog 记录。

## 校验
- 搜索 XiaoHongShuMCP 内 EvaluateAsync 使用点：仅余 Internal/HtmlAuditSampler 与 Utilities/JsEvalGuard（允许的内部/封装路径）。
- 单测：Tests/Services/DetectApiFeaturesFromMonitorTests.cs（依赖主项目通过后执行）。

## 风险评估
- 识别粒度更保守（仅统计已命中的端点响应）。
- 若监控未命中，特性列表可能为空；上层按“有则用、无则跳过”。
