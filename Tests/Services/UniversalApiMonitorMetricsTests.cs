using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using HushOps.Core.Automation.Abstractions;
using XiaoHongShuMCP.Services;
using Moq;
using System.Reflection;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// 指标抽象单测：验证当注入 IMetrics 时内部度量对象创建；未注入时不创建。
/// 说明：仅做“存在性”校验，避免对具体实现产生耦合；破坏性变更后不再依赖 UAM 配置开关。
/// </summary>
[TestFixture]
public class UniversalApiMonitorMetricsTests
{
    private IOptions<XhsSettings> CreateOptions()
    {
        var cfg = new XhsSettings
        {
            UniversalApiMonitor = new XhsSettings.UniversalApiMonitorSection { MetricsLogEveryResponses = 0 },
            McpSettings = new XhsSettings.McpSettingsSection()
        };
        return Options.Create(cfg);
    }

    private static object? GetPrivateField(object obj, string name)
        => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj);

    [Test]
    public void Without_IMetrics_ShouldNotCreateMeters()
    {
        var logger = Mock.Of<ILogger<UniversalApiMonitor>>();
        var monitor = new UniversalApiMonitor(
            logger,
            CreateOptions(),
            breaker: null,
            accountManager: null,
            network: Mock.Of<INetworkMonitor>(),
            metrics: null
        );

        Assert.That(GetPrivateField(monitor, "_mTotalResponses"), Is.Null);
        Assert.That(GetPrivateField(monitor, "_mSuccess2xx"), Is.Null);
        Assert.That(GetPrivateField(monitor, "_mProcessDurationMs"), Is.Null);
    }

    [Test]
    public void With_IMetrics_ShouldCreateMeters()
    {
        var logger = Mock.Of<ILogger<UniversalApiMonitor>>();
        var fakeMetrics = new HushOps.Core.Observability.NoopMetrics();
        var monitor = new UniversalApiMonitor(
            logger,
            CreateOptions(),
            breaker: null,
            accountManager: null,
            network: Mock.Of<INetworkMonitor>(),
            metrics: fakeMetrics
        );

        Assert.That(GetPrivateField(monitor, "_mTotalResponses"), Is.Not.Null);
        Assert.That(GetPrivateField(monitor, "_mSuccess2xx"), Is.Not.Null);
        Assert.That(GetPrivateField(monitor, "_mProcessDurationMs"), Is.Not.Null);
    }
}
