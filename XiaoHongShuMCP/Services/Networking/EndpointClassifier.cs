using HushOps.Core.Networking;
using HushOps.Core.Automation.Abstractions;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 应用端点分类器：将 URL/状态/正文映射为低基数端点键（字符串）。
/// - 规则：复用 UniversalApiMonitor.IdentifyApiEndpoint 的识别逻辑；
/// - 优先：URL→端点映射；
/// - 回退（仅 HTTP 且提供 Payload）：基于正文结构体征做低基数判别（Homefeed/Feed/SearchNotes/Comments）；
/// - 返回端点枚举名字符串，未知返回 null。
/// </summary>
public sealed class EndpointClassifier : IEndpointClassifier
{
    /// <summary>
    /// 低基数分类策略：
    /// - HTTP：根据 URL 模式映射为端点键（忽略正文），与 UAM 一致；
    /// - WS/Worker：默认返回 null（unknown），后续可按 URL/载荷体征补充规则。
    /// </summary>
    public string? Classify(NetworkEventKind kind, string url, int? status, string? payload, NetworkDirection? direction)
    {
        if (kind == NetworkEventKind.HttpResponse)
        {
            var ep = UniversalApiMonitor.IdentifyApiEndpoint(url);
            if (ep != null) return ep.ToString();

            // —— 回退：基于正文结构体征低基数判别（仅在成功响应或有正文时尝试）——
            if (string.IsNullOrWhiteSpace(payload)) return null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payload);
                var root = doc.RootElement;
                // 常见成功标志（不强制约束）
                var ok = root.TryGetProperty("success", out var succ) ? (succ.ValueKind == System.Text.Json.JsonValueKind.True) : true;
                if (!root.TryGetProperty("data", out var data)) return null;

                // 1) 评论：data.comments 为数组
                if (data.TryGetProperty("comments", out var cm) && cm.ValueKind == System.Text.Json.JsonValueKind.Array)
                    return ApiEndpointType.Comments.ToString();

                // 2) 搜索：data.items 为数组，且存在 page_token/search_id 等分页/检索体征
                bool looksSearch = (data.TryGetProperty("page_token", out _) || data.TryGetProperty("search_id", out _));
                if (looksSearch && data.TryGetProperty("items", out var itemsS) && itemsS.ValueKind == System.Text.Json.JsonValueKind.Array)
                    return ApiEndpointType.SearchNotes.ToString();

                // 3) Feed/Homefeed：data.items 数组 + note_card 体征；使用 current_time 区分 Feed
                if (data.TryGetProperty("items", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    bool hasNoteCard = false;
                    foreach (var it in items.EnumerateArray())
                    {
                        if (it.ValueKind == System.Text.Json.JsonValueKind.Object && it.TryGetProperty("note_card", out var _))
                        { hasNoteCard = true; break; }
                    }
                    if (hasNoteCard)
                    {
                        if (data.TryGetProperty("current_time", out _)) return ApiEndpointType.Feed.ToString();
                        return ApiEndpointType.Homefeed.ToString();
                    }
                }
            }
            catch { /* 忽略解析异常，返回 null */ }
            return null;
        }
        // 非 HTTP 事件暂未纳入端点枚举，返回 null 以计入 unknown
        return null;
    }
}
