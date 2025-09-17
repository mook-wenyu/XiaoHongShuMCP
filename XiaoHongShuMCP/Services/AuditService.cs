using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HushOps.Core.Persistence;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 审计事件写盘服务：将交互关键证据落盘至 .audit 目录（可通过配置关闭）。
/// - 文件名：yyyyMMdd_HHmmss_ffff_action.json
/// - 内容：InteractionAuditEvent 序列化，已对文本字段做基础脱敏与截断。
/// </summary>
public class AuditService : IAuditService
{
    private readonly ILogger<AuditService>? _logger;
    private readonly XhsSettings _settings = null!; // 由构造函数注入
    private readonly IJsonLocalStore _jsonStore;
    private readonly string _auditSubDirectory;
    private readonly bool _enabled;

    public AuditService(IOptions<XhsSettings> options, IJsonLocalStore jsonStore, ILogger<AuditService>? logger = null)
    {
        _logger = logger;
        _settings = options.Value;
        _jsonStore = jsonStore ?? throw new ArgumentNullException(nameof(jsonStore));
        _enabled = _settings?.Audit?.Enabled ?? true;
        var dir = _settings?.Audit?.Directory ?? ".audit";
        _auditSubDirectory = dir.Replace('\\', '/');
    }

    public async Task WriteAsync(InteractionAuditEvent evt, CancellationToken ct = default)
    {
        if (!_enabled) return;
        try
        {
            // 轻脱敏与截断
            evt.Action = HtmlSanitizer.SanitizeForLogging(evt.Action);
            evt.Keyword = HtmlSanitizer.SanitizeForLogging(evt.Keyword);
            if (!string.IsNullOrEmpty(evt.Extra))
                evt.Extra = HtmlSanitizer.SafeTruncate(HtmlSanitizer.SanitizeForLogging(evt.Extra!), 8); // 最多 8KB

            var name = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_ffff}_{SanitizeFilePart(evt.Action)}.json";
            var relativePath = Path.Combine(_auditSubDirectory, name);
            var entry = await _jsonStore.SaveAsync(relativePath, evt, ct).ConfigureAwait(false);
            _logger?.LogInformation("[Audit] 写入 {File}", entry.FullPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "写入审计事件失败（忽略，不影响主流程）");
        }
    }

    private static string SanitizeFilePart(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "action";
        foreach (var ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
        return s.Length > 40 ? s[..40] : s;
    }
}
