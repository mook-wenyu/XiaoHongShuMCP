# MCP 清单（manifest）与使用说明

本目录不再存放静态 manifest.json。请通过运行时命令动态获取清单（stdout），用于 Host 校验、客户端绑定生成与审计对照。

导出命令（仅写文件，不输出到 stdout）：

```
dotnet run -p XiaoHongShuMCP -- export-manifest docs/mcp/runtime-manifest.json
```

CI 校验（幂等一致性检查）：

```
bash scripts/ci-guards/check-mcp-manifest.sh
```

说明：清单由 C# 反射生成，包含工具/提示名称、描述、参数 Schema、统一错误模型字段（errorCode/message/retriable/requestId）。
