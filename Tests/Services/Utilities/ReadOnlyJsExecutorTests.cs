using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Observability;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace Tests.Services.Utilities;

/// <summary>
/// IReadOnlyJsExecutor（只读 JS 执行门控）单元测试：
/// - 当禁用时应抛出 NotSupportedException；
/// - 当启用时应计数并调用底层 Evaluate 方法。
/// </summary>
public class ReadOnlyJsExecutorTests
{
    private sealed class CapturingMetrics : IMetrics
    {
        public long Count { get; private set; }
        public IReadOnlyDictionary<string, object?>? LastLabels { get; private set; }

        private sealed class Counter : ICounter
        {
            private readonly Action<long, LabelSet> _onAdd;
            public Counter(Action<long, LabelSet> onAdd) { _onAdd = onAdd; }
            public void Add(long value, in LabelSet labels) => _onAdd(value, labels);
        }

        public ICounter CreateCounter(string name, string? description = null)
            => new Counter((v, labels) => { Count += v; LastLabels = labels.Labels; });

        public IHistogram CreateHistogram(string name, string? description = null)
            => new NoopMetrics().CreateHistogram(name, description);
    }

    [Test]
    public void Disabled_Should_Throw_NotSupported()
    {
        var options = Options.Create(new XiaoHongShuMCP.Services.XhsSettings
        {
            InteractionPolicy = new XiaoHongShuMCP.Services.XhsSettings.InteractionPolicySection
            {
                EnableJsReadEval = false
            }
        });
        var executor = new XiaoHongShuMCP.Services.Utilities.ReadOnlyJsExecutor(options, metrics: null);

        var page = new Mock<IAutoPage>(MockBehavior.Strict);
        Assert.ThrowsAsync<NotSupportedException>(async () =>
            await executor.ExecuteOnPageAsync<object>(page.Object, "() => 1", "tests/disabled"));
    }

    [Test]
    public async Task Enabled_Should_Count_And_Invoke_Page_Evaluate()
    {
        var options = Options.Create(new XiaoHongShuMCP.Services.XhsSettings
        {
            InteractionPolicy = new XiaoHongShuMCP.Services.XhsSettings.InteractionPolicySection
            {
                EnableJsReadEval = true
            }
        });
        var metrics = new CapturingMetrics();
        var executor = new XiaoHongShuMCP.Services.Utilities.ReadOnlyJsExecutor(options, metrics);

        var page = new Mock<IAutoPage>();
        page.Setup(p => p.EvaluateAsync<int>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var result = await executor.ExecuteOnPageAsync<int>(page.Object, "() => 42", "tests/enabled");
        Assert.That(result, Is.EqualTo(42));
        Assert.That(metrics.Count, Is.EqualTo(1));
        Assert.That(metrics.LastLabels, Is.Not.Null);
        Assert.That(metrics.LastLabels!["type"]?.ToString(), Is.EqualTo("eval"));
        Assert.That(metrics.LastLabels!["path"]?.ToString(), Is.EqualTo("tests/enabled"));
    }
}
