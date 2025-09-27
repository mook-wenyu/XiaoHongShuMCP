# 任务基础信息
- 任务 ID：TASK-20250927-006
- 来源：用户新增浏览器配置与拟人化需求
- 更新时间：2025-09-28
- 责任人：Codex
- 关联提交：待定
- 状态：未交付

## 交付内容
- 浏览器会话管理：统一 `profileKey` 策略、自动打开、冲突校验与缓存字典。
- 指纹与网络策略：为会话提供 UA/视口/触控/代理/延迟/缓解计数等元数据。
- MCP 工具：添加中英文 Description 注解，保留审计字段。
- CLI 修复：Release 构建通过；`--tools-list` 已验证 MCP 工具注册。
- 验证流程：增加 `verification.statusUrl` 配置，端点不可达时输出警告并跳过缓解统计。

## 迁移与回滚
- 迁移：部署前需确认 `storage/browser-profiles/` 目录权限与 Playwright 依赖安装；若存在旧版配置，可保留原目录并补充 `profileKey` 映射。
- 回滚：若新指纹/网络策略导致异常，可暂时禁用相关配置节或将 `profileKey` 自动打开逻辑恢复到手动模式。

## 待完成事项
- `--verification-run` 默认 httpbin 端点可能被阻断，可通过 `verification.statusUrl` 改用内网端点，或依赖 `verification.mockStatusCode` 在本地模拟；仍需在真实端点上复验缓解指标。
- 获取实测数据以评估反检测效果。
