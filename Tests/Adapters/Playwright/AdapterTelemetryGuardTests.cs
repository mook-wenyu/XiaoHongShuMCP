using System;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Runtime.Playwright;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Observability;
using Moq;

namespace Tests.Adapters.Playwright;

/// <summary>
/// 适配器层只读 Evaluate 门控测试：验证禁用/白名单行为。
/// - 当启用=false 时，任何 Evaluate 调用应抛出 NotSupportedException；
/// - 当启用=true 且白名单不包含路径时，应抛出 NotSupportedException；
/// - 当启用=true 且白名单包含路径时，应正常调用底层 Evaluate 并返回结果。
/// 说明：通过 InternalsVisibleTo 访问内部类 PlaywrightAdapterTelemetry。
/// </summary>
public class AdapterTelemetryGuardTests
{
    private sealed class NoopMetrics : IMetrics
    {
        public ICounter CreateCounter(string name, string? description = null) => new C();
        public IHistogram CreateHistogram(string name, string? description = null) => new H();
        private sealed class C : ICounter { public void Add(long value, in LabelSet labels) { } }
        private sealed class H : IHistogram { public void Record(double value, in LabelSet labels) { } }
    }

    [NUnit.Framework.SetUp]
    public void ResetInit()
    {
        // 每个测试前重置：启用=false，无白名单
        PlaywrightAdapterTelemetry.Init(new NoopMetrics(), enable: false, allowed: null);
    }

    [NUnit.Framework.Test]
    public void Disabled_Should_Throw_NotSupported()
    {
        var page = new Mock<Microsoft.Playwright.IPage>(MockBehavior.Strict);
        NUnit.Framework.Assert.ThrowsAsync<NotSupportedException>(async () =>
            await PlaywrightAdapterTelemetry.EvalAsync<int>(page.Object, "() => 1", "tests/disabled", CancellationToken.None));
        page.Verify(p => p.EvaluateAsync<int>(It.IsAny<string>(), null), Times.Never);
    }

    [NUnit.Framework.Test]
    public void Enabled_But_Path_Not_Whitelisted_Should_Throw()
    {
        PlaywrightAdapterTelemetry.Init(new NoopMetrics(), enable: true, allowed: new[] { "element.probeVisibility" });
        var page = new Mock<Microsoft.Playwright.IPage>(MockBehavior.Strict);
        NUnit.Framework.Assert.ThrowsAsync<NotSupportedException>(async () =>
            await PlaywrightAdapterTelemetry.EvalAsync<int>(page.Object, "() => 1", "not.allowed", CancellationToken.None));
    }

    [NUnit.Framework.Test]
    public async Task Enabled_And_Whitelisted_Should_Invoke_Underlying()
    {
        PlaywrightAdapterTelemetry.Init(new NoopMetrics(), enable: true, allowed: new[] { "element.probeVisibility" });
        var page = new Mock<Microsoft.Playwright.IPage>();
        page.Setup(p => p.EvaluateAsync<int>(It.IsAny<string>(), null)).ReturnsAsync(7);
        var v = await PlaywrightAdapterTelemetry.EvalAsync<int>(page.Object, "() => 7", "element.probeVisibility", CancellationToken.None);
        NUnit.Framework.Assert.That(v, NUnit.Framework.Is.EqualTo(7));
        page.Verify(p => p.EvaluateAsync<int>(It.IsAny<string>(), null), Times.Once);
    }
}
