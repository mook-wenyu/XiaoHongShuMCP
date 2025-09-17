# 反检测采样与偏差报表

本目录用于存放：

- 采样快照：`.audit/antidetect-snapshot-*.json`（由工具采集，默认目录 `.audit`）
- 日报：`docs/anti-detect/anti-detect-drift-report-YYYYMMDD.json`
- 白名单：`docs/anti-detect/whitelist.json`（需按环境维护）

命令与脚本：

- 采样（由业务流程触发，或独立调用工具 AntiDetectionTools.GetAntiDetectionSnapshot）。
- 生成日报：
  - `dotnet run -p XiaoHongShuMCP -- antidetect-daily [YYYY-MM-DD] [docs/anti-detect/whitelist.json]`
  - 或 `scripts/run-antidetect-daily.sh`（默认今天与默认白名单路径）
- 生成周报并产出 ADR（若越阈值）：
  - `dotnet run -p XiaoHongShuMCP -- antidetect-weekly [days] [docs/anti-detect/whitelist.json]`
  - 或 `scripts/run-antidetect-weekly.sh`

审计说明：

- 日/周报文件作为可审计证据；若存在违反>0 或 DegradeRecommended>0，将在 `docs/adr` 生成带编号的 ADR 文档。

