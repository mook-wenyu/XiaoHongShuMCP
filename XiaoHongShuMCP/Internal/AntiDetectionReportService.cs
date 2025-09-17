using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HushOps.Core.AntiDetection;

namespace XiaoHongShuMCP.Internal;

internal static class AntiDetectionReportService
{
    public sealed record DailyReport(
        string Date,
        int Samples,
        int Ok,
        int Violated,
        int DegradeRecommended,
        IReadOnlyList<string> TopViolations,
        [property: JsonPropertyName("output")] string OutputPath,
        [property: JsonPropertyName("errorCode")] string? ErrorCode = null,
        [property: JsonPropertyName("message")] string? Message = null,
        [property: JsonPropertyName("retriable")] bool? Retriable = null,
        [property: JsonPropertyName("requestId")] string? RequestId = null
    );

    public static async Task<DailyReport> GenerateDailyReport(
        string? date = null,
        string whitelistPath = "whitelist.json",
        IServiceProvider? sp = null,
        string? requestId = null)
    {
        if (sp is null)
        {
            return new DailyReport(DateTime.UtcNow.Date.ToString("yyyy-MM-dd"), 0, 0, 0, 0, Array.Empty<string>(), "",
                ErrorCode: "NO_SERVICE_PROVIDER", Message: "ServiceProvider is null", Retriable: false, RequestId: requestId);
        }

        var settings = sp.GetRequiredService<IOptions<HushOps.Core.Config.XhsSettings>>().Value;

        var dt = string.IsNullOrWhiteSpace(date)
            ? DateTime.UtcNow.Date
            : DateTime.Parse(date!).Date;
        var day = dt.ToString("yyyyMMdd");

        var auditDir = settings.AntiDetection.AuditDirectory ?? ".audit";
        Directory.CreateDirectory(auditDir);
        var files = Directory.GetFiles(auditDir, $"antidetect-snapshot-{day}*.json");

        var wlJson = await File.ReadAllTextAsync(whitelistPath);
        var whitelist = JsonSerializer.Deserialize<AntiDetectionWhitelist>(wlJson) ?? new AntiDetectionWhitelist();

        var top = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ok = 0; var violated = 0; var degrade = 0; var total = 0;
        foreach (var f in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(f);
                var snap = JsonSerializer.Deserialize<AntiDetectionSnapshot>(json) ?? new AntiDetectionSnapshot();
                var res = AntiDetectionBaselineValidator.Validate(snap, whitelist);
                total++;
                if (res.TotalViolations == 0) ok++;
                else
                {
                    violated++;
                    foreach (var v in res.Violations)
                        top[v] = top.TryGetValue(v, out var c) ? c + 1 : 1;
                }
                if (res.DegradeRecommended) degrade++;
            }
            catch
            {
                // 忽略坏样本文件
            }
        }

        var topList = top.OrderByDescending(kv => kv.Value).Take(10).Select(kv => $"{kv.Key}:{kv.Value}").ToList();
        var outputDir = Path.Combine("docs", "anti-detect");
        Directory.CreateDirectory(outputDir);
        var output = Path.Combine(outputDir, $"anti-detect-drift-report-{day}.json");
        var payload = new { date = dt.ToString("yyyy-MM-dd"), samples = total, ok, violated, degradeRecommended = degrade, topViolations = topList };
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        return new DailyReport(dt.ToString("yyyy-MM-dd"), total, ok, violated, degrade, topList, output, null, null, null, requestId);
    }

    public static async Task<object> GenerateWeeklyAdr(
        int windowDays = 7,
        string whitelistPath = "whitelist.json",
        IServiceProvider? sp = null,
        string? requestId = null)
    {
        if (sp is null)
        {
            return new { errorCode = "NO_SERVICE_PROVIDER", message = "ServiceProvider is null", retriable = false, requestId };
        }

        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-Math.Max(1, windowDays) + 1);

        var reports = new List<DailyReport>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var r = await GenerateDailyReport(d.ToString("yyyy-MM-dd"), whitelistPath, sp, requestId);
            reports.Add(r);
        }

        var sumSamples = reports.Sum(r => r.Samples);
        var sumViol = reports.Sum(r => r.Violated);
        var sumDegrade = reports.Sum(r => r.DegradeRecommended);

        string? adrPath = null;
        if (sumViol > 0 || sumDegrade > 0)
        {
            adrPath = WriteAdr(reports, sumSamples, sumViol, sumDegrade);
        }

        return new { status = "ok", windowDays, samples = sumSamples, violated = sumViol, degradeRecommended = sumDegrade, adr = adrPath };
    }

    private static string WriteAdr(List<DailyReport> reports, int samples, int violated, int degrade)
    {
        var adrDir = Path.Combine("docs", "adr");
        Directory.CreateDirectory(adrDir);
        var next = NextAdrNumber(adrDir);
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var file = Path.Combine(adrDir, $"ADR-{next:0000}-antidetect-drift-{date}.md");
        var lines = new List<string>
        {
            "# ADR: Anti-Detection Drift Weekly Report",
            "",
            $"- 日期范围: {reports.First().Date} ~ {reports.Last().Date}",
            $"- 样本总数: {samples}",
            $"- 违反总数: {violated}",
            $"- 建议降级: {degrade}",
            "",
            "## 每日汇总",
        };
        foreach (var r in reports)
        {
            lines.Add($"- {r.Date}: samples={r.Samples}, ok={r.Ok}, violated={r.Violated}, degrade={r.DegradeRecommended}");
        }
        File.WriteAllLines(file, lines);
        return file;
    }

    private static int NextAdrNumber(string dir)
    {
        var max = 0;
        foreach (var f in Directory.GetFiles(dir, "ADR-*.md"))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var parts = name.Split('-');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var n))
                max = Math.Max(max, n);
        }
        return max + 1;
    }
}

