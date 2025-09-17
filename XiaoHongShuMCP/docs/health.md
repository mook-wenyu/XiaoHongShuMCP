# 运行时健康与限额观测（已裁剪）

当前版本仅保留核心业务工具，原有的策略/限额治理工具（GetRuntimeHealth、GetLimits 等）已删除。
如需观测并发、限流或熔断状态，请直接查看：

- 服务级并发配置：`XHS:Concurrency` 节；
- Serilog 日志：默认写入 `logs/` 目录，异常会包含 `ERR_TIMEOUT`、`ERR_VALIDATION` 等错误码；
- Resumable/Checkpoints：位于 `.audit/` 或自定义目录，可辅助排查交互流程。

后续若需要恢复治理能力，可在新分支上重新实现入口限流与策略工具，并补充对应文档。
