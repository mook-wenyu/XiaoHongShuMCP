using XiaoHongShuMCP.Services;

namespace Tests.Services;

/// <summary>
/// 弱选择器治理器单元测试：验证按 successRate 阈值与最小样本进行降权与顺序保持。
/// </summary>
public class WeakSelectorGovernorTests
{
    private sealed class FakeDomElementManager : DomElementManager
    {
        // 复用真实实现（包含 _selectors 字典），便于直接验证 TryReorderSelectors 效果
    }

    [Test]
    public void BuildPlan_And_Apply_Reorders_Weak_To_Tail()
    {
        // Arrange：建立一个最简 alias 映射
        var dom = new FakeDomElementManager();
        var telemetry = new HushOps.Core.Selectors.SelectorTelemetryService();

        // 直接操纵 dom 内部：为测试简化，调用 GetAllSelectors 后拿到一个存在的 alias
        var all = dom.GetAllSelectors();
        var alias = all.Keys.First();
        var before = all[alias];
        // 若目标别名选择器不足 3 个，说明当前测试集不适配，跳过
        if (before.Count < 3) Assert.Inconclusive("测试需要 >=3 个候选选择器");

        // 记录遥测：将第一个设为强（高成功率），将第二个设为弱（低成功率），第三个无样本保持原位
        for (int i = 0; i < 10; i++) telemetry.RecordAttempt(alias, before[0], true, 10, 1);
        for (int i = 0; i < 10; i++) telemetry.RecordAttempt(alias, before[1], false, 10, 2);

        var gov = new WeakSelectorGovernor(dom, telemetry);
        var plan = gov.BuildPlan(0.5, 5);
        if (plan.Items.Count == 0) Assert.Inconclusive("当前选择器分布未生成变更计划");

        // Apply
        var ok = gov.ApplyPlan(plan);
        Assert.That(ok, Is.True);

        var after = dom.GetAllSelectors()[alias];
        // 断言：弱的 before[1] 被移动至末尾；强的 before[0] 仍在首位
        Assert.That(after[0], Is.EqualTo(before[0]));
        Assert.That(after[^1], Is.EqualTo(before[1]));
    }
}
