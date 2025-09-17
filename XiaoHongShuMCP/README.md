# XHS 智能自动化运营系统（本地-only 基线：Core + MCP/stdio）

本目录提供完整、可运行的 .NET 8 实现：

- `HushOps.Core`（私有内核）：反检测管线、浏览器适配、网络聚合、拟人轨迹、配置、可观测；
- `XiaoHongShuMCP`（开源 MCP，提供命令行入口）：读取白名单配置、初始化 Serilog、运行反检测与示例交互；
- `Tests`：涵盖服务层与工具层的单元测试。

## 构建与运行

要求：.NET SDK 8.x。

```bash
 dotnet build HushOps.Core/HushOps.Core.csproj -c Release
 dotnet build XiaoHongShuMCP/XiaoHongShuMCP.csproj -c Release
 
 # 启动 MCP（仅保留 serve 核心命令）
 XHS__Metrics__Exporter=Console \
 XHS__AntiDetection__AuditDirectory=.audit \
 dotnet run --project XiaoHongShuMCP/XiaoHongShuMCP.csproj -- serve
 
 # 运行单元测试
 dotnet test Tests -c Release
```

建议设置：

```bash
export OTEL_SEMCONV_STABILITY_OPT_IN=http
```

## 配置（仅白名单 XHS__*）

参见 `docs/config-minimal.md`。

## 指标

参见 `docs/metrics-otel.md`（本地 Console 导出）。

## 架构

参见 `docs/architecture-v2025-09-decoupled.md`（历史归档说明）与最新 SDS。

## MCP 工具（业务面暴露）

- ConnectToBrowser(waitUntilLoggedIn?, maxWaitSeconds?, pollMs?)
- GetNoteDetail(keyword, includeComments?) / BatchGetNoteDetails(...)
- LikeNote(keyword) / FavoriteNote(keyword)
- InteractNote(keyword, likeAction, favoriteAction) // 破坏式新签名：likeAction/favoriteAction ∈ {do|cancel|none}
- UnlikeNote(keyword) / UncollectNote(keyword)
- PostComment(keyword, content)
- GetRecommendedNotes(limit?, timeoutSeconds?)
- SearchNotes(keyword, maxResults?, sortBy[comprehensive|latest|most_liked], noteType[all|video|image], publishTime[all|day|week|half_year], ...)
- ScrollCurrentPage(targetDistance?, waitForLoad?)
- TemporarySaveAndLeave(title, content, noteType, images[]/imagePaths, videoPath?, tags?)

更多运行细节与维护步骤参见 `docs/mcp/sds.md` 与 `docs/operations/runbook.md`。
