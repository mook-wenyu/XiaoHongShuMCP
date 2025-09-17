using System;
using System.Linq;
using FluentAssertions;
using NetArchTest.Rules;

namespace HushOps.Core.Tests;

/// <summary>
/// 中文：架构守卫测试。
/// 目标：仅允许 Microsoft.Playwright 依赖存在于 Core.Runtime.Playwright.* 命名空间；
/// 其他域（Core.Policy.*、Core.Observability.*、Core.Resilience.* 等）均不得引用 Playwright。
/// 说明：作为破坏式重构后的“红线测试”，防止回归。
/// </summary>
public class ArchitectureGuards
{
    [Fact]
    public void NonRuntimeNamespaces_MustNot_DependOn_Playwright()
    {
        // 筛选 HushOps.Core 程序集中除 Runtime.Playwright.* 之外的所有类型
        var result = Types.InAssembly(typeof(HushOps.Core.Observability.Logging).Assembly)
            .That()
            .ResideInNamespace("HushOps.Core", true)
            .And().DoNotResideInNamespace("HushOps.Core.Runtime.Playwright", true)
            .Should().NotHaveDependencyOn("Microsoft.Playwright")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("仅 Runtime.Playwright 子命名空间可以依赖 Microsoft.Playwright，其它域必须纯净");
    }
}

