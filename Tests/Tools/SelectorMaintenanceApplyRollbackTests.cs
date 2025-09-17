using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Internal;

namespace Tests.Tools;

/// <summary>
/// 验证选择器计划应用与回滚：
/// - 在临时 DomElementManager.cs 副本上应用 plan.After 后，首项发生改变；
/// - 调用回滚使用 plan.Before 恢复原顺序。
/// </summary>
public class SelectorMaintenanceApplyRollbackTests
{
    private string _tmpDir = null!;
    private string _srcCopy = null!;
    private string _planPath = null!;
    private List<string> _afterOrder = null!;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [SetUp]
    public void Setup()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "xhs-selector-rollback-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
        // 拷贝真实源文件到临时路径
        // 定位仓库根
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        DirectoryInfo? root = null;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HushOps.sln"))) { root = dir; break; }
            dir = dir.Parent;
        }
        Assert.That(root, Is.Not.Null, "未能定位仓库根目录");
        var real = Path.Combine(root!.FullName, "HushOps.Core", "Persistence", "Data", "locator-selectors.json");
        Assert.That(File.Exists(real), Is.True);
        _srcCopy = Path.Combine(_tmpDir, "locator-selectors.json");
        File.Copy(real, _srcCopy);

        // 构造一个最小 plan：选择现有别名 LoginButton，将列表首项与第二项对换
        var selectors = JsonSerializer.Deserialize<Dictionary<string, SelectorDocument>>(File.ReadAllText(_srcCopy), _jsonOptions)
                        ?? throw new InvalidOperationException("定位器配置解析失败");
        Assert.That(selectors.ContainsKey("LoginButton"), Is.True, "未找到 LoginButton 别名");
        var items = selectors["LoginButton"].Selectors;
        Assert.That(items.Count, Is.GreaterThanOrEqualTo(2));
        var before = items.ToList();
        _afterOrder = items.ToList(); (_afterOrder[0], _afterOrder[1]) = (_afterOrder[1], _afterOrder[0]);

        var plan = new HushOps.Core.Selectors.WeakSelectorPlan(new []
        {
            new HushOps.Core.Selectors.WeakSelectorPlanItem("LoginButton", before, _afterOrder, new string[0])
        });
        _planPath = Path.Combine(_tmpDir, "plan.json");
        File.WriteAllText(_planPath, JsonSerializer.Serialize(plan));
    }

    [TearDown]
    public void Cleanup()
    {
        try { Directory.Delete(_tmpDir, true); } catch { }
    }

    [Test]
    public async Task Apply_Then_Rollback_Should_Restore_Order()
    {
        // 应用计划（对临时文件），验证首项改变
        var applyRes = await SelectorMaintenanceService.ApplyDomSelectorsPlanToSource(_planPath, _srcCopy);
        var prop = applyRes.GetType().GetProperty("status");
        Assert.That(prop?.GetValue(applyRes)?.ToString(), Is.EqualTo("ok"));

        var snapshot = JsonSerializer.Deserialize<Dictionary<string, SelectorDocument>>(await File.ReadAllTextAsync(_srcCopy), _jsonOptions)
                       ?? throw new InvalidOperationException("更新后的定位器配置解析失败");
        var applied = snapshot["LoginButton"].Selectors;
        Assert.That(applied.Count, Is.GreaterThanOrEqualTo(2));

        // 回滚计划，验证恢复原首项
        var rollbackRes = await SelectorMaintenanceService.RollbackDomSelectorsPlanOnSource(_planPath, _srcCopy);
        var stat2 = rollbackRes.GetType().GetProperty("status");
        Assert.That(stat2?.GetValue(rollbackRes)?.ToString(), Is.EqualTo("ok"));

        var rolledSnapshot = JsonSerializer.Deserialize<Dictionary<string, SelectorDocument>>(await File.ReadAllTextAsync(_srcCopy), _jsonOptions)
                             ?? throw new InvalidOperationException("回滚后的定位器配置解析失败");
        var rolled = rolledSnapshot["LoginButton"].Selectors;
        Assert.That(rolled[0], Is.EqualTo(_afterOrder[1]));
    }

    private sealed class SelectorDocument
    {
        public List<string> Selectors { get; set; } = new();
    }
}
