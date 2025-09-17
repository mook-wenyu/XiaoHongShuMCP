using System;
using System.Linq;
using System.Reflection;
using NetArchTest.Rules;
using NUnit.Framework;

namespace Tests.Architecture;

/// <summary>
/// 架构守卫测试（NetArchTest）：
/// - Core：禁止引用 Playwright 与主工程/适配器工程命名空间
/// - Adapters.Playwright：禁止引用主工程服务层命名空间
/// 说明：该测试仅验证编译后类型依赖，作为持续集成的静态守卫。
/// </summary>
public class ArchitectureRulesTests
{
    private static Assembly Load(string name)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
        if (loaded != null) return loaded;
        // 主动加载指定名称的程序集，避免因未直接引用而未被测试运行时加载
        return Assembly.Load(name);
    }

    [Test]
    public void Core_Should_Not_Depend_On_Playwright_Or_Hosts()
    {
        var core = Load("HushOps.Core");
        var result = Types.InAssembly(core)
            .That().ResideInNamespace("HushOps.Core")
            .And().DoNotResideInNamespace("HushOps.Core.Runtime.Playwright")
            .Should().NotHaveDependencyOnAny(new[] { "Microsoft.Playwright", "XiaoHongShuMCP.Services" })
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True);
    }

    [Test]
    public void Runtime_Playwright_Should_Not_Depend_On_ServiceLayer()
    {
        // 破坏式合流后：运行时代码位于 Core.Runtime.Playwright.* 命名空间
        var core = Load("HushOps.Core");
        var result = Types.InAssembly(core)
            .That().ResideInNamespace("HushOps.Core.Runtime.Playwright")
            .Should().NotHaveDependencyOnAny(new[] { "XiaoHongShuMCP.Services" })
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True);
    }

    [Test]
    public void Observability_Should_Not_Depend_On_Services_Or_Adapters()
    {
        var core = Load("HushOps.Core");
        var result = Types.InAssembly(core)
            .That().ResideInNamespace("HushOps.Observability")
            .Should().NotHaveDependencyOnAny(new[] {
                "XiaoHongShuMCP.Services",
                "HushOps.Core.Runtime.Playwright"
            })
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True);
    }

    [Test]
    public void Persistence_Should_Not_Depend_On_Services_Or_Adapters()
    {
        var core = Load("HushOps.Core");
        var result = Types.InAssembly(core)
            .That().ResideInNamespace("HushOps.Persistence")
            .Should().NotHaveDependencyOnAny(new[] {
                "XiaoHongShuMCP.Services",
                "HushOps.Core.Runtime.Playwright"
            })
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True);
    }
}
