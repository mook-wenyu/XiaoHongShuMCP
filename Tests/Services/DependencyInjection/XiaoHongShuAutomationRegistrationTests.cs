using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Services.Extensions;

namespace Tests.Services.DependencyInjection;

/// <summary>
/// 验证 AddXiaoHongShuAutomation 扩展的核心注册结果，确保后续功能具备运行基础。
/// </summary>
[TestFixture]
public class XiaoHongShuAutomationRegistrationTests
{
    [Test]
    public async Task AddXiaoHongShuAutomation_ShouldRegisterCoreServices()
    {
        // 准备：使用最小化配置，避免引入额外安全设计
        var configValues = new Dictionary<string, string?>
        {
            ["XHS:Persona:Http429BaseMultiplier"] = "2.5",
            ["XHS:Persona:Http403BaseMultiplier"] = "2.0",
            ["XHS:Persona:MaxDelayMultiplier"] = "3.0",
            ["XHS:Persona:DegradeHalfLifeSeconds"] = "60"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton<IConfiguration>(configuration);

        // 执行：注册拟人化与反检测组件
        var coreSettings = HushOps.Core.Config.XhsConfiguration.LoadFromEnvironment();
        services.AddXiaoHongShuAutomation(configuration, coreSettings);

        await using var provider = services.BuildServiceProvider();

        // 断言：关键服务可解析，且文本策略不少于两个，满足拟人化需求
        Assert.DoesNotThrow(() => provider.GetRequiredService<IXiaoHongShuService>());
        Assert.DoesNotThrow(() => provider.GetRequiredService<IHumanizedInteractionService>());
        var strategies = provider.GetRequiredService<IEnumerable<ITextInputStrategy>>().ToList();
        Assert.That(strategies.Count, Is.GreaterThanOrEqualTo(2));
    }


}
