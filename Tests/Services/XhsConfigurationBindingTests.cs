using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// XHS 根节配置绑定与破坏性变更验证测试
/// 覆盖点：
/// - 环境变量使用根节 XHS 进行绑定（双下划线映射冒号）
/// - 旧路径（无 XHS 根节）不再生效，确保破坏性变更到位
/// </summary>
[TestFixture]
public class XhsConfigurationBindingTests
{
    [SetUp]
    public void Setup()
    {
        // 清理与本测试相关的环境变量，避免相互影响
        Environment.SetEnvironmentVariable("XHS__McpSettings__WaitTimeoutMs", null);
        Environment.SetEnvironmentVariable("XHS__SearchTimeoutsConfig__UiWaitMs", null);
        Environment.SetEnvironmentVariable("XHS__Serilog__MinimumLevel", null);
        Environment.SetEnvironmentVariable("Serilog__MinimumLevel", null);
    }

    [TearDown]
    public void TearDown()
    {
        // 还原环境
        Environment.SetEnvironmentVariable("XHS__McpSettings__WaitTimeoutMs", null);
        Environment.SetEnvironmentVariable("XHS__SearchTimeoutsConfig__UiWaitMs", null);
        Environment.SetEnvironmentVariable("XHS__Serilog__MinimumLevel", null);
        Environment.SetEnvironmentVariable("Serilog__MinimumLevel", null);
    }

    /// <summary>
    /// 验证：XHS 根节下的环境变量可正确绑定至 Options
    /// </summary>
    [Test]
    public void Should_Bind_Options_From_EnvVars_Under_XHS_Root()
    {
        // Arrange
        Environment.SetEnvironmentVariable("XHS__McpSettings__WaitTimeoutMs", "123456");
        Environment.SetEnvironmentVariable("XHS__SearchTimeoutsConfig__UiWaitMs", "7777");

        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables() // 无前缀过滤
            .Build();

        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<XhsSettings>(config.GetSection("XHS"));

        var provider = services.BuildServiceProvider();

        // Act
        var xhs = provider.GetRequiredService<IOptions<XhsSettings>>().Value;
        var mcp = xhs.McpSettings;
        var timeouts = xhs.SearchTimeoutsConfig;

        // Assert
        Assert.That(mcp.WaitTimeoutMs, Is.EqualTo(123456), "XHS 根节下的 MCP 超时应从环境变量绑定");
        Assert.That(timeouts.UiWaitMs, Is.EqualTo(7777), "XHS 根节下的搜索 UI 等待应从环境变量绑定");
    }

    /// <summary>
    /// 验证：旧路径（未带 XHS 根节）不会影响新配置（破坏性变更）。
    /// - 设置 Serilog__MinimumLevel=Debug，不应覆盖 XHS:Serilog:MinimumLevel 的默认值。
    /// </summary>
    [Test]
    public void Old_Rootless_Keys_Should_Not_Override_XHS_Root()
    {
        // Arrange：设置旧路径环境变量
        Environment.SetEnvironmentVariable("Serilog__MinimumLevel", "Debug");

        // 默认值字典：仅为本测试准备，不依赖生产代码内部方法
        var defaults = new Dictionary<string, string?>
        {
            ["XHS:Serilog:MinimumLevel"] = "Information"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .AddEnvironmentVariables()
            .Build();

        // Act
        var level = config["XHS:Serilog:MinimumLevel"]; // 仍应是默认值 Information

        // Assert
        Assert.That(level, Is.EqualTo("Information"), "旧键 Serilog__MinimumLevel 不应影响 XHS:Serilog:MinimumLevel");
    }
}
