using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Tests.Architecture;

/// <summary>
/// ReadOnlyJsExecutor 白名单守卫（静态）：
/// - 扫描仓库内对 PlaywrightAdapterTelemetry.EvalAsync 的调用，提取路径标签参数；
/// - 强制所有标签必须属于白名单集合，否则失败；
/// - 目的：防止新增不受控的只读 Evaluate 路径，违背“只读+白名单”的核心原则。
/// </summary>
public class ReadOnlyJsExecutorWhitelistTests
{
    [Test]
    public void All_EvalAsync_PathLabels_Must_Be_Whitelisted()
    {
        var repoRoot = LocateRepoRoot();
        var allowedCustomLabelFiles = new[]
        {
            // 审计专用：仅在显式开关与运行时白名单下可能启用的 html.sample，避免干扰默认白名单测试
            "HushOps.Core/Runtime/Playwright/PlaywrightAutoFactory.cs",
            "HushOps.Core/Runtime/Playwright/PlaywrightBrowserDriver.cs"
        };

        var csFiles = Directory.GetFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Replace('\\','/').Contains("/bin/") && !p.Replace('\\','/').Contains("/obj/")
                        && !p.Replace('\\','/').Contains("/Tests/")
                        && !allowedCustomLabelFiles.Any(af => p.Replace('\\','/').EndsWith(af.Replace('\\','/'), StringComparison.Ordinal)))
            .ToArray();

        // 匹配形如：PlaywrightAdapterTelemetry.EvalAsync<...>(..., "label", ...)
        // 提取第三个参数中的路径标签（第二个字符串是 JS 脚本，可能包含 =>，因此需跳过）
        var re = new Regex(@"PlaywrightAdapterTelemetry\.EvalAsync\s*<[^>]+>\s*\([^)]*,\s*""(?<label>[A-Za-z0-9\.-:]+)""\s*,\s*[^)]*\)", RegexOptions.Compiled);
        var labels = csFiles
            .SelectMany(p => re.Matches(File.ReadAllText(p)).Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups["label"].Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var allowed = new[]
        {
            "element.tagName","element.computedStyle","element.textProbe",
            "element.clickability","element.probeVisibility","element.evaluate",
            // 收紧页面级标签：通用 page.evaluate 改为 page.eval.read；增加反检测快照专用标签
            "page.eval.read","antidetect.snapshot"
        };

        var notAllowed = labels.Where(l => !allowed.Contains(l, StringComparer.Ordinal)).ToArray();
        if (notAllowed.Length > 0)
            Assert.Fail("发现未在白名单的 Evaluate 路径标签: " + string.Join(",", notAllowed));
    }

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HushOps.sln")))
            dir = dir.Parent!;
        Assert.That(dir, Is.Not.Null, "未能定位仓库根目录");
        return dir!.FullName;
    }
}
