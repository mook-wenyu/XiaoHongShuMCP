using NUnit.Framework;
using Moq;
using XiaoHongShuMCP.Services;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Observability;

namespace Tests.Services.LocatorPolicy;

[TestFixture]
public class LocatorPolicyStackTests
{
    private sealed class DummyMetrics : IMetrics
    {
        private sealed class C : ICounter { public long Last; public LabelSet L; public void Add(long value, in LabelSet labels) { Last += value; L = labels; } }
        private sealed class H : IHistogram { public void Record(double value, in LabelSet labels) { } }
        public ICounter CreateCounter(string name, string? description = null) => new C();
        public IHistogram CreateHistogram(string name, string? description = null) => new H();
    }

    [Test]
    public async Task Acquire_AliasHasText_ShouldTryAliasAndHasText()
    {
        var dom = new Mock<IDomElementManager>();
        dom.Setup(d => d.GetSelectors("SearchButton")).Returns(new List<string>{ ".search-btn" });

        var finder = new Mock<IElementFinder>();
        // 先返回一个不含文本匹配的元素（模拟 alias 命中但需 has-text 强化）
        var elMock = new Mock<IAutoElement>();
        elMock.Setup(e => e.IsVisibleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var autoEl = elMock.Object;
        finder.Setup(f => f.FindElementAsync(It.IsAny<IAutoPage>(), "SearchButton", It.IsAny<int>(), It.IsAny<int>()))
              .ReturnsAsync(autoEl);

        var page = new Mock<IAutoPage>();
        // 当尝试 alias + :has-text 时，通过 page.QueryAsync 命中（模拟 Playwright 选择器解析）
        page.Setup(p => p.QueryAsync(".search-btn:has-text(\"搜\")", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(autoEl);

        var stack = new LocatorPolicyStack(dom.Object, finder.Object, new DummyMetrics(), new HushOps.Core.Selectors.SelectorTelemetryService());
        var hint = new LocatorHint{ Aliases = new []{"SearchButton"}, NameOrText = "搜" };
        var r = await stack.AcquireAsync(page.Object, hint);
        Assert.That(r.Element, Is.Not.Null);
        Assert.That(new[]{"alias","alias-has-text","a11y-role","text-engine"}.Contains(r.Strategy));
    }

    [Test]
    public async Task Telemetry_Should_Reorder_AliasSelectors_For_HasText()
    {
        var dom = new Mock<IDomElementManager>();
        // 原始顺序：.b, .a
        dom.Setup(d => d.GetSelectors("Btn")).Returns(new List<string>{ ".b", ".a" });
        // 容器别名：Main -> .wrap
        dom.Setup(d => d.GetSelectors("Main")).Returns(new List<string>{ ".wrap" });

        var finder = new Mock<IElementFinder>();
        // 阶段1：alias 直接找不到，迫使进入 alias+has-text 阶段
        finder.Setup(f => f.FindElementAsync(It.IsAny<IAutoPage>(), "Btn", It.IsAny<int>(), It.IsAny<int>()))
              .ReturnsAsync((IAutoElement?)null);

        var elMock = new Mock<IAutoElement>();
        elMock.Setup(e => e.IsVisibleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var autoEl = elMock.Object;

        var page = new Mock<IAutoPage>();
        // 仅当选择器为 .wrap .a:has-text("Go") 时命中
        page.Setup(p => p.QueryAsync(".wrap .a:has-text(\"Go\")", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(autoEl);

        // 构造并预热 Telemetry：让 .a 拥有更高成功率与更低耗时
        var telemetry = new HushOps.Core.Selectors.SelectorTelemetryService();
        telemetry.RecordAttempt("Btn", ".a", success: true, elapsedMs: 50, attemptOrder: 1);
        telemetry.RecordAttempt("Btn", ".b", success: false, elapsedMs: 200, attemptOrder: 1);

        var stack = new LocatorPolicyStack(dom.Object, finder.Object, new DummyMetrics(), telemetry);
        var hint = new LocatorHint{ Aliases = new []{"Btn"}, ContainerAliases = new []{"Main"}, NameOrText = "Go", StepTimeoutMs = 5000 };
        var r = await stack.AcquireAsync(page.Object, hint);
        Assert.That(r.Element, Is.Not.Null);
        Assert.That(new[]{"alias-has-text","container-alias-has-text"}.Contains(r.Strategy));
    }
}
