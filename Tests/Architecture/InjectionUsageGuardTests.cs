using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Tests.Architecture;

/// <summary>
/// 代码扫描守卫：禁止非必要 JS 注入模式（window.scrollBy/dispatchEvent('click')）出现在业务层。
/// - 允许范围：Adapters 层（驱动适配）和 Tests（测试桩）可以包含；其余项目一律禁止。
/// - 目的：强制“禁注入，拟人化交互优先”。
/// </summary>
public class InjectionUsageGuardTests
{
    [Test]
    public void NoJsInjectionPatternsInServices()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        // repo 根通常是 TestDirectory 的上两级
        var repoRoot = Path.GetFullPath(Path.Combine(root, "..", "..", ".."));
        Assert.That(Directory.Exists(repoRoot), Is.True, $"repoRoot not found: {repoRoot}");

        var banned = new[]
        {
            "window.scrollBy(",
            "dispatchEvent(new MouseEvent('click'",
            ".EvaluateAsync("
        };

        var allowedDirs = new[] { "HushOps.Core/Adapters.Playwright", "Tests" };
        var allowedFiles = new[] { 
            // 唯一允许的注入字符串出现点：受门控的策略实现（记录指标）
            // 路径修正：从 HushOps/Services/ 还原为 XiaoHongShuMCP/Services/ 以匹配当前物理目录
            "XiaoHongShuMCP/Services/HumanizedInteraction/HumanizedClickPolicy.cs",
            // 只读 Evaluate 的门控实现（集中计量），允许包含 Evaluate 调用
            "XiaoHongShuMCP/Services/Utilities/ReadOnlyJsExecutor.cs"
        };

        var csFiles = Directory.GetFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p => !allowedDirs.Any(ad => p.Replace('\\','/').Contains("/" + ad + "/")))
            .Where(p => !allowedFiles.Any(af => p.Replace('\\','/').EndsWith(af.Replace('\\','/'), StringComparison.Ordinal)))
            .ToList();

        var hits = csFiles
            .SelectMany(p =>
            {
                var text = File.ReadAllText(p);
                return banned.Where(b => text.Contains(b, StringComparison.Ordinal)).Select(b => (file: p, pattern: b));
            })
            .ToList();

        if (hits.Count > 0)
        {
            var msg = string.Join(Environment.NewLine, hits.Select(h => $"{h.file}: {h.pattern}"));
            Assert.Fail("检测到禁止的 JS 注入用法:\n" + msg);
        }
    }
}

