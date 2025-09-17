// 说明：命名空间迁移至 HushOps.Services。
namespace XiaoHongShuMCP.Services;

/// <summary>
/// HTML 文本脱敏与安全截断工具（纯函数，可单元测试）。
/// </summary>
public static class HtmlSanitizer
{
    /// <summary>
    /// 基础脱敏：去除常见敏感关键词，避免日志中意外暴露。
    /// </summary>
    public static string SanitizeForLogging(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        try
        {
            // 先处理更长的关键词，避免子串替换引发的 “set-ck”等异常结果
            return text
                .Replace("set-cookie", "sc", StringComparison.OrdinalIgnoreCase)
                .Replace("authorization", "auth", StringComparison.OrdinalIgnoreCase)
                .Replace("cookie", "ck", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return text;
        }
    }

    /// <summary>
    /// 安全截断：按 KB 限制长度，避免日志过长；KB &gt;=1。
    /// </summary>
    public static string SafeTruncate(string text, int maxKb)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var max = Math.Max(1, maxKb) * 1024;
        return text.Length <= max ? text : text[..max];
    }
}
