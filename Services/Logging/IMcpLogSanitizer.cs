using System;
using System.Text.RegularExpressions;

namespace HushOps.Servers.XiaoHongShu.Services.Logging;

internal interface IMcpLogSanitizer
{
    string Sanitize(string message);
}

internal sealed class DefaultMcpLogSanitizer : IMcpLogSanitizer
{
    private static readonly Regex AssignmentPattern =
        new(@"(?xi)
            \b(api[_-]?key|apikey|secret|token|password)\b
            (?<delim>\s*[:=]\s*)
            (?<value>[^\s,;]+)
        ", RegexOptions.Compiled);

    private static readonly Regex BearerPattern =
        new(@"(?i)bearer\s+[a-z0-9\-_.]+", RegexOptions.Compiled);

    public string Sanitize(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        var sanitized = AssignmentPattern.Replace(message, static match =>
        {
            var key = match.Groups[1].Value;
            var delimiter = match.Groups["delim"].Value;
            return string.Concat(key, delimiter, "[REDACTED]");
        });

        sanitized = BearerPattern.Replace(sanitized, "Bearer [REDACTED]");

        return sanitized;
    }
}
