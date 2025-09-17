using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Internal;

namespace Tests.Tools;

/// <summary>
/// 基线对比与重排计划生成（基于 .probe 快照）测试。
/// </summary>
public class SelectorMaintenanceToolsProbeDiffTests
{
    private string _tmp = null!;

    [SetUp]
    public void SetUp()
    {
        _tmp = Path.Combine(Path.GetTempPath(), "xhs-probe-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmp);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tmp, true); } catch { }
    }

    [Test]
    public async Task CompareProbeAndBuildPlan_Should_Produce_Reorder_When_FirstSelector_Changed()
    {
        // Arrange: 构造基线与当前快照（别名 NoteItem），将 firstMatched 从第1项变为第3项
        var baseline = new XiaoHongShuMCP.Services.PageProbeResult(true, "https://x/", "", new []
        {
            new XiaoHongShuMCP.Services.PageProbeAliasDetail("NoteItem", "section.note-item", 10, "<div class='note-item'>...</div>")
        }, "ok");
        var current = new XiaoHongShuMCP.Services.PageProbeResult(true, "https://x/", "", new []
        {
            new XiaoHongShuMCP.Services.PageProbeAliasDetail("NoteItem", ".note-item[data-width][data-height]", 8, "<div class='note-item' data-width='1' data-height='1'>...</div>")
        }, "ok");

        var baselinePath = Path.Combine(_tmp, "baseline.json");
        var currentPath = Path.Combine(_tmp, "current.json");
        await File.WriteAllTextAsync(baselinePath, JsonSerializer.Serialize(baseline));
        await File.WriteAllTextAsync(currentPath, JsonSerializer.Serialize(current));

        var services = new ServiceCollection();
        services.AddSingleton<IDomElementManager, DomElementManager>();
        var sp = services.BuildServiceProvider();

        // Act
        var res = await SelectorMaintenanceService.CompareProbeAndBuildPlan(currentPath, baselinePath, true, Path.Combine(_tmp, "plans"), sp);

        // Assert
        var planPathProp = res.GetType().GetProperty("planPath");
        var planPath = planPathProp?.GetValue(res) as string;
        Assert.That(planPath, Is.Not.Null.And.Not.Empty);
        var json = await File.ReadAllTextAsync(planPath!);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.That(root.TryGetProperty("Items", out var itemsEl), Is.True);
        Assert.That(itemsEl.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(itemsEl.GetArrayLength(), Is.GreaterThan(0));
        var first = itemsEl[0];
        Assert.That(first.GetProperty("Alias").GetString(), Is.EqualTo("NoteItem"));
        var after = first.GetProperty("After");
        Assert.That(after.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(after[0].GetString(), Is.EqualTo(".note-item[data-width][data-height]"));
    }
}
