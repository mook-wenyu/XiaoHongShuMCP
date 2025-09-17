namespace XiaoHongShuMCP.Internal;

/// <summary>
/// 内部提示文案集合（不暴露给 MCP，供 CLI/日志等内部使用）。
/// </summary>
internal static class McpPromptsInternal
{
    public static string ConnectBrowserGentle() => "Connect to the Xiaohongshu browser session, verify login state, do not inject JS. Respect read-only evaluation policy and timeout <= 120s. Output JSON: {IsConnected, IsLoggedIn, Message, ErrorCode}.";
    public static string AntiDetectSnapshotSafe() => "Collect anti-detection snapshot via internal service only. Validate with whitelist if provided. Return JSON {Success, Message, AuditPath, Violations[], DegradeRecommended}. Never run page-level scripts yourself.";
}

