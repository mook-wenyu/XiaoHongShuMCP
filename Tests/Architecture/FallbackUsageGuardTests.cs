using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Tests.Architecture;

/// <summary>
/// 破坏性守卫：禁止在可恢复操作（Resumable*Operation）中使用服务层回退调用（如 IXiaoHongShuService.SearchNotesAsync / InteractNoteAsync 等）。
/// 原则：唯一权威为“监听 + 拟人交互 + 聚合/确认”。
/// </summary>
public class FallbackUsageGuardTests
{
    [Test]
    public void NoServiceFallbackCallsInResumableOperations()
    {
        // 更稳健地定位到仓库根：向上查找包含解决方案文件的目录
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        DirectoryInfo? root = null;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HushOps.sln")))
            {
                root = dir; break;
            }
            dir = dir.Parent;
        }
        Assert.That(root, Is.Not.Null, "未能定位仓库根目录（缺少解决方案文件）");

        // 支持新旧目录结构：优先使用 XiaoHongShuMCP/Services/Resumable，其次兼容历史 HushOps 目录
        var candidateDirs = new[]
        {
            Path.Combine(root!.FullName, "XiaoHongShuMCP", "Services", "Resumable"),
            Path.Combine(root!.FullName, "HushOps", "Services", "Resumable")
        };
        var targetDir = candidateDirs.FirstOrDefault(path => Directory.Exists(path));
        Assert.That(targetDir, Is.Not.Null, $"Resumable 目录不存在，候选路径：{string.Join(", ", candidateDirs)}");

        var banned = new[]
        {
            "IXiaoHongShuService",
            ".SearchNotesAsync(",
            ".InteractNoteAsync("
        };

        var csFiles = Directory.GetFiles(targetDir!, "*.cs", SearchOption.AllDirectories).ToList();
        var hits = csFiles
            .SelectMany(p => banned.Where(b => File.ReadAllText(p).Contains(b, StringComparison.Ordinal)).Select(b => (file: p, pattern: b)))
            .ToList();

        if (hits.Count > 0)
        {
            var msg = string.Join(Environment.NewLine, hits.Select(h => $"{h.file}: {h.pattern}"));
            Assert.Fail("检测到服务回退调用痕迹（不允许）：\n" + msg);
        }
    }
}
