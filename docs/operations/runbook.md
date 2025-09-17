# MCP 运行手册（Runbook）
最后更新：2025-09-16

## 快速巡检
1. 执行 `dotnet test Tests`，确认核心单元测试通过；
2. 检查配置：`XHS` 根节是否包含浏览器、并发等基础参数；
3. 查看 `logs/` 与 `.audit/`（如启用）是否有异常错误码或超时堆积。

## 常见问题
### 工具返回 ERR_VALIDATION
- 检查输入参数是否缺失或格式异常；
- 确认浏览器已连接并完成登录（使用 `ConnectToBrowser(waitUntilLoggedIn=true)` 验证）。

### 工具超时或无响应
- 检查 Playwright 浏览器是否正常运行；
- 根据需要调整 `XHS:McpSettings:WaitTimeoutMs` 或 `XHS:Concurrency` 中的速率/熔断配置；
- 复查网络代理、VPN 等本地环境。

### 日志提示限流或熔断
- 调整 `XHS:Concurrency:Rate` 与 `XHS:Concurrency:Breaker` 的参数；
- 观察关键工具是否存在频繁失败，必要时降低调用频次。

## 发布步骤
1. 更新 `docs/mcp/sds.md` 与相关运行文档，说明本轮裁剪范围；
2. 执行 `dotnet test Tests --collect:"XPlat Code Coverage"`，保存覆盖率报告；
3. 运行 `dotnet run --project XiaoHongShuMCP -- serve`，由本地 Agent 验证主要工具；
4. 备份关键日志与 `.audit/`（若启用）到 `evidence/`，记录验证结论；
5. 发布前再次确认配置差异并记录回滚步骤。

## 回滚策略
- 恢复上一版本的代码与配置即可回滚全部变更；
- 保留历史日志与覆盖率报告，便于发布后对比；
- 若 Playwright 驱动出现异常，可先回滚 Playwright 版本或切换备用用户数据目录。
